using RoomTrack.IAM.Domain.Model.Aggregates;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.IAM.Domain.Repositories;

/// <summary>Refresh tokens of remembered sessions.</summary>
public interface IRefreshTokenRepository : IBaseRepository<RefreshToken>
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash);

    /// <summary>Tokens of a session (family) that are not revoked yet.</summary>
    Task<IReadOnlyList<RefreshToken>> ListUnrevokedByFamilyAsync(Guid familyId);

    /// <summary>Tokens of every session of a user that are not revoked yet.</summary>
    Task<IReadOnlyList<RefreshToken>> ListUnrevokedByUserAsync(int userId);
}
