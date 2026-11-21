using System;
using System.Linq;
using FluentAssertions;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Domain.Model.Enums;
using RoomTrack.IAM.Domain.Model.Events;
using RoomTrack.IAM.Domain.Model.Exceptions;
using RoomTrack.IAM.Domain.Model.ValueObjects;
using RoomTrack.IAM.Domain.Services;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Xunit;
using static RoomTrack.Identity.API.Tests.IAM.IdentityTestData;

namespace RoomTrack.Identity.API.Tests.IAM.Domain;

/// <summary>The User aggregate: temporary lock, sessions, second factor and password recovery.</summary>
public class UserTests
{
    // ── Registration ────────────────────────────────────────────────

    [Fact]
    public void Register_ShouldStartWithAnUnverifiedEmail_AndTheOwnersName()
    {
        var user = global::RoomTrack.IAM.Domain.Model.Aggregates.User.Register(new PersonName("Ana", "Torres"),
            new Email("Ana@Hotel.pe"), "hash", new Role(UserRoles.Reception), hotelId: 1, chainId: null,
            createdByUserId: 9, Now);

        user.Email.Value.Should().Be("ana@hotel.pe");
        user.FirstName.Should().Be("Ana");
        user.EmailVerified.Should().BeFalse();
        user.Status.Should().Be(UserStatus.Active);
        user.RequiresMfaEnrollment.Should().BeTrue("staff must enroll a second factor");
    }

    // ── Temporary lock after failed sign-ins ────────────────────────

    [Fact]
    public void ReachingTheMaximumFailures_ShouldLockTheAccount_ForThePolicyDuration()
    {
        var user = NewUser(1, UserRoles.Guest);

        user.RegisterFailedSignIn(Lockout, Now).Should().BeFalse();
        user.RegisterFailedSignIn(Lockout, Now).Should().BeFalse();
        user.RegisterFailedSignIn(Lockout, Now).Should().BeTrue("the third failure starts the lock");

        user.IsLockedOut(Now.AddMinutes(14)).Should().BeTrue();
        user.IsLockedOut(Now.AddMinutes(15)).Should().BeFalse();
        user.LockedUntil.Should().Be(Now.AddMinutes(15));
        user.DomainEvents.OfType<SignInFailedEvent>().Should().HaveCount(3);
        user.DomainEvents.OfType<UserLockedOutEvent>().Should().ContainSingle();
    }

    [Fact]
    public void FailuresWhileLocked_ShouldNotExtendTheLock()
    {
        var user = NewUser(1, UserRoles.Guest);
        for (var i = 0; i < 3; i++) user.RegisterFailedSignIn(Lockout, Now);

        user.RegisterFailedSignIn(Lockout, Now.AddMinutes(5)).Should().BeFalse();

        user.LockedUntil.Should().Be(Now.AddMinutes(15));
        user.FailedSignInAttempts.Should().Be(0);
    }

    [Fact]
    public void ASuccessfulSignIn_ShouldResetTheFailures_ButALockedAccountCannotSignIn()
    {
        var user = NewUser(1, UserRoles.Guest);
        user.RegisterFailedSignIn(Lockout, Now);
        user.RegisterFailedSignIn(Lockout, Now);

        user.RegisterSuccessfulSignIn(Now);
        user.FailedSignInAttempts.Should().Be(0);

        for (var i = 0; i < 3; i++) user.RegisterFailedSignIn(Lockout, Now);
        var whileLocked = () => user.RegisterSuccessfulSignIn(Now.AddMinutes(1));
        whileLocked.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(IamErrorCodes.AccountLocked);
        user.RegisterSuccessfulSignIn(Now.AddMinutes(15));
        user.LockedUntil.Should().BeNull();
    }

    // ── Sessions ────────────────────────────────────────────────────

