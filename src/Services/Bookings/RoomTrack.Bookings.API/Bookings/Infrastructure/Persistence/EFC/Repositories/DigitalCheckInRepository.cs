using BackendAwRoomTrack.API.Bookings.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Bookings.Domain.Repositories;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace BackendAwRoomTrack.API.Bookings.Infrastructure.Persistence.EFC.Repositories;

public class DigitalCheckInRepository(AppDbContext context) : IDigitalCheckInRepository
{
    public async Task AddAsync(DigitalCheckIn checkIn) => await context.Set<DigitalCheckIn>().AddAsync(checkIn);

    public Task<DigitalCheckIn?> FindByBookingIdAsync(int bookingId) =>
        context.Set<DigitalCheckIn>().FirstOrDefaultAsync(checkIn => checkIn.BookingId == bookingId);
}
