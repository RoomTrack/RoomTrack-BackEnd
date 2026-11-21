using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Reqnroll;
using RoomTrack.IAM.Domain.Model.Aggregates;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Domain.Model.Enums;
using RoomTrack.IAM.Domain.Model.Events;
using RoomTrack.IAM.Domain.Model.ValueObjects;
using RoomTrack.IAM.Domain.Services;
using RoomTrack.Shared.Domain.Model.Exceptions;
using static RoomTrack.Identity.API.Tests.IAM.IdentityTestData;

namespace RoomTrack.Identity.API.Tests.Features.Steps;

/// <summary>Steps of the authentication scenarios on the User aggregate and the password policy.</summary>
[Binding]
public class AuthenticationSteps
{
    private static readonly Dictionary<string, string> Roles = new()
    {
        ["huésped"] = UserRoles.Guest,
        ["recepción"] = UserRoles.Reception,
        ["limpieza"] = UserRoles.Housekeeping,
        ["mantenimiento"] = UserRoles.Maintenance,
        ["administrador"] = UserRoles.Admin
    };

    private static readonly Dictionary<string, SessionRevocationReason> Reasons = new()
    {
        ["cambio de rol"] = SessionRevocationReason.RoleChanged,
        ["cambio de contraseña"] = SessionRevocationReason.PasswordChanged
    };

    private readonly TotpSecret _secret = RfcSecret();
    private User _user = null!;
    private Email? _email;
    private PasswordCheck? _passwordCheck;
    private int _openSessionVersion;
    private DateTimeOffset _lastCodeAt;
    private MfaCodeOutcome? _codeOutcome;
    private bool _lockStarted;
    private Exception? _signInError;

    // ── Given ───────────────────────────────────────────────────────

    [Given("una cuenta de huésped activa")]
    public void GivenAnActiveGuestAccount() => _user = NewUser(1, UserRoles.Guest);

    [Given("una cuenta de {string} activa")]
    public void GivenAnActiveAccount(string role) => _user = NewUser(1, Roles[role], hotelId: 1);

    [Given("una cuenta de {string} con el correo {string}")]
    public void GivenAnAccountWithEmail(string role, string email)
    {
        _user = NewUser(1, Roles[role], hotelId: 1, email: email);
        _email = new Email(email);
    }

    [Given("una cuenta de {string} con el segundo factor activo")]
    public void GivenAnAccountWithMfa(string role)
    {
        GivenAnActiveAccount(role);
        _user.StartMfaEnrollment("protected-secret");
        _user.ConfirmMfaEnrollment(_secret, CodeAt(Now), Lockout, Now).Should().Be(MfaCodeOutcome.Accepted);
    }

    [Given("que la cuenta fue bloqueada por intentos fallidos")]
    public void GivenTheAccountWasLocked() => WhenTheWrongPasswordIsEntered(Lockout.MaxConsecutiveFailures);

    [Given("que el usuario ya usó el código actual de su aplicación")]
    public void GivenTheCurrentCodeWasUsed()
    {
        _lastCodeAt = Now.AddSeconds(30);
        _user.VerifyMfaCode(_secret, CodeAt(_lastCodeAt), Lockout, _lastCodeAt).Should().Be(MfaCodeOutcome.Accepted);
    }

    [Given("que el usuario tiene una sesión abierta")]
    public void GivenAnOpenSession()
    {
        _openSessionVersion = _user.TokenVersion;
        _user.GetSession(_openSessionVersion).IsValid.Should().BeTrue();
    }

    // ── When ────────────────────────────────────────────────────────

    [When("se ingresa una contraseña incorrecta {int} veces seguidas")]
    public void WhenTheWrongPasswordIsEntered(int times)
    {
        for (var i = 0; i < times; i++) _lockStarted = _user.RegisterFailedSignIn(Lockout, Now);
    }

    [When("el usuario inicia sesión con la contraseña correcta después de {int} minutos")]
    public void WhenSigningInAfter(int minutes)
    {
        try { _user.RegisterSuccessfulSignIn(Now.AddMinutes(minutes)); }
        catch (DomainException e) { _signInError = e; }
    }

    [When("el usuario confirma el segundo factor con un código de su aplicación autenticadora")]
    public void WhenConfirmingTheSecondFactor()
    {
        _user.StartMfaEnrollment("protected-secret");
        _codeOutcome = _user.ConfirmMfaEnrollment(_secret, CodeAt(Now), Lockout, Now);
    }

    [When("el usuario intenta usar el mismo código otra vez")]
    public void WhenReusingTheCode() => _codeOutcome = _user.VerifyMfaCode(_secret, CodeAt(_lastCodeAt), Lockout, _lastCodeAt);

    [When("el usuario elige la contraseña {string}")]
    public void WhenChoosingThePassword(string password) => _passwordCheck = PasswordPolicy.Check(password, _user.Role, _email);

    [When("el administrador le cambia el rol a {string}")]
    public void WhenTheAdministratorChangesTheRole(string role) => _user.AssignRole(Roles[role], changedByUserId: 9, Now);

    // ── Then ────────────────────────────────────────────────────────

    [Then("la cuenta queda bloqueada durante {int} minutos")]
    public void ThenTheAccountIsLockedFor(int minutes)
    {
        _lockStarted.Should().BeTrue();
        _user.IsLockedOut(Now.AddMinutes(minutes).AddSeconds(-1)).Should().BeTrue();
        _user.IsLockedOut(Now.AddMinutes(minutes)).Should().BeFalse();
    }

    [Then("el propietario recibe el aviso del bloqueo")]
    public void ThenTheOwnerIsNotified() =>
        // The lock event is what the application turns into the e-mail to the owner.
        _user.DomainEvents.OfType<UserLockedOutEvent>().Should().ContainSingle();

    [Then("el inicio de sesión es aceptado")]
    public void ThenTheSignInIsAccepted()
    {
        _signInError.Should().BeNull();
        _user.DomainEvents.OfType<UserSignedInEvent>().Should().ContainSingle();
    }

    [Then("la cuenta debe activar el segundo factor antes de operar")]
    public void ThenTheAccountMustEnrollMfa() => _user.RequiresMfaEnrollment.Should().BeTrue();

    [Then("la cuenta tiene el segundo factor activo")]
    public void ThenMfaIsEnabled()
    {
        _codeOutcome.Should().Be(MfaCodeOutcome.Accepted);
        _user.MfaEnabled.Should().BeTrue();
        _user.RequiresMfaEnrollment.Should().BeFalse();
    }

    [Then("el código es rechazado por reutilizado")]
    public void ThenTheCodeIsRejectedAsReplay() => _codeOutcome.Should().Be(MfaCodeOutcome.Replayed);

    [Then("la contraseña es rechazada con el código {string}")]
    public void ThenThePasswordIsRejected(string code) => _passwordCheck!.Code.Should().Be(code);

    [Then("la contraseña es aceptada")]
    public void ThenThePasswordIsAccepted() => _passwordCheck!.IsAcceptable.Should().BeTrue();

    [Then("la sesión abierta deja de ser válida por {string}")]
    public void ThenTheOpenSessionIsRevoked(string reason)
    {
        var session = _user.GetSession(_openSessionVersion);
        session.IsValid.Should().BeFalse();
        session.RevocationReason.Should().Be(Reasons[reason]);
    }

    private string CodeAt(DateTimeOffset at) => TotpAlgorithm.CodeAt(_secret, TotpAlgorithm.TimeStepAt(at));
}
