using RoomTrack.Profiles.Domain.Model.Aggregates;
using RoomTrack.Profiles.Domain.Model.ValueObjects;

namespace RoomTrack.Profiles.Domain.Repositories;

public interface IStaffProfileRepository
{
    Task AddAsync(StaffProfile profile);
    Task<StaffProfile?> FindByIdAsync(StaffProfileId id);
    Task<StaffProfile?> FindByUserIdAsync(UserId userId);
    Task<StaffProfile?> FindByEmployeeCodeAsync(EmployeeCode code);
    Task<IEnumerable<StaffProfile>> FindByTargetIdAsync(TargetId targetId);
    Task<bool> ExistsByEmployeeCodeAsync(EmployeeCode code);
    Task<bool> ExistsByUserIdAsync(UserId userId);
    void Update(StaffProfile profile);
    void Remove(StaffProfile profile);
    Task<IEnumerable<StaffProfile>> ListAsync();
}
