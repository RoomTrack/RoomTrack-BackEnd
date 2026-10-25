using BackendAwRoomTrack.Domain.Shared.Domain.Model.Events;

namespace BackendAwRoomTrack.API.Housekeeping.Domain.Model.Events;

/// <summary>
///     A cleaning task was assigned (or reassigned) to a housekeeping staff member. <paramref name="AssignedByUserId"/>
///     is null when the system assigned it (automatic assignment).
/// </summary>
public sealed record CleaningTaskAssignedEvent(int CleaningTaskId, int HotelId, int RoomId, int AssigneeUserId,
    int? PreviousAssigneeUserId, int? AssignedByUserId, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>A cleaning task was completed: its room is Available again.</summary>
public sealed record CleaningTaskCompletedEvent(int CleaningTaskId, int HotelId, int RoomId, int CompletedByUserId,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);
