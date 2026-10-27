using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Events;

namespace RoomTrack.Housekeeping.Domain.Model.Events;

/// <summary>
///     A cleaning task was assigned (or reassigned) to a housekeeping staff member. <paramref name="AssignedByUserId"/>
///     is null when the system assigned it (automatic assignment).
/// </summary>
public sealed record CleaningTaskAssignedEvent(int CleaningTaskId, int HotelId, int RoomId, int AssigneeUserId,
    int? PreviousAssigneeUserId, int? AssignedByUserId, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>A cleaning task was completed: its room is Available again.</summary>
public sealed record CleaningTaskCompletedEvent(int CleaningTaskId, int HotelId, int RoomId, int CompletedByUserId,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>
///     The urgency of an open cleaning task changed (US-22: the assignee is notified, highlighted when it became
///     urgent). <paramref name="AssigneeUserId"/> is null when nobody has the task yet.
/// </summary>
public sealed record CleaningTaskPriorityChangedEvent(int CleaningTaskId, int HotelId, int RoomId, int? AssigneeUserId,
    CleaningTaskPriority FromPriority, CleaningTaskPriority ToPriority, int ChangedByUserId, DateTimeOffset OccurredOn)
    : DomainEvent(OccurredOn);
