using RoomTrack.IAM.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.IAM.Domain.Model.ValueObjects;

/// <summary>
///     After <see cref="MaxConsecutiveFailures"/> consecutive failed sign-ins the account is locked
///     for <see cref="LockoutDuration"/>.
/// </summary>
public sealed record SignInLockoutPolicy
{
    public SignInLockoutPolicy(int maxConsecutiveFailures, TimeSpan lockoutDuration)
    {
        if (maxConsecutiveFailures < 1)
            throw new DomainValidationException(IamErrorCodes.InternalInvariant, "The lockout threshold must be at least one failed attempt.");
        if (lockoutDuration <= TimeSpan.Zero)
            throw new DomainValidationException(IamErrorCodes.InternalInvariant, "The lockout duration must be positive.");
        MaxConsecutiveFailures = maxConsecutiveFailures;
        LockoutDuration = lockoutDuration;
    }

    public int MaxConsecutiveFailures { get; }
    public TimeSpan LockoutDuration { get; }
}
