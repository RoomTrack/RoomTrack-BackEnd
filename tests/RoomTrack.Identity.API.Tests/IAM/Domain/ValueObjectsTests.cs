using System;
using System.Linq;
using FluentAssertions;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Domain.Model.Exceptions;
using RoomTrack.IAM.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Xunit;

namespace RoomTrack.Identity.API.Tests.IAM.Domain;

public class EmailTests
{
    [Fact]
    public void Email_ShouldBeTrimmedAndLowerCased_SoTheLoginIsCaseInsensitive()
    {
        new Email("  Ana.Torres@Hotel.PE ").Value.Should().Be("ana.torres@hotel.pe");
        new Email("ANA@hotel.pe").Should().Be(new Email("ana@HOTEL.pe"));
    }

    [Theory]
    [InlineData("", IamErrorCodes.EmailRequired)]
    [InlineData("   ", IamErrorCodes.EmailRequired)]
    [InlineData("ana", IamErrorCodes.EmailInvalid)]
    [InlineData("ana@hotel", IamErrorCodes.EmailInvalid)]
    [InlineData("ana torres@hotel.pe", IamErrorCodes.EmailInvalid)]
    [InlineData("ana@@hotel.pe", IamErrorCodes.EmailInvalid)]
    public void InvalidEmails_ShouldBeRejected_WithTheirCode(string value, string code)
    {
        var act = () => new Email(value);

        act.Should().Throw<DomainValidationException>().Which.Code.Should().Be(code);
        Email.IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void AnEmailLongerThan254Characters_ShouldBeRejected()
    {
        var value = new string('a', 250) + "@x.pe";

        var act = () => new Email(value);

        act.Should().Throw<DomainValidationException>().Which.Code.Should().Be(IamErrorCodes.EmailTooLong);
        Email.IsValid(value).Should().BeFalse();
    }
}

public class PersonNameTests
{
    [Fact]
    public void Names_ShouldCollapseTheirSpaces_AndAcceptAccentsHyphensAndApostrophes()
    {
        var name = new PersonName("  María   José ", "O'Connor-Ñúñez");

        name.FirstName.Should().Be("María José");
        name.LastName.Should().Be("O'Connor-Ñúñez");
        name.FullName.Should().Be("María José O'Connor-Ñúñez");
    }

    [Theory]
    [InlineData("", IamErrorCodes.NameRequired)]
    [InlineData("A", IamErrorCodes.NameLength)]
    [InlineData("Ana3", IamErrorCodes.NameFormat)]
    [InlineData("Ana--Luz", IamErrorCodes.NameFormat)]
    [InlineData("-Ana", IamErrorCodes.NameFormat)]
    public void InvalidFirstNames_ShouldBeRejected_WithTheirCode(string firstName, string code)
    {
        var act = () => new PersonName(firstName, "Torres");

        act.Should().Throw<DomainValidationException>().Which.Code.Should().Be(code);
        PersonName.IsValidPart(firstName).Should().BeFalse();
    }

    [Fact]
    public void ANameLongerThan50Characters_ShouldBeRejected()
    {
        var act = () => new PersonName("Ana", new string('a', PersonName.MaxLength + 1));

        act.Should().Throw<DomainValidationException>().Which.Code.Should().Be(IamErrorCodes.NameLength);
    }
}

public class RoleTests
{
    [Theory]
    [InlineData("Admin", UserRoles.Admin, 2)]
    [InlineData(" CHAIN_ADMIN ", UserRoles.ChainAdmin, 3)]
    [InlineData("reception", UserRoles.Reception, 1)]
    [InlineData("guest", UserRoles.Guest, 0)]
    public void Roles_ShouldBeCanonicalLowerCase_WithTheirHierarchyLevel(string value, string canonical, int level)
    {
        var role = new Role(value);

        role.Value.Should().Be(canonical);
        role.HierarchyLevel.Should().Be(level);
    }

    [Theory]
    [InlineData("", IamErrorCodes.RoleRequired)]
    [InlineData("superuser", IamErrorCodes.RoleUnknown)]
    public void UnknownOrEmptyRoles_ShouldBeRejected(string value, string code)
    {
        var act = () => new Role(value);

        act.Should().Throw<DomainValidationException>().Which.Code.Should().Be(code);
    }

    [Fact]
    public void Hierarchy_ShouldLetAHigherRoleManageALowerOne_ButNotItsPeers()
    {
        Role admin = UserRoles.Admin;
        Role reception = UserRoles.Reception;
        Role housekeeping = UserRoles.Housekeeping;

        admin.CanManage(reception).Should().BeTrue();
        reception.CanManage(admin).Should().BeFalse();
        reception.CanManage(housekeeping).Should().BeFalse();
        reception.CanManageOrEqual(housekeeping).Should().BeTrue();
    }

    [Fact]
    public void EveryStaffRole_ShouldRequireASecondFactor_ButNotGuests()
    {
        new[] { UserRoles.Reception, UserRoles.Housekeeping, UserRoles.Maintenance, UserRoles.Admin, UserRoles.ChainAdmin }
            .Select(value => new Role(value))
            .Should().OnlyContain(role => role.IsStaff && role.RequiresMultiFactorAuthentication);
        new Role(UserRoles.Guest).RequiresMultiFactorAuthentication.Should().BeFalse();
    }
}

public class RecoveryCodeFormatTests
{
    [Fact]
    public void NewCodes_ShouldBeWellFormed_AndAvoidLookAlikeCharacters()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => RecoveryCodeFormat.NewCode()).ToList();

        codes.Should().OnlyContain(code => code.Length == 11 && code[5] == '-' && RecoveryCodeFormat.IsWellFormed(code));
        string.Concat(codes).Should().NotContainAny("0", "O", "1", "I", "L");
        codes.Distinct().Should().HaveCount(codes.Count);
    }

    [Fact]
    public void TypedCodes_ShouldIgnoreCaseSpacesAndHyphens()
    {
        RecoveryCodeFormat.Canonicalize(" abcde - fghjk ").Should().Be("ABCDEFGHJK");
        RecoveryCodeFormat.IsWellFormed("abcde fghjk").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ABCDE-FGHJ")]
    [InlineData("ABCDE-FGHJKM")]
    [InlineData("ABCDE-FGHJ0")]
    public void MalformedCodes_ShouldBeRejected(string? code) => RecoveryCodeFormat.IsWellFormed(code).Should().BeFalse();
}

public class SignInLockoutPolicyTests
{
    [Fact]
    public void ThePolicy_ShouldNeedAtLeastOneAttempt_AndAPositiveDuration()
    {
        var noAttempts = () => new SignInLockoutPolicy(0, TimeSpan.FromMinutes(1));
        var noDuration = () => new SignInLockoutPolicy(3, TimeSpan.Zero);

        noAttempts.Should().Throw<DomainValidationException>();
        noDuration.Should().Throw<DomainValidationException>();
    }
}
