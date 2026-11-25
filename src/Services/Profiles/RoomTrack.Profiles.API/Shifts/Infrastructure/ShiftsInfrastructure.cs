using Microsoft.EntityFrameworkCore;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using RoomTrack.Shifts.Application;
using RoomTrack.Shifts.Domain;

namespace RoomTrack.Shifts.Infrastructure;

public static class ShiftsModelBuilderExtensions
{
    /// <summary>Tables of the staff shifts (US-31). Users are logical references (IAM ids).</summary>
    public static void ApplyShiftsConfiguration(this ModelBuilder builder)
    {
        builder.Entity<WorkShift>().ToTable("work_shifts");
        builder.Entity<WorkShift>().HasKey(s => s.Id);
        builder.Entity<WorkShift>().Property(s => s.Id).ValueGeneratedOnAdd();
        builder.Entity<WorkShift>().Property(s => s.Period).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<WorkShift>().Property(s => s.Area).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<WorkShift>().HasIndex(s => new { s.HotelId, s.Date, s.Period, s.Area }).IsUnique();
        builder.Entity<WorkShift>().Ignore(s => s.CoverageGap);
        builder.Entity<WorkShift>().Ignore(s => s.IsUnderstaffed);
        builder.Entity<WorkShift>().HasMany(s => s.Assignees).WithOne().HasForeignKey(a => a.WorkShiftId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<WorkShift>().Navigation(s => s.Assignees).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_assignees");

        builder.Entity<ShiftAssignee>().ToTable("work_shift_assignees");
        builder.Entity<ShiftAssignee>().HasKey(a => a.Id);
        builder.Entity<ShiftAssignee>().Property(a => a.Id).ValueGeneratedOnAdd();
        builder.Entity<ShiftAssignee>().Property(a => a.DisplayName).HasMaxLength(ShiftAssignee.MaxDisplayNameLength).IsRequired();
        builder.Entity<ShiftAssignee>().HasIndex(a => new { a.WorkShiftId, a.UserId }).IsUnique();
        builder.Entity<ShiftAssignee>().HasIndex(a => a.UserId);

        builder.Entity<ShiftSwapRequest>().ToTable("shift_swap_requests");
        builder.Entity<ShiftSwapRequest>().HasKey(r => r.Id);
        builder.Entity<ShiftSwapRequest>().Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Entity<ShiftSwapRequest>().Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<ShiftSwapRequest>().Property(r => r.Reason).HasMaxLength(ShiftSwapRequest.MaxReasonLength);
        builder.Entity<ShiftSwapRequest>().HasIndex(r => new { r.HotelId, r.Status });
    }
}

public class WorkShiftRepository(AppDbContext context) : IWorkShiftRepository
{
    public async Task AddAsync(WorkShift shift) => await context.Set<WorkShift>().AddAsync(shift);

    public Task<WorkShift?> FindByIdAsync(int id) =>
        context.Set<WorkShift>().Include(s => s.Assignees).FirstOrDefaultAsync(s => s.Id == id);

    public Task<WorkShift?> FindAsync(int hotelId, DateOnly date, ShiftPeriod period, ShiftArea area) =>
        context.Set<WorkShift>().Include(s => s.Assignees)
            .FirstOrDefaultAsync(s => s.HotelId == hotelId && s.Date == date && s.Period == period && s.Area == area);

    public async Task<IReadOnlyList<WorkShift>> ListAsync(int hotelId, DateOnly from, DateOnly to, ShiftArea? area)
    {
        var shifts = context.Set<WorkShift>().Include(s => s.Assignees)
            .Where(s => s.HotelId == hotelId && s.Date >= from && s.Date <= to);
        if (area is { } value) shifts = shifts.Where(s => s.Area == value);
        return await shifts.OrderBy(s => s.Date).ThenBy(s => s.Period).ThenBy(s => s.Area).ToListAsync();
    }

    public async Task<IReadOnlyList<WorkShift>> ListOfUserAsync(int userId, DateOnly from, DateOnly to) =>
        await context.Set<WorkShift>().Include(s => s.Assignees)
            .Where(s => s.Date >= from && s.Date <= to && s.Assignees.Any(a => a.UserId == userId))
            .OrderBy(s => s.Date).ThenBy(s => s.Period)
            .ToListAsync();
}

public class ShiftSwapRequestRepository(AppDbContext context) : IShiftSwapRequestRepository
{
    public async Task AddAsync(ShiftSwapRequest request) => await context.Set<ShiftSwapRequest>().AddAsync(request);

    public Task<ShiftSwapRequest?> FindByIdAsync(int id) => context.Set<ShiftSwapRequest>().FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IReadOnlyList<ShiftSwapRequest>> ListAsync(int hotelId, ShiftSwapStatus? status, int? userId)
    {
        var requests = context.Set<ShiftSwapRequest>().Where(r => r.HotelId == hotelId);
        if (status is { } value) requests = requests.Where(r => r.Status == value);
        if (userId is { } user) requests = requests.Where(r => r.RequesterUserId == user || r.ColleagueUserId == user);
        return await requests.OrderByDescending(r => r.RequestedAt).ToListAsync();
    }
}

public static class ShiftsServiceCollectionExtensions
{
    /// <summary>Shifts module of the Profiles service (US-31).</summary>
    public static void AddShiftsServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IWorkShiftRepository, WorkShiftRepository>();
        builder.Services.AddScoped<IShiftSwapRequestRepository, ShiftSwapRequestRepository>();
        builder.Services.AddScoped<ShiftService>();
    }
}