    [Fact]
    public void ARoleChange_ShouldEndEverySession_WithItsReason()
    {
        var user = NewUser(1, UserRoles.Reception, hotelId: 1);
        var oldVersion = user.TokenVersion;

        user.AssignRole(UserRoles.Housekeeping, changedByUserId: 9, Now);

        user.GetSession(oldVersion).Should().Be(new UserSession(UserSessionStatus.Revoked, RevocationReason: SessionRevocationReason.RoleChanged));
        user.GetSession(user.TokenVersion).Should().Be(new UserSession(UserSessionStatus.Valid, UserRoles.Housekeeping, 1));
        user.DomainEvents.OfType<UserRoleChangedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void AssigningTheSameRole_ShouldKeepTheSessions()
    {
        var user = NewUser(1, UserRoles.Reception, hotelId: 1);

        user.AssignRole("Reception");

        user.TokenVersion.Should().Be(0);
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Deactivation_ShouldEndTheSessions_AndBeIdempotent()
    {
        var user = NewUser(1, UserRoles.Reception, hotelId: 1);

        user.Deactivate(9, Now).Deactivate(9, Now);

        user.GetSession(user.TokenVersion).Status.Should().Be(UserSessionStatus.Inactive);
        user.TokenVersion.Should().Be(1);
        user.DomainEvents.OfType<UserDeactivatedEvent>().Should().ContainSingle();

        user.Activate(9, Now);
        user.GetSession(user.TokenVersion).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ChangingTheHotel_ShouldEndTheSessions_ButTheSameAssignmentIsANoOp()
    {
        var user = NewUser(1, UserRoles.Reception, hotelId: 1);

        user.ChangeAssignment(1, null, 9, Now).Should().BeFalse();
        user.ChangeAssignment(2, null, 9, Now).Should().BeTrue();

        user.HotelId.Should().Be(2);
        user.SessionRevocationReason.Should().Be(SessionRevocationReason.AssignmentChanged);
    }

    [Fact]
    public void AHotelAdministrator_ShouldTakeChargeOfASingleHotel()
    {
        var admin = NewUser(1, UserRoles.Admin);
        var reception = NewUser(2, UserRoles.Reception);

        admin.TakeChargeOfHotel(5, Now);
        admin.TakeChargeOfHotel(5, Now);
        var second = () => admin.TakeChargeOfHotel(6, Now);
        var notAdmin = () => reception.TakeChargeOfHotel(5, Now);

        admin.HotelId.Should().Be(5);
        second.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(IamErrorCodes.AdminAlreadyHasHotel);
        notAdmin.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(IamErrorCodes.NotHotelAdministrator);
    }

    // ── Second factor (TOTP) ────────────────────────────────────────

    [Fact]
    public void MfaEnrollment_ShouldOnlyBeEnabled_WithACodeOfTheNewSecret()
    {
        var user = NewUser(1, UserRoles.Reception, hotelId: 1);
        var secret = RfcSecret();
        user.StartMfaEnrollment("protected-secret");

        user.ConfirmMfaEnrollment(secret, "000000", Lockout, Now).Should().Be(MfaCodeOutcome.Rejected);
        user.MfaEnabled.Should().BeFalse();

        user.ConfirmMfaEnrollment(secret, CurrentCode(secret), Lockout, Now).Should().Be(MfaCodeOutcome.Accepted);
        user.MfaEnabled.Should().BeTrue();
        user.MfaSecretProtected.Should().Be("protected-secret");
        user.MfaPendingSecretProtected.Should().BeNull();
        user.RequiresMfaEnrollment.Should().BeFalse();
    }

    [Fact]
    public void ConfirmingWithoutStartingTheEnrollment_ShouldFail()
    {
        var user = NewUser(1, UserRoles.Reception, hotelId: 1);

        var act = () => user.ConfirmMfaEnrollment(RfcSecret(), "123456", Lockout, Now);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(IamErrorCodes.MfaEnrollmentNotStarted);
    }

    [Fact]
    public void ACodeOfAnAlreadyUsedTimeStep_ShouldBeRejectedAsAReplay()
    {
        var (user, secret) = UserWithMfa();
        var next = Now.AddSeconds(30);

        user.VerifyMfaCode(secret, CurrentCode(secret, next), Lockout, next).Should().Be(MfaCodeOutcome.Accepted);
        user.VerifyMfaCode(secret, CurrentCode(secret, next), Lockout, next).Should().Be(MfaCodeOutcome.Replayed);
    }

    [Fact]
    public void WrongCodes_ShouldCountTowardTheLock()
    {
        var (user, secret) = UserWithMfa();

        user.VerifyMfaCode(secret, "000000", Lockout, Now).Should().Be(MfaCodeOutcome.Rejected);
        user.RejectRecoveryCode(Lockout, Now).Should().Be(MfaCodeOutcome.Rejected);
        user.VerifyMfaCode(secret, "000001", Lockout, Now).Should().Be(MfaCodeOutcome.LockStarted);

        user.IsLockedOut(Now).Should().BeTrue();
    }

    [Fact]
    public void AnAdministratorResettingTheSecondFactor_ShouldLiftTheLock_AndAskForANewEnrollment()
    {
        var (user, _) = UserWithMfa();
        for (var i = 0; i < 3; i++) user.RegisterFailedSignIn(Lockout, Now);

        user.ResetMfa(resetByUserId: 9, Now);

        user.MfaEnabled.Should().BeFalse();
        user.MfaSecretProtected.Should().BeNull();
        user.IsLockedOut(Now).Should().BeFalse();
        user.RequiresMfaEnrollment.Should().BeTrue();
        user.SessionRevocationReason.Should().Be(SessionRevocationReason.MfaReset);
        user.DomainEvents.OfType<MfaResetEvent>().Should().ContainSingle();
    }

    [Fact]
    public void EnrollingTwice_ShouldFail()
    {
        var (user, _) = UserWithMfa();

        var act = () => user.StartMfaEnrollment("another");

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(IamErrorCodes.MfaAlreadyEnabled);
    }

    // ── Passwords and e-mail ────────────────────────────────────────

    [Fact]
    public void ResettingThePassword_ShouldVerifyTheEmail_LiftTheLock_AndEndTheSessions()
    {
        var user = NewUser(1, UserRoles.Guest);
        for (var i = 0; i < 3; i++) user.RegisterFailedSignIn(Lockout, Now);

        user.ResetPassword("new-hash", Now);

        user.PasswordHash.Should().Be("new-hash");
        user.EmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().Be(Now);
        user.IsLockedOut(Now).Should().BeFalse();
        user.SessionRevocationReason.Should().Be(SessionRevocationReason.PasswordReset);
    }

    [Fact]
    public void ChangingThePassword_ShouldEndTheOtherSessions_AndRefuseAnEmptyHash()
    {
        var user = NewUser(1, UserRoles.Guest);

        user.ChangePassword("new-hash", Now);
        var empty = () => user.ChangePassword(" ", Now);

        user.TokenVersion.Should().Be(1);
        user.DomainEvents.OfType<UserPasswordChangedEvent>().Should().ContainSingle();
        empty.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void SigningOutEverywhere_ShouldEndEverySession()
    {
        var user = NewUser(1, UserRoles.Guest);

        user.SignOutEverywhere(Now);

        user.GetSession(0).RevocationReason.Should().Be(SessionRevocationReason.SignedOutEverywhere);
    }

    private static string CurrentCode(TotpSecret secret, DateTimeOffset? at = null) =>
        TotpAlgorithm.CodeAt(secret, TotpAlgorithm.TimeStepAt(at ?? Now));

    private static (RoomTrack.IAM.Domain.Model.Aggregates.User User, TotpSecret Secret) UserWithMfa()
    {
        var user = NewUser(1, UserRoles.Reception, hotelId: 1);
        var secret = RfcSecret();
        user.StartMfaEnrollment("protected-secret");
        user.ConfirmMfaEnrollment(secret, CurrentCode(secret), Lockout, Now);
        user.ClearDomainEvents();
        return (user, secret);
    }
}
