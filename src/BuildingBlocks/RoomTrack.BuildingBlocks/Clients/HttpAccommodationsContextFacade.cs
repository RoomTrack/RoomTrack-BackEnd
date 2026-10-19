using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;

namespace RoomTrack.BuildingBlocks.Clients;

/// <summary>
///     <see cref="IAccommodationsContextFacade"/> of the services other than Accommodations: the Accommodations
///     service's internal API.
/// </summary>
public class HttpAccommodationsContextFacade(HttpClient httpClient)
    : InternalServiceClient(httpClient, "Accommodations"), IAccommodationsContextFacade
{
    public async Task<IReadOnlyDictionary<int, string>> FetchRoomNumbersAsync(IReadOnlyCollection<int> roomIds)
    {
        if (roomIds.Count == 0) return new Dictionary<int, string>();
        var query = string.Join('&', roomIds.Distinct().Select(id => $"ids={id}"));
        return await GetAsync<Dictionary<int, string>>($"internal/v1/rooms/numbers?{query}");
    }

    public Task<RoomOffer?> FetchRoomAsync(int roomId) =>
        roomId <= 0 ? Task.FromResult<RoomOffer?>(null) : GetOrDefaultAsync<RoomOffer>($"internal/v1/rooms/{roomId}/offer");

    public Task<HotelSummary?> FetchHotelAsync(int hotelId) =>
        hotelId <= 0 ? Task.FromResult<HotelSummary?>(null) : GetOrDefaultAsync<HotelSummary>($"internal/v1/hotels/{hotelId}/summary");

    public Task<HotelPaymentInstructions?> FetchPaymentInstructionsAsync(int hotelId) =>
        hotelId <= 0
            ? Task.FromResult<HotelPaymentInstructions?>(null)
            : GetOrDefaultAsync<HotelPaymentInstructions>($"internal/v1/hotels/{hotelId}/payment-instructions");

    public Task<bool> HotelExistsAsync(int hotelId) =>
        hotelId <= 0 ? Task.FromResult(false) : GetAsync<bool>($"internal/v1/hotels/{hotelId}/exists");

    public Task<bool> RoomExistsAsync(int roomId) =>
        roomId <= 0 ? Task.FromResult(false) : GetAsync<bool>($"internal/v1/rooms/{roomId}/exists");

    public async Task<decimal?> FetchRoomPricePerNightAsync(int roomId) =>
        (await FetchRoomAsync(roomId))?.PricePerNight;

    public async Task<int?> FetchHotelIdOfRoomAsync(int roomId) =>
        (await FetchRoomAsync(roomId))?.HotelId;

    /// <summary>
    ///     Only fetches the room: a database lock cannot span two services. The caller that needs the bookings of a
    ///     room serialized must lock them in its own database (the Bookings service decorates this method).
    /// </summary>
    public virtual Task<RoomOffer?> LockRoomForBookingAsync(int roomId) => FetchRoomAsync(roomId);

    /// <remarks>Committed by the Accommodations service on its own: call it last, right before the caller commits.</remarks>
    public Task OccupyRoomForCheckInAsync(int roomId, int? guestUserId, string? guestEmail) =>
        PostAsync($"internal/v1/rooms/{roomId}/occupancy", new { guestUserId, guestEmail });

    public async Task<IReadOnlyList<RoomOffer>> FetchRoomsOfferedForBookingAsync(int? hotelId) =>
        await GetAsync<List<RoomOffer>>(hotelId is null ? "internal/v1/rooms/offered" : $"internal/v1/rooms/offered?hotelId={hotelId}");
}
