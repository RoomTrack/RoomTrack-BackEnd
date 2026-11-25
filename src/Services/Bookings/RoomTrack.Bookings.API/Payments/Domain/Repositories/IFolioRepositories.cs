using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.ValueObjects;

namespace RoomTrack.Payments.Domain.Repositories;

public interface IGuestFolioRepository
{
    Task AddAsync(GuestFolio folio);

    /// <summary>The folio of the booking with its charges and payments, or null.</summary>
    Task<GuestFolio?> FindByBookingIdAsync(int bookingId);

    /// <summary>The folios of a hotel with activity (charges, payments or deposit) in the period.</summary>
    Task<IReadOnlyList<GuestFolio>> ListWithActivityAsync(int hotelId, DateTimeOffset from, DateTimeOffset to);
}

public interface IReceiptRepository
{
    Task AddAsync(Receipt receipt);

    Task<Receipt?> FindByIdAsync(int id);

    Task<bool> ExistsForSourceAsync(ReceiptSource source, int sourceId);

    Task<IReadOnlyList<Receipt>> ListByBookingIdAsync(int bookingId);

    Task<IReadOnlyList<Receipt>> ListByHotelAsync(int hotelId, DateTimeOffset from, DateTimeOffset to);

    /// <summary>The next correlative number of the series in the hotel (counting the receipts added in this unit of work).</summary>
    Task<int> NextNumberAsync(int hotelId, string series);
}
