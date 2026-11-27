using Microsoft.EntityFrameworkCore;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using RoomTrack.StaffPerformance.Domain.Model.Aggregates;
using RoomTrack.StaffPerformance.Domain.Repositories;

namespace RoomTrack.StaffPerformance.Infrastructure.Persistence.EFC;

public static class ModelBuilderExtensions
{
    /// <summary>Tables of the StaffPerformance context: the schedules of the staff reports (US-34).</summary>
    public static void ApplyStaffPerformanceConfiguration(this ModelBuilder builder)
    {
        builder.Entity<ReportSchedule>().ToTable("report_schedules");
        builder.Entity<ReportSchedule>().HasKey(s => s.Id);
        builder.Entity<ReportSchedule>().Property(s => s.Id).ValueGeneratedOnAdd();
        builder.Entity<ReportSchedule>().Property(s => s.Frequency).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<ReportSchedule>().Property(s => s.RecipientList)
            .HasMaxLength((ReportSchedule.MaxEmailLength + 1) * ReportSchedule.MaxRecipients).IsRequired();
        builder.Entity<ReportSchedule>().Ignore(s => s.Recipients);
        builder.Entity<ReportSchedule>().Ignore(s => s.DuePeriod);
        builder.Entity<ReportSchedule>().HasIndex(s => s.HotelId);
        builder.Entity<ReportSchedule>().HasIndex(s => new { s.IsActive, s.NextRunAt });
    }
}

public class ReportScheduleRepository(AppDbContext context) : IReportScheduleRepository
{
    public async Task AddAsync(ReportSchedule schedule) => await context.Set<ReportSchedule>().AddAsync(schedule);

    public Task<ReportSchedule?> FindByIdAsync(int id) =>
        context.Set<ReportSchedule>().FirstOrDefaultAsync(schedule => schedule.Id == id);

    public async Task<IReadOnlyList<ReportSchedule>> ListByHotelAsync(int hotelId) =>
        await context.Set<ReportSchedule>().Where(schedule => schedule.HotelId == hotelId)
            .OrderBy(schedule => schedule.Id).ToListAsync();

    public async Task<IReadOnlyList<ReportSchedule>> ListDueAsync(DateTimeOffset now) =>
        await context.Set<ReportSchedule>().Where(schedule => schedule.IsActive && schedule.NextRunAt <= now)
            .OrderBy(schedule => schedule.NextRunAt).ToListAsync();

    public void Remove(ReportSchedule schedule) => context.Set<ReportSchedule>().Remove(schedule);
}
