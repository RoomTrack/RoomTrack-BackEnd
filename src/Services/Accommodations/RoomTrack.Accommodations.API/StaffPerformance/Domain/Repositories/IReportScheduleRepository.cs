using RoomTrack.StaffPerformance.Domain.Model.Aggregates;

namespace RoomTrack.StaffPerformance.Domain.Repositories;

public interface IReportScheduleRepository
{
    Task AddAsync(ReportSchedule schedule);
    Task<ReportSchedule?> FindByIdAsync(int id);
    Task<IReadOnlyList<ReportSchedule>> ListByHotelAsync(int hotelId);

    /// <summary>The active schedules whose next run is at or before <paramref name="now"/>.</summary>
    Task<IReadOnlyList<ReportSchedule>> ListDueAsync(DateTimeOffset now);

    void Remove(ReportSchedule schedule);
}
