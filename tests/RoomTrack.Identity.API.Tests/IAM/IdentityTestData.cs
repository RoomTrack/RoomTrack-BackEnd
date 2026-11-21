using System;
using RoomTrack.IAM.Domain.Model.Aggregates;
using RoomTrack.IAM.Domain.Model.ValueObjects;

namespace RoomTrack.Identity.API.Tests.IAM;

/// <summary>Users and clock shared by the IAM tests.</summary>
internal static class IdentityTestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 20, 15, 0, 0, TimeSpan.Zero);
    public static readonly SignInLockoutPolicy Lockout = new(maxConsecutiveFailures: 3, TimeSpan.FromMinutes(15));

    /// <summary>A persisted user (with its generated id).</summary>
    public static User NewUser(int id, string role, int? hotelId = null, int? chainId = null, string? email = null)
    {
        var user = new User(email ?? $"user{id}@roomtrack.pe", "hash", role, hotelId: hotelId, chainId: chainId);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }

    /// <summary>The secret of the RFC 6238 test vectors (ASCII "12345678901234567890").</summary>
    public static TotpSecret RfcSecret() => TotpSecret.FromBase32("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ");
}
