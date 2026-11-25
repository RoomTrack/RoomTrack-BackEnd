namespace RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;

/// <summary>The operational area whose shifts hand over to each other.</summary>
public enum ShiftArea
{
    FrontDesk,
    Housekeeping,
    Maintenance
}

/// <summary>What an item of a handover refers to.</summary>
public enum ShiftHandoverItemKind
{
    CleaningTask,
    Incident
}

/// <summary>Who writes or reads a handover.</summary>
/// <param name="UserId">IAM user id.</param>
/// <param name="Email">Their e-mail (kept with the handover).</param>
/// <param name="Area">The area of their role (null for an administrator, who names it).</param>
public sealed record ShiftHandoverActor(int UserId, string? Email, ShiftArea? Area);

/// <summary>A task or incident open at the end of the shift, as it was then.</summary>
public sealed record ShiftHandoverItemSnapshot(ShiftHandoverItemKind Kind, int ReferenceId, int RoomId, string Summary,
    string Status, string Priority, int? AssigneeUserId);
