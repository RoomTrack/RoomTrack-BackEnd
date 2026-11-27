using RoomTrack.Bookings.Domain.Model.Aggregates;

namespace RoomTrack.Bookings.Domain.Repositories;

public interface IStayReviewRepository
{
    Task AddAsync(StayReview review);

    Task<StayReview?> FindByBookingIdAsync(int bookingId);

    /// <summary>
    ///     The evaluations of the hotel submitted in [<paramref name="from"/>, <paramref name="to"/>), newest first;
    ///     at most <paramref name="limit"/> (all of them when null).
    /// </summary>
    Task<IReadOnlyList<StayReview>> ListAsync(int hotelId, DateTimeOffset from, DateTimeOffset to, int? limit = null);
}
