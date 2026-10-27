using RoomTrack.Bookings.Domain.Model.Aggregates;

namespace RoomTrack.Bookings.Domain.Repositories;

/// <summary>Digital check-ins.</summary>
public interface IDigitalCheckInRepository
{
    Task AddAsync(DigitalCheckIn checkIn);

    Task<DigitalCheckIn?> FindByBookingIdAsync(int bookingId);
}
