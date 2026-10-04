using BackendAwRoomTrack.API.IAM.Domain.Model.ValueObjects;

namespace BackendAwRoomTrack.API.IAM.Application.OutboundServices;

/// <summary>
///     Tells whether the session of an access token is still valid (active user, current token version). The Identity
///     service answers from its own database; the other services ask it over HTTP (with a short cache).
/// </summary>
public interface IUserSessionValidator
{
    Task<UserSession> GetSessionAsync(int userId, int tokenVersion, CancellationToken cancellationToken = default);
}
