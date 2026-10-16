using System.Globalization;
using BackendAwRoomTrack.API.Bookings.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;

namespace RoomTrack.BuildingBlocks.Clients;

/// <summary><see cref="IRoomReservationsFacade"/> of the Accommodations service: the Bookings service's internal API.</summary>
public class HttpRoomReservationsFacade(HttpClient httpClient)
    : InternalServiceClient(httpClient, "Bookings"), IRoomReservationsFacade
{
    public async Task<IReadOnlyDictionary<int, int>> CountActiveBookingsAsync(IReadOnlyCollection<int> roomIds)
    {
        if (roomIds.Count == 0) return new Dictionary<int, int>();
        var query = string.Join('&', roomIds.Distinct().Select(id => $"roomIds={id}"));
        return await GetAsync<Dictionary<int, int>>($"internal/v1/rooms/active-bookings?{query}");
    }

    public Task<bool> HasCurrentConfirmedStayAsync(int guestUserId, int roomId, DateTime day) =>
        GetAsync<bool>($"internal/v1/guests/{guestUserId}/current-stay?roomId={roomId}" +
                       $"&day={day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
}
