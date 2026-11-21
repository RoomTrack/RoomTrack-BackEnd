using FluentAssertions;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Domain.Model.Exceptions;
using RoomTrack.IAM.Domain.Model.ValueObjects;
using RoomTrack.IAM.Domain.Services;
using Xunit;

namespace RoomTrack.Identity.API.Tests.IAM.Domain;

/// <summary>NIST SP 800-63B-4 password rules: length by role, blocklist, repetition and context words.</summary>
public class PasswordPolicyTests
{
    private static readonly Role Staff = new(UserRoles.Reception);
    private static readonly Role Guest = new(UserRoles.Guest);
    private static readonly Email Mail = new("carlos.perez@hotel.pe");

    [Fact]
    public void StaffNeedEightCharacters_AndGuestsFifteen()
    {
        PasswordPolicy.MinimumLengthFor(Staff).Should().Be(8);
        PasswordPolicy.MinimumLengthFor(Guest).Should().Be(15);

        PasswordPolicy.Check("verde azul", Staff, Mail).IsAcceptable.Should().BeTrue();
        var guest = PasswordPolicy.Check("verde azul", Guest, Mail);
        guest.Code.Should().Be(IamErrorCodes.PasswordTooShort);
        guest.Parameters!["minLength"].Should().Be(15);
    }

    [Fact]
    public void APassphrase_ShouldBeAcceptedForAGuest()
    {
        PasswordPolicy.Check("tres tigres comen trigo", Guest, Mail).Should().Be(PasswordCheck.Acceptable);
    }

    [Fact]
    public void Length_ShouldCountCodePoints_SoAnEmojiIsOneCharacter()
    {
        // 14 code points, 15 UTF-16 units: still too short for a guest.
        const string password = "cafe con lech🌙";
        password.Length.Should().Be(15);

        PasswordPolicy.Check(password, Guest, null).Code.Should().Be(IamErrorCodes.PasswordTooShort);
    }

    [Theory]
    [InlineData(null, IamErrorCodes.PasswordRequired)]
    [InlineData("", IamErrorCodes.PasswordRequired)]
    [InlineData("password123", IamErrorCodes.PasswordTooCommon)]
    [InlineData("Contraseña123", IamErrorCodes.PasswordTooCommon)]
    [InlineData("ｐａｓｓｗｏｒｄ", IamErrorCodes.PasswordTooCommon)] // full-width, NFKC → "password"
    [InlineData("RoomTrack2026!", IamErrorCodes.PasswordTooCommon)]
    [InlineData("zzzzzzzzzz", IamErrorCodes.PasswordRepetitive)]
    [InlineData("abcdefghij", IamErrorCodes.PasswordRepetitive)]
    [InlineData("98765432", IamErrorCodes.PasswordRepetitive)]
    [InlineData("xyzxyzxyz", IamErrorCodes.PasswordRepetitive)]
    [InlineData("carlos.perez", IamErrorCodes.PasswordContainsEmail)]
    [InlineData("Carlos_Perez", IamErrorCodes.PasswordContainsEmail)]
    [InlineData("carlos.perez@hotel.pe", IamErrorCodes.PasswordContainsEmail)]
    public void WeakPasswords_ShouldBeRejected_WithTheirCode(string? password, string code)
    {
        PasswordPolicy.Check(password, Staff, Mail).Code.Should().Be(code);
    }

    [Fact]
    public void APasswordLongerThan128Characters_ShouldBeRejected()
    {
        var check = PasswordPolicy.Check(new string('a', 64) + new string('b', 65), Staff, Mail);

        check.Code.Should().Be(IamErrorCodes.PasswordTooLong);
        check.Parameters!["maxLength"].Should().Be(128);
    }

    [Fact]
    public void AnyCharacter_ShouldBeAllowed_WithoutCompositionRules()
    {
        PasswordPolicy.Check("mañana iré al río 🌊", Guest, Mail).IsAcceptable.Should().BeTrue();
        PasswordPolicy.Check("solominusculas", Staff, Mail).IsAcceptable.Should().BeTrue();
    }
}
