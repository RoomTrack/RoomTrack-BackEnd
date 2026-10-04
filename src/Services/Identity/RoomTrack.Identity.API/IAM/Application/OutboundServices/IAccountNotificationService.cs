using BackendAwRoomTrack.API.IAM.Domain.Model.Aggregates;

namespace BackendAwRoomTrack.API.IAM.Application.OutboundServices;

/// <summary>
///     E-mails about the account. The application services call it before the unit of work commits the change the
///     e-mail announces (transactional outbox: stored only if the change commits).
/// </summary>
public interface IAccountNotificationService
{
    /// <summary>Confirmation of the registration with the verification link.</summary>
    Task SendEmailVerificationAsync(User user, string verificationToken, DateTimeOffset expiresAt);

    /// <summary>The account was temporarily locked.</summary>
    Task SendAccountLockedAsync(User user, DateTimeOffset lockedUntil);

    /// <summary>Link to choose a new password.</summary>
    Task SendPasswordResetLinkAsync(User user, string resetToken, DateTimeOffset expiresAt);

    /// <summary>The password was changed.</summary>
    Task SendPasswordChangedAsync(User user);

    /// <summary>
    ///     An administrator changed the user's role or hotel; their sessions ended and they must
    ///     sign in again.
    /// </summary>
    Task SendPermissionsChangedAsync(User user);
}
