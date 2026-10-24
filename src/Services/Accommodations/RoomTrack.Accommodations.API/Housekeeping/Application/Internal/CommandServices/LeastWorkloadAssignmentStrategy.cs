using BackendAwRoomTrack.API.Housekeeping.Domain.Repositories;
using BackendAwRoomTrack.API.Housekeeping.Domain.Services;
using BackendAwRoomTrack.API.IAM.Domain.Model.Constants;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;

namespace BackendAwRoomTrack.API.Housekeeping.Application.Internal.CommandServices;

/// <summary>
///     Automatic assignment of US-05: the active housekeeping staff member of the hotel with the fewest open
///     (Pending or InProgress) cleaning tasks; ties go to the lowest user id so the choice is deterministic.
/// </summary>
public class LeastWorkloadAssignmentStrategy(
    ICleaningTaskRepository cleaningTaskRepository,
    IIamContextFacade iamContextFacade) : ICleaningTaskAssignmentStrategy
{
    public async Task<int?> SelectAssigneeAsync(int hotelId, int? excludingUserId = null)
    {
        var candidates = (await iamContextFacade.ListHotelStaffAsync(hotelId, [UserRoles.Housekeeping]))
            .Select(contact => contact.UserId).Distinct().ToList();
        if (excludingUserId is { } excluded && candidates.Any(userId => userId != excluded))
            candidates.Remove(excluded);
        if (candidates.Count == 0) return null;

        var openTasks = await cleaningTaskRepository.CountOpenByAssigneeAsync(hotelId);
        return candidates
            .OrderBy(userId => openTasks.GetValueOrDefault(userId))
            .ThenBy(userId => userId)
            .First();
    }
}
