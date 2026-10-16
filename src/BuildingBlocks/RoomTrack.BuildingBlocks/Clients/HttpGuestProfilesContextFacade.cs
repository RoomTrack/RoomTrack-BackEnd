using BackendAwRoomTrack.API.Profiles.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;

namespace RoomTrack.BuildingBlocks.Clients;

/// <summary>
///     <see cref="IGuestProfilesContextFacade"/> of the services other than Profiles: the Profiles service's internal API.
/// </summary>
public class HttpGuestProfilesContextFacade(HttpClient httpClient)
    : InternalServiceClient(httpClient, "Profiles"), IGuestProfilesContextFacade
{
    public Task<Guid?> CreateGuestProfileAsync(string firstName, string lastName, string phone, string? email = null, int? userId = null) =>
        PostAsync<Guid?>("internal/v1/guest-profiles", new { firstName, lastName, phone, email, userId });

    public Task<Guid?> FetchGuestProfileIdByUserIdAsync(int userId) =>
        GetOrDefaultAsync<Guid?>($"internal/v1/guest-profiles/by-user/{userId}");

    public Task<Guid?> FetchGuestProfileIdByEmailAsync(string email) =>
        GetOrDefaultAsync<Guid?>($"internal/v1/guest-profiles/by-email?email={Uri.EscapeDataString(email)}");

    public async Task<bool> LinkGuestProfileToUserAsync(Guid guestProfileId, int userId, string email) =>
        await PostAsync<bool>($"internal/v1/guest-profiles/{guestProfileId}/link", new { userId, email });
}
