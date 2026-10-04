using BackendAwRoomTrack.API.Analytics.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Analytics.Domain.Model.ReadModels;
using BackendAwRoomTrack.API.Analytics.Domain.Repositories;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace BackendAwRoomTrack.API.Analytics.Infrastructure.Persistence.EFC.Repositories;

/// <summary>
/// Implementation of the analytics repository on the service's own read model (rooms, bookings and payments
/// replicated from the Accommodations and Bookings services through their integration events).
/// </summary>
/// <remarks>Eventually consistent: a change shows up in the metrics once its event has been consumed.</remarks>
public class AnalyticsRepository(AppDbContext context) : IAnalyticsRepository
{
    private const string Completed = "Completed";
    private const string Cancelled = "Cancelled";
    private const string Confirmed = "Confirmed";
    private const string CheckedIn = "CheckedIn";

    public async Task<PerformanceMetrics> GetMonthlyMetricsAsync(int? hotelId)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        // 1. Revenue: completed payments of the month
        var payments = context.Set<PaymentFact>().AsNoTracking();
        if (hotelId is not null) payments = payments.Where(p => p.HotelId == hotelId);
        var totalRevenue = await payments
            .Where(p => p.PaymentDate >= startOfMonth && p.PaymentDate <= endOfMonth && p.Status == Completed)
            .SumAsync(p => p.Amount);

        // 2. Booking Stats
        var bookingsQuery = context.Set<BookingFact>().AsNoTracking()
            .Where(b => b.CheckInDate >= startOfMonth && b.CheckInDate <= endOfMonth);
        if (hotelId is not null) bookingsQuery = bookingsQuery.Where(b => b.HotelId == hotelId);

        var totalBookings = await bookingsQuery.CountAsync();
        var cancelledBookings = await bookingsQuery.CountAsync(b => b.Status == Cancelled);

        // 3. Occupancy Rate (Simplified logic: Booked Rooms / Total Rooms * 100)
        // Note: For a real rigorous calculation, we'd check day-by-day availability.
        var rooms = context.Set<RoomFact>().AsNoTracking().Where(r => !r.Removed);
        if (hotelId is not null) rooms = rooms.Where(r => r.HotelId == hotelId);
        var totalRooms = await rooms.CountAsync();

        double occupancyRate = 0;
        if (totalRooms > 0 && totalBookings > 0)
        {
            // Simple heuristic: (Confirmed Bookings / Total Rooms) * 100
            // This is a snapshot, a real system would calculate room-nights.
            var activeBookings = await bookingsQuery.CountAsync(b => b.Status == Confirmed || b.Status == CheckedIn);
            occupancyRate = ((double)activeBookings / totalRooms) * 100;
        }

        return new PerformanceMetrics
        {
            TotalRevenue = totalRevenue,
            TotalBookings = totalBookings,
            CancelledBookings = cancelledBookings,
            OccupancyRate = Math.Round(occupancyRate, 2)
        };
    }
}
