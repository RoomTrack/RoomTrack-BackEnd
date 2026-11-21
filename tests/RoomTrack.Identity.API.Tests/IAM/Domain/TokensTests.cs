using System;
using FluentAssertions;
using RoomTrack.IAM.Domain.Model.Aggregates;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Domain.Model.Enums;
using RoomTrack.IAM.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Xunit;
using static RoomTrack.Identity.API.Tests.IAM.IdentityTestData;

namespace RoomTrack.Identity.API.Tests.IAM.Domain;

/// <summary>"Remember me" refresh tokens: rotation, expiry and session generation.</summary>
public class RefreshTokenTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    [Fact]
    public void Rotation_ShouldRevokeTheTokenAndIssueItsSuccessor_InTheSameFamily()
    {
        var token = RefreshToken.StartSession(NewUser(1, UserRoles.Guest), "hash-1", Now, Lifetime);

        var successor = token.Rotate("hash-2", Now.AddDays(1), Lifetime);

        token.WasRotated.Should().BeTrue();
        token.IsActive(Now.AddDays(1)).Should().BeFalse();
        successor.FamilyId.Should().Be(token.FamilyId);
        successor.ExpiresAt.Should().Be(Now.AddDays(31), "the expiry slides with each use");
    }

    [Fact]
    public void ARotatedOrExpiredToken_ShouldNotRotateAgain()
    {
        var rotated = RefreshToken.StartSession(NewUser(1, UserRoles.Guest), "hash-1", Now, Lifetime);
        rotated.Rotate("hash-2", Now, Lifetime);
        var expired = RefreshToken.StartSession(NewUser(1, UserRoles.Guest), "hash-3", Now, Lifetime);

        var replay = () => rotated.Rotate("hash-4", Now, Lifetime);
        var late = () => expired.Rotate("hash-5", Now + Lifetime, Lifetime);

        replay.Should().Throw<InvalidRefreshTokenException>();
        late.Should().Throw<InvalidRefreshTokenException>();
    }

    [Fact]
    public void ATokenOfAnOlderSessionGeneration_ShouldNotBelongToTheCurrentSession()
    {
        var user = NewUser(1, UserRoles.Guest);
        var token = RefreshToken.StartSession(user, "hash-1", Now, Lifetime);
        token.BelongsToCurrentSessionOf(user).Should().BeTrue();

        user.ChangePassword("new-hash", Now);

        token.BelongsToCurrentSessionOf(user).Should().BeFalse();
        token.BelongsToCurrentSessionOf(NewUser(2, UserRoles.Guest)).Should().BeFalse();
    }

    [Fact]
    public void Revoking_ShouldKeepTheFirstReason()
    {
        var token = RefreshToken.StartSession(NewUser(1, UserRoles.Guest), "hash-1", Now, Lifetime);

        token.Revoke(RefreshTokenRevocationReason.SignedOut, Now);
        token.Revoke(RefreshTokenRevocationReason.ReuseDetected, Now.AddMinutes(1));

        token.RevocationReason.Should().Be(RefreshTokenRevocationReason.SignedOut);
        token.RevokedAt.Should().Be(Now);
    }

    [Fact]
    public void ATokenWithoutUserHashOrLifetime_ShouldBeRefused()
    {
        var unsaved = new User("ana@hotel.pe", "hash", UserRoles.Guest);

        var noUser = () => RefreshToken.StartSession(unsaved, "hash", Now, Lifetime);
        var noHash = () => RefreshToken.StartSession(NewUser(1, UserRoles.Guest), " ", Now, Lifetime);
        var noLifetime = () => RefreshToken.StartSession(NewUser(1, UserRoles.Guest), "hash", Now, TimeSpan.Zero);

        noUser.Should().Throw<DomainValidationException>();
        noHash.Should().Throw<DomainValidationException>();
        noLifetime.Should().Throw<DomainValidationException>();
    }
}

/// <summary>Single-use links sent by e-mail (verification and password reset).</summary>
public class AccountTokenTests
{
    [Fact]
    public void ALink_ShouldBeUsableOnlyOnce()
    {
        var token = AccountToken.Issue(1, AccountTokenPurpose.PasswordReset, "hash", Now, TimeSpan.FromHours(1));

        token.Consume(Now.AddMinutes(10));
        var again = () => token.Consume(Now.AddMinutes(11));

        token.ConsumedAt.Should().Be(Now.AddMinutes(10));
        again.Should().Throw<InvalidAccountTokenException>();
    }

    [Fact]
    public void AnExpiredLink_ShouldBeRefused()
    {
        var token = AccountToken.Issue(1, AccountTokenPurpose.EmailVerification, "hash", Now, TimeSpan.FromHours(1));

        var act = () => token.Consume(Now.AddHours(1));

        act.Should().Throw<AccountTokenExpiredException>();
        token.IsOutstanding.Should().BeTrue();
    }

    [Fact]
    public void ASupersededLink_ShouldBeRefused()
    {
        var token = AccountToken.Issue(1, AccountTokenPurpose.EmailVerification, "hash", Now, TimeSpan.FromHours(1));

        token.Revoke(Now);
        var act = () => token.Consume(Now.AddMinutes(1));

        act.Should().Throw<InvalidAccountTokenException>();
    }
}
