using System.ComponentModel.DataAnnotations;
using BackendAwRoomTrack.API.IAM.Domain.Model.ValueObjects;

namespace BackendAwRoomTrack.API.IAM.Application.Internal.Configuration;

/// <summary>
///     Account security settings (section <c>AccountSecurity</c>, env vars <c>AccountSecurity__*</c>), validated
///     at startup. The defaults follow the acceptance criteria: 5 failed attempts, 30-minute recovery
///     links.
/// </summary>
public class AccountSecuritySettings
{
    public const string SectionName = "AccountSecurity";

    /// <summary>Consecutive failed sign-ins that lock the account.</summary>
    [Range(1, 20)]
    public int LockoutMaxFailedAttempts { get; set; } = 5;

    /// <summary>Duration of the temporary lock.</summary>
    [Range(1, 24 * 60)]
    public int LockoutDurationMinutes { get; set; } = 15;

    /// <summary>Lifetime of the e-mail verification link.</summary>
    [Range(1, 7 * 24)]
    public int EmailVerificationTokenLifetimeHours { get; set; } = 24;

    /// <summary>Lifetime of the password reset link (30 minutes).</summary>
    [Range(5, 24 * 60)]
    public int PasswordResetTokenLifetimeMinutes { get; set; } = 30;

    /// <summary>Lifetime of a remembered session since its last use.</summary>
    [Range(1, 365)]
    public int RefreshTokenLifetimeDays { get; set; } = 30;

    public SignInLockoutPolicy LockoutPolicy =>
        new(LockoutMaxFailedAttempts, TimeSpan.FromMinutes(LockoutDurationMinutes));

    public TimeSpan EmailVerificationTokenLifetime => TimeSpan.FromHours(EmailVerificationTokenLifetimeHours);
    public TimeSpan PasswordResetTokenLifetime => TimeSpan.FromMinutes(PasswordResetTokenLifetimeMinutes);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenLifetimeDays);
}
