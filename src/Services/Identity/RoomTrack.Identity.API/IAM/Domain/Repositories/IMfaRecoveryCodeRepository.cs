using BackendAwRoomTrack.API.IAM.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Shared.Domain.Repositories;

namespace BackendAwRoomTrack.API.IAM.Domain.Repositories;

/// <summary>One-time recovery codes of the second factor.</summary>
public interface IMfaRecoveryCodeRepository : IBaseRepository<MfaRecoveryCode>
{
    /// <summary>The codes of the user that were not used yet.</summary>
    Task<IReadOnlyList<MfaRecoveryCode>> ListUnusedByUserAsync(int userId);

    /// <summary>Removes every code of the user (new enrollment or reset).</summary>
    Task RemoveAllOfUserAsync(int userId);
}
