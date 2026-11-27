namespace RoomTrack.StaffPerformance.Domain.Model.ValueObjects;

/// <summary>The operational area a finished piece of work belongs to.</summary>
public enum WorkArea
{
    FrontDesk,
    Housekeeping,
    Maintenance
}

/// <summary>What was finished.</summary>
public enum WorkKind
{
    /// <summary>A completed cleaning task (Housekeeping).</summary>
    CleaningTask,
    /// <summary>A resolved incident (Maintenance).</summary>
    Incident,
    /// <summary>A resolved guest request (the area it was routed to).</summary>
    GuestRequest
}

/// <summary>
///     A cleaning task completed, an incident resolved or a guest request resolved: the read model both reports are
///     calculated from. It is a projection of the Housekeeping, Incidents and GuestRequests aggregates.
/// </summary>
/// <param name="Kind">What was finished.</param>
/// <param name="ReferenceId">Its id in its own context.</param>
/// <param name="Area">The area that did it.</param>
/// <param name="UserId">Who finished it (null when nobody is recorded).</param>
/// <param name="CreatedAt">When it was created or reported: the clock of the guest or of the room starts here.</param>
/// <param name="StartedAt">When someone started working on it (null when it was finished without being started).</param>
/// <param name="FinishedAt">When it was completed or resolved.</param>
/// <param name="CountsForServiceTimes">
///     False for the preventive maintenance tasks: they are generated ahead of their due date, so their waiting time
///     does not measure how fast the hotel attends. They still count as work done.
/// </param>
public sealed record FinishedWork(
    WorkKind Kind,
    int ReferenceId,
    WorkArea Area,
    int? UserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset FinishedAt,
    bool CountsForServiceTimes = true)
{
    /// <summary>From created or reported until someone started it.</summary>
    public TimeSpan? ResponseTime => StartedAt is { } started ? NonNegative(started - CreatedAt) : null;

    /// <summary>From started until finished: how long the work itself took.</summary>
    public TimeSpan? WorkTime => StartedAt is { } started ? NonNegative(FinishedAt - started) : null;

    /// <summary>From created or reported until finished: what the guest or the room waited.</summary>
    public TimeSpan TotalTime => NonNegative(FinishedAt - CreatedAt);

    private static TimeSpan NonNegative(TimeSpan span) => span < TimeSpan.Zero ? TimeSpan.Zero : span;
}

/// <summary>A staff member as the Identity service knows them.</summary>
public sealed record StaffMember(int UserId, string? FullName, string? Email, string Role);

/// <summary>The work a staff member finished in the period (US-24).</summary>
/// <param name="UserId">The staff member.</param>
/// <param name="FullName">Their name (null when Identity does not know them any more).</param>
/// <param name="Email">Their e-mail.</param>
/// <param name="Role">Their role (null when Identity does not know them any more).</param>
/// <param name="CleaningTasksCompleted">Cleaning tasks they completed.</param>
/// <param name="IncidentsResolved">Incidents they resolved (corrective and preventive).</param>
/// <param name="GuestRequestsResolved">Guest requests they resolved.</param>
/// <param name="AverageWorkMinutes">Average minutes from starting to finishing their work (null when none was started).</param>
public sealed record CollaboratorProductivity(
    int UserId,
    string? FullName,
    string? Email,
    string? Role,
    int CleaningTasksCompleted,
    int IncidentsResolved,
    int GuestRequestsResolved,
    double? AverageWorkMinutes)
{
    public int TotalCompleted => CleaningTasksCompleted + IncidentsResolved + GuestRequestsResolved;
}

/// <summary>The work an area finished in the period, to compare the areas (US-24).</summary>
/// <param name="Area">The area.</param>
/// <param name="Completed">Work finished by the area.</param>
/// <param name="Collaborators">Staff members who finished at least one piece of work of the area.</param>
/// <param name="AveragePerCollaborator">Completed over collaborators (0 without collaborators).</param>
/// <param name="AverageWorkMinutes">Average minutes from starting to finishing (null when none was started).</param>
public sealed record AreaProductivity(
    WorkArea Area,
    int Completed,
    int Collaborators,
    double AveragePerCollaborator,
    double? AverageWorkMinutes);

/// <summary>US-24: the work finished in a hotel in a period, by staff member and by area.</summary>
public sealed record ProductivityReport(
    int HotelId,
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalCompleted,
    IReadOnlyList<CollaboratorProductivity> Collaborators,
    IReadOnlyList<AreaProductivity> Areas);

/// <summary>How long a kind of work takes (US-25). The averages are null when there is nothing to average.</summary>
/// <param name="Count">Pieces of work measured.</param>
/// <param name="AverageResponseMinutes">From created or reported until started.</param>
/// <param name="AverageWorkMinutes">From started until finished.</param>
/// <param name="AverageTotalMinutes">From created or reported until finished.</param>
/// <param name="LongestTotalMinutes">The longest total time.</param>
public sealed record ServiceTimeIndicator(
    int Count,
    double? AverageResponseMinutes,
    double? AverageWorkMinutes,
    double? AverageTotalMinutes,
    double? LongestTotalMinutes);

/// <summary>The service times of the guest requests of one area.</summary>
public sealed record AreaServiceTime(WorkArea Area, ServiceTimeIndicator Indicator);

/// <summary>US-25: how long it takes on average to clean a room, resolve an incident and attend a guest request.</summary>
public sealed record ServiceTimesReport(
    int HotelId,
    DateTimeOffset From,
    DateTimeOffset To,
    ServiceTimeIndicator Cleaning,
    ServiceTimeIndicator Incidents,
    ServiceTimeIndicator GuestRequests,
    IReadOnlyList<AreaServiceTime> GuestRequestsByArea);
