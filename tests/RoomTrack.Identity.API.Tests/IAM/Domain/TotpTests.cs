using System;
using FluentAssertions;
using RoomTrack.IAM.Domain.Model.ValueObjects;
using RoomTrack.IAM.Domain.Services;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Xunit;
using static RoomTrack.Identity.API.Tests.IAM.IdentityTestData;

namespace RoomTrack.Identity.API.Tests.IAM.Domain;

/// <summary>TOTP (RFC 6238, SHA-1, 6 digits, 30 s) checked against the RFC test vectors.</summary>
public class TotpTests
{
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void Codes_ShouldMatchTheRfc6238TestVectors(long unixSeconds, string expected)
    {
        var step = TotpAlgorithm.TimeStepAt(DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

        TotpAlgorithm.CodeAt(RfcSecret(), step).Should().Be(expected);
    }

    [Fact]
    public void ACode_ShouldBeAcceptedOneStepBeforeOrAfter_ButNotTwo()
    {
        var secret = RfcSecret();
        var step = TotpAlgorithm.TimeStepAt(Now);
        var code = TotpAlgorithm.CodeAt(secret, step);

        TotpAlgorithm.MatchingTimeStep(secret, code, Now).Should().Be(step);
        TotpAlgorithm.MatchingTimeStep(secret, code, Now.AddSeconds(30)).Should().Be(step);
        TotpAlgorithm.MatchingTimeStep(secret, code, Now.AddSeconds(-30)).Should().Be(step);
        TotpAlgorithm.MatchingTimeStep(secret, code, Now.AddSeconds(90)).Should().BeNull();
        TotpAlgorithm.MatchingTimeStep(secret, "000000", Now).Should().BeNull();
    }

    [Fact]
    public void Secrets_ShouldRoundTripThroughBase32_AndHave160Bits()
    {
        var secret = TotpSecret.Generate();
        var base32 = secret.ToBase32();

        base32.Should().HaveLength(32).And.MatchRegex("^[A-Z2-7]+$");
        TotpSecret.FromBase32(base32.ToLowerInvariant() + "====").ToBase32().Should().Be(base32);
        RfcSecret().ToBase32().Should().Be("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ");
    }

    [Theory]
    [InlineData("NOT-BASE32!")]
    [InlineData("GEZDGNBV")] // 40 bits: too short
    public void InvalidSecrets_ShouldBeRejected(string base32)
    {
        var act = () => TotpSecret.FromBase32(base32);

        act.Should().Throw<DomainValidationException>();
    }
}
