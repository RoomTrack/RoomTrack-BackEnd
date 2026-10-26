using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Bookings.Infrastructure.Persistence.EFC.Repositories;

public class DigitalCheckInRepository(AppDbContext context) : IDigitalCheckInRepository
{
    public async Task AddAsync(DigitalCheckIn checkIn) => await context.Set<DigitalCheckIn>().AddAsync(checkIn);

    public Task<DigitalCheckIn?> FindByBookingIdAsync(int bookingId) =>
        context.Set<DigitalCheckIn>().FirstOrDefaultAsync(checkIn => checkIn.BookingId == bookingId);
}
