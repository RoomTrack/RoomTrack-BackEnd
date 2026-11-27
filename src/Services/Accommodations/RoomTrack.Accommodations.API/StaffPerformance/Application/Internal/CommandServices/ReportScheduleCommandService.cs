using RoomTrack.Accommodations.Domain.Model.Queries;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.Shared.Domain.Repositories;
using RoomTrack.StaffPerformance.Application.OutboundServices;
using RoomTrack.StaffPerformance.Domain.Model.Aggregates;
using RoomTrack.StaffPerformance.Domain.Model.Commands;
using RoomTrack.StaffPerformance.Domain.Model.Queries;
using RoomTrack.StaffPerformance.Domain.Repositories;
using RoomTrack.StaffPerformance.Domain.Services;

namespace RoomTrack.StaffPerformance.Application.Internal.CommandServices;

/// <summary>
///     US-34: the schedules of the staff reports and their delivery. Each delivery and the next run of its schedule are
///     saved together, so the e-mails (through the outbox) are sent exactly when the schedule moves on.
/// </summary>
public class ReportScheduleCommandService(
    IReportScheduleRepository scheduleRepository,
    IStaffPerformanceQueryService performanceQueryService,
    IHotelQueryService hotelQueryService,
    IStaffReportNotificationService notificationService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ReportScheduleCommandService> logger) : IReportScheduleCommandService
{
    public async Task<ReportSchedule> Handle(CreateReportScheduleCommand command)
    {
        var schedule = ReportSchedule.Create(command.HotelId, command.Settings, command.CreatedByUserId, timeProvider.GetUtcNow());
        await scheduleRepository.AddAsync(schedule);
        await unitOfWork.CompleteAsync();
        return schedule;
    }

    public async Task<ReportSchedule?> Handle(UpdateReportScheduleCommand command)
    {
        var schedule = await scheduleRepository.FindByIdAsync(command.ReportScheduleId);
        if (schedule is null) return null;
        schedule.Update(command.Settings, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return schedule;
    }

    public async Task<bool> Handle(DeleteReportScheduleCommand command)
    {
        var schedule = await scheduleRepository.FindByIdAsync(command.ReportScheduleId);
        if (schedule is null) return false;
        scheduleRepository.Remove(schedule);
        await unitOfWork.CompleteAsync();
        return true;
    }

    public async Task<ReportSchedule?> Handle(SendReportNowCommand command)
    {
        var schedule = await scheduleRepository.FindByIdAsync(command.ReportScheduleId);
        if (schedule is null) return null;
        var (from, to) = schedule.PeriodUntil(timeProvider.GetUtcNow());
        await DeliverAsync(schedule, from, to);
        await unitOfWork.CompleteAsync();
        return schedule;
    }

    public async Task<int> Handle(SendDueReportsCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var sent = 0;
        foreach (var schedule in await scheduleRepository.ListDueAsync(now))
        {
            try
            {
                var (from, to) = schedule.DuePeriod;
                await DeliverAsync(schedule, from, to);
                schedule.MarkSent(now);
                await unitOfWork.CompleteAsync();
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One hotel failing does not stop the others; it is retried on the next run.
                logger.LogError(exception, "Could not send the scheduled report {ReportScheduleId}", schedule.Id);
            }
        }
        return sent;
    }

    private async Task DeliverAsync(ReportSchedule schedule, DateTimeOffset from, DateTimeOffset to)
    {
        var hotel = await hotelQueryService.Handle(new GetHotelByIdQuery(schedule.HotelId));
        var productivity = schedule.IncludeProductivity
            ? await performanceQueryService.Handle(new GetStaffProductivityQuery(schedule.HotelId, from, to))
            : null;
        var serviceTimes = schedule.IncludeServiceTimes
            ? await performanceQueryService.Handle(new GetServiceTimesQuery(schedule.HotelId, from, to))
            : null;
        await notificationService.SendAsync(schedule.Recipients, new StaffReports(hotel?.Name ?? $"Hotel {schedule.HotelId}",
            schedule.Frequency, from, to, productivity, serviceTimes));
    }
}

public class ReportScheduleQueryService(IReportScheduleRepository scheduleRepository) : IReportScheduleQueryService
{
    public Task<ReportSchedule?> FindByIdAsync(int reportScheduleId) => scheduleRepository.FindByIdAsync(reportScheduleId);

    public Task<IReadOnlyList<ReportSchedule>> ListByHotelAsync(int hotelId) => scheduleRepository.ListByHotelAsync(hotelId);
}
