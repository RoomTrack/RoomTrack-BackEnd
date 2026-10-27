using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Repositories;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Bookings.Infrastructure.Persistence.EFC.Repositories;

public class BookingRepository(AppDbContext context) : BaseRepository<Booking>(context), IBookingRepository
{
    // Same statuses as BookingStatusExtensions.IsActive, in a form EF Core translates to SQL.
    private static readonly BookingStatus[] ActiveStatuses =
        [BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    public async Task<IEnumerable<Booking>> FindByOwnerAsync(int userId, Guid? guestProfileId) =>
        await OwnedBy(userId, guestProfileId).OrderByDescending(b => b.Id).ToListAsync();

    public async Task<IEnumerable<Booking>> ListNewestFirstAsync(int? hotelId) =>
        await OfHotel(hotelId).OrderByDescending(b => b.Id).ToListAsync();

    public Task<BookingPage> FindPageByOwnerAsync(int userId, Guid? guestProfileId, PageRequest page) =>
        PageNewestFirstAsync(OwnedBy(userId, guestProfileId), page);

    public Task<BookingPage> ListPageNewestFirstAsync(int? hotelId, PageRequest page) =>
        PageNewestFirstAsync(OfHotel(hotelId), page);

    private IQueryable<Booking> OwnedBy(int userId, Guid? guestProfileId)
    {
        var guestId = new GuestId(userId);
        return Context.Set<Booking>()
            .Where(b => b.GuestId == guestId || (guestProfileId != null && b.GuestProfileId == guestProfileId));
    }

    private IQueryable<Booking> OfHotel(int? hotelId) =>
        Context.Set<Booking>().Where(b => hotelId == null || b.HotelId == hotelId);

    private static async Task<BookingPage> PageNewestFirstAsync(IQueryable<Booking> bookings, PageRequest page)
    {
        var total = await bookings.CountAsync();
        var items = await bookings.OrderByDescending(b => b.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync();
        return new BookingPage(items, total);
    }

    public async Task<IEnumerable<Booking>> FindByRoomIdAsync(int roomId)
    {
        return await Context.Set<Booking>()
            .Where(b => b.RoomId == roomId)
            .OrderBy(b => b.CheckInDate)
            .ToListAsync();
    }

    public async Task<IReadOnlySet<int>> FindRoomIdsWithActiveBookingOverlappingAsync(IReadOnlyCollection<int> roomIds, DateRange dates)
    {
        var booked = await Context.Set<Booking>()
            .Where(b => roomIds.Contains(b.RoomId)
                        && ActiveStatuses.Contains(b.Status)
                        && b.CheckInDate < dates.CheckOut
                        && dates.CheckIn < b.CheckOutDate)
            .Select(b => b.RoomId)
            .Distinct()
            .ToListAsync();
        return booked.ToHashSet();
    }

    public async Task<bool> ExistsActiveBookingOverlappingAsync(int roomId, DateRange dates, int? excludingBookingId = null)
    {
        // Same predicate as DateRange.Overlaps, translated to SQL. Stored dates are calendar dates.
        return await Context.Set<Booking>().AnyAsync(b =>
            b.RoomId == roomId
            && (excludingBookingId == null || b.Id != excludingBookingId)
            && ActiveStatuses.Contains(b.Status)
            && b.CheckInDate < dates.CheckOut
            && dates.CheckIn < b.CheckOutDate);
    }

    public async Task<IReadOnlyList<Booking>> ListActiveOverlappingAsync(int? hotelId, DateRange window) =>
        await Context.Set<Booking>()
            .Where(b => (hotelId == null || b.HotelId == hotelId)
                        && ActiveStatuses.Contains(b.Status)
                        && b.CheckInDate < window.CheckOut
                        && window.CheckIn < b.CheckOutDate)
            .OrderBy(b => b.CheckInDate).ThenBy(b => b.RoomId)
            .ToListAsync();

    public async Task<IReadOnlyList<Booking>> ListPendingPaymentDueAsync(DateTimeOffset now) =>
        await Context.Set<Booking>()
            .Where(b => b.Status == BookingStatus.Pending && b.PaymentDueAt != null && b.PaymentDueAt <= now)
            .ToListAsync();

    public async Task<IReadOnlyDictionary<int, int>> CountActiveByRoomAsync(IReadOnlyCollection<int> roomIds) =>
        await Context.Set<Booking>()
            .Where(b => roomIds.Contains(b.RoomId) && ActiveStatuses.Contains(b.Status))
            .GroupBy(b => b.RoomId)
            .Select(group => new { RoomId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.RoomId, row => row.Count);
}
