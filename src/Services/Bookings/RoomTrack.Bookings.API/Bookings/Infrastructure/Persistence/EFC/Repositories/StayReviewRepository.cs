using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Bookings.Infrastructure.Persistence.EFC.Repositories;

public class StayReviewRepository(AppDbContext context) : IStayReviewRepository
{
    public async Task AddAsync(StayReview review) => await context.Set<StayReview>().AddAsync(review);

    public Task<StayReview?> FindByBookingIdAsync(int bookingId) =>
        context.Set<StayReview>().FirstOrDefaultAsync(review => review.BookingId == bookingId);

    public async Task<IReadOnlyList<StayReview>> ListAsync(int hotelId, DateTimeOffset from, DateTimeOffset to, int? limit = null)
    {
        var reviews = context.Set<StayReview>().AsNoTracking()
            .Where(review => review.HotelId == hotelId && review.SubmittedAt >= from && review.SubmittedAt < to)
            .OrderByDescending(review => review.SubmittedAt).ThenByDescending(review => review.Id);
        return limit is { } max ? await reviews.Take(max).ToListAsync() : await reviews.ToListAsync();
    }
}
