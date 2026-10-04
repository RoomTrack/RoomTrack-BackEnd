using BackendAwRoomTrack.API.IAM.Application.OutboundServices;
using BackendAwRoomTrack.API.IAM.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;
using Microsoft.Extensions.Caching.Memory;

namespace BackendAwRoomTrack.API.IAM.Infrastructure.Sessions;

/// <summary>
///     <see cref="IUserSessionValidator"/> of the services other than Identity: asks the Identity service whether the
///     session of a token is still valid. The answer is cached for <see cref="CacheDuration"/> per user and token
///     version, so a burst of requests of one user costs one call; a revocation is honored within that time.
/// </summary>
public class HttpUserSessionValidator(HttpClient httpClient, IMemoryCache cache)
    : InternalServiceClient(httpClient, "Identity"), IUserSessionValidator
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);

    public async Task<UserSession> GetSessionAsync(int userId, int tokenVersion, CancellationToken cancellationToken = default)
    {
        var key = $"user-session:{userId}:{tokenVersion}";
        if (cache.TryGetValue(key, out UserSession? cached) && cached is not null) return cached;

        var session = await GetOrDefaultAsync<UserSession>($"internal/v1/sessions/{userId}/{tokenVersion}", cancellationToken)
                      ?? UserSession.NotFound();
        cache.Set(key, session, CacheDuration);
        return session;
    }
}
