using RoomTrack.IAM.Domain.Model.Aggregates;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.IAM.Domain.Repositories;

/// <summary>One-time recovery codes of the second factor.</summary>
public interface IMfaRecoveryCodeRepository : IBaseRepository<MfaRecoveryCode>
{
    /// <summary>The codes of the user that were not used yet.</summary>
    Task<IReadOnlyList<MfaRecoveryCode>> ListUnusedByUserAsync(int userId);

    /// <summary>Removes every code of the user (new enrollment or reset).</summary>
    Task RemoveAllOfUserAsync(int userId);
}
