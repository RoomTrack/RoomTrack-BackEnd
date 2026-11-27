namespace RoomTrack.StaffPerformance.Interfaces.REST.Resources;

/// <summary>The work a staff member finished in the period (US-24).</summary>
/// <param name="UserId">The staff member.</param>
/// <param name="FullName">Their name (null when Identity does not know them any more).</param>
/// <param name="Email">Their e-mail.</param>
/// <param name="Role">Their role: Reception, Housekeeping, Maintenance, Admin or ChainAdmin.</param>
/// <param name="CleaningTasksCompleted">Cleaning tasks they completed.</param>
/// <param name="IncidentsResolved">Incidents they resolved (corrective and preventive).</param>
/// <param name="GuestRequestsResolved">Guest requests they resolved.</param>
/// <param name="TotalCompleted">All of the above.</param>
/// <param name="AverageWorkMinutes">Average minutes from starting to finishing their work.</param>
public record CollaboratorProductivityResource(
    int UserId,
    string? FullName,
    string? Email,
    string? Role,
    int CleaningTasksCompleted,
    int IncidentsResolved,
    int GuestRequestsResolved,
    int TotalCompleted,
    double? AverageWorkMinutes);

/// <summary>The work an area finished in the period (US-24).</summary>
/// <param name="Area">FrontDesk, Housekeeping or Maintenance.</param>
/// <param name="Completed">Work finished by the area.</param>
/// <param name="Collaborators">Staff members who finished work of the area.</param>
/// <param name="AveragePerCollaborator">Completed over collaborators.</param>
/// <param name="AverageWorkMinutes">Average minutes from starting to finishing.</param>
public record AreaProductivityResource(
    string Area,
    int Completed,
    int Collaborators,
    double AveragePerCollaborator,
    double? AverageWorkMinutes);

/// <summary>US-24: productivity of the staff of a hotel.</summary>
/// <param name="HotelId">The hotel.</param>
/// <param name="From">Start of the period (inclusive).</param>
/// <param name="To">End of the period (exclusive).</param>
/// <param name="TotalCompleted">Work finished in the hotel.</param>
/// <param name="Collaborators">By staff member, most productive first.</param>
/// <param name="Areas">By area: FrontDesk, Housekeeping and Maintenance, always the three.</param>
public record ProductivityReportResource(
    int HotelId,
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalCompleted,
    IReadOnlyList<CollaboratorProductivityResource> Collaborators,
    IReadOnlyList<AreaProductivityResource> Areas);

/// <summary>How long a kind of work takes (US-25), in minutes. Averages are null when nothing was measured.</summary>
/// <param name="Count">Pieces of work measured.</param>
/// <param name="AverageResponseMinutes">From created or reported until someone started it.</param>
/// <param name="AverageWorkMinutes">From started until finished: the work itself.</param>
/// <param name="AverageTotalMinutes">From created or reported until finished: what the guest or the room waited.</param>
/// <param name="LongestTotalMinutes">The longest total time.</param>
public record ServiceTimeIndicatorResource(
    int Count,
    double? AverageResponseMinutes,
    double? AverageWorkMinutes,
    double? AverageTotalMinutes,
    double? LongestTotalMinutes);

/// <summary>The service times of the guest requests of one area.</summary>
public record AreaServiceTimeResource(
    string Area,
    int Count,
    double? AverageResponseMinutes,
    double? AverageWorkMinutes,
    double? AverageTotalMinutes,
    double? LongestTotalMinutes);

/// <summary>US-25: service-time indicators of a hotel.</summary>
/// <param name="HotelId">The hotel.</param>
/// <param name="From">Start of the period (inclusive).</param>
/// <param name="To">End of the period (exclusive).</param>
/// <param name="Cleaning">Cleaning of the rooms.</param>
/// <param name="Incidents">Corrective incidents (preventive maintenance is generated ahead of time and not measured).</param>
/// <param name="GuestRequests">Requests of the guests, all areas.</param>
/// <param name="GuestRequestsByArea">Requests of the guests by area.</param>
public record ServiceTimesReportResource(
    int HotelId,
    DateTimeOffset From,
    DateTimeOffset To,
    ServiceTimeIndicatorResource Cleaning,
    ServiceTimeIndicatorResource Incidents,
    ServiceTimeIndicatorResource GuestRequests,
    IReadOnlyList<AreaServiceTimeResource> GuestRequestsByArea);
