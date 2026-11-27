using RoomTrack.StaffPerformance.Domain.Model.ValueObjects;
using RoomTrack.StaffPerformance.Interfaces.REST.Resources;

namespace RoomTrack.StaffPerformance.Interfaces.REST.Transform;

public static class StaffPerformanceResourceAssembler
{
    public static ProductivityReportResource ToResource(ProductivityReport report) => new(
        report.HotelId,
        report.From,
        report.To,
        report.TotalCompleted,
        report.Collaborators.Select(c => new CollaboratorProductivityResource(c.UserId, c.FullName, c.Email, c.Role,
            c.CleaningTasksCompleted, c.IncidentsResolved, c.GuestRequestsResolved, c.TotalCompleted, c.AverageWorkMinutes)).ToList(),
        report.Areas.Select(a => new AreaProductivityResource(a.Area.ToString(), a.Completed, a.Collaborators,
            a.AveragePerCollaborator, a.AverageWorkMinutes)).ToList());

    public static ServiceTimesReportResource ToResource(ServiceTimesReport report) => new(
        report.HotelId,
        report.From,
        report.To,
        ToResource(report.Cleaning),
        ToResource(report.Incidents),
        ToResource(report.GuestRequests),
        report.GuestRequestsByArea.Select(a => new AreaServiceTimeResource(a.Area.ToString(), a.Indicator.Count,
            a.Indicator.AverageResponseMinutes, a.Indicator.AverageWorkMinutes, a.Indicator.AverageTotalMinutes,
            a.Indicator.LongestTotalMinutes)).ToList());

    private static ServiceTimeIndicatorResource ToResource(ServiceTimeIndicator indicator) => new(indicator.Count,
        indicator.AverageResponseMinutes, indicator.AverageWorkMinutes, indicator.AverageTotalMinutes,
        indicator.LongestTotalMinutes);
}
