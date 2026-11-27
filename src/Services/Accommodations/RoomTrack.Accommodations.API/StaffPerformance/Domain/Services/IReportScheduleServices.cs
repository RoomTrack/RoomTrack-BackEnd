using RoomTrack.StaffPerformance.Domain.Model.Aggregates;
using RoomTrack.StaffPerformance.Domain.Model.Commands;

namespace RoomTrack.StaffPerformance.Domain.Services;

public interface IReportScheduleCommandService
{
    Task<ReportSchedule> Handle(CreateReportScheduleCommand command);

    /// <returns>The schedule, or null when it does not exist.</returns>
    Task<ReportSchedule?> Handle(UpdateReportScheduleCommand command);

    /// <returns>False when it does not exist.</returns>
    Task<bool> Handle(DeleteReportScheduleCommand command);

    /// <returns>The schedule, or null when it does not exist.</returns>
    Task<ReportSchedule?> Handle(SendReportNowCommand command);

    /// <returns>How many reports were sent.</returns>
    Task<int> Handle(SendDueReportsCommand command);
}

public interface IReportScheduleQueryService
{
    Task<ReportSchedule?> FindByIdAsync(int reportScheduleId);
    Task<IReadOnlyList<ReportSchedule>> ListByHotelAsync(int hotelId);
}
