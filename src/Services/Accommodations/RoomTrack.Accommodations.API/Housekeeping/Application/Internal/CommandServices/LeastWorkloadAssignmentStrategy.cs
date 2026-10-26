using RoomTrack.Housekeeping.Domain.Repositories;
using RoomTrack.Housekeeping.Domain.Services;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.ACL;

namespace RoomTrack.Housekeeping.Application.Internal.CommandServices;

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
        // A reassignment moves the task to someone else (US-05): the current assignee is never chosen again, so with
        // nobody else available there is no candidate instead of a silent "reassignment" to the same person.
        var candidates = (await iamContextFacade.ListHotelStaffAsync(hotelId, [UserRoles.Housekeeping]))
            .Select(contact => contact.UserId).Distinct()
            .Where(userId => userId != excludingUserId)
            .ToList();
        if (candidates.Count == 0) return null;

        var openTasks = await cleaningTaskRepository.CountOpenByAssigneeAsync(hotelId);
        return candidates
            .OrderBy(userId => openTasks.GetValueOrDefault(userId))
            .ThenBy(userId => userId)
            .First();
    }
}
