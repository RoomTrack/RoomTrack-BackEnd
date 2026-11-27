using RoomTrack.StaffPerformance.Domain.Model.Aggregates;

namespace RoomTrack.StaffPerformance.Domain.Model.Commands;

/// <summary>US-34: an administrator schedules the delivery of the staff reports of their hotel by e-mail.</summary>
public record CreateReportScheduleCommand(int HotelId, ReportScheduleSettings Settings, int CreatedByUserId);

public record UpdateReportScheduleCommand(int ReportScheduleId, ReportScheduleSettings Settings);

public record DeleteReportScheduleCommand(int ReportScheduleId);

/// <summary>Sends the report of a schedule right away, with the last week or month until now (its next run does not change).</summary>
public record SendReportNowCommand(int ReportScheduleId);

/// <summary>Scheduled job: sends every report that is due and moves each schedule to its next run.</summary>
public record SendDueReportsCommand;
