using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;

namespace RoomTrack.BuildingBlocks.Clients;

/// <summary><see cref="IIamContextFacade"/> of the services other than Identity: the Identity service's internal API.</summary>
public class HttpIamContextFacade(HttpClient httpClient) : InternalServiceClient(httpClient, "Identity"), IIamContextFacade
{
    public Task<UserContact?> FetchUserContactAsync(int userId) =>
        GetOrDefaultAsync<UserContact>($"internal/v1/users/{userId}/contact");

    public async Task<IReadOnlyList<UserContact>> ListHotelStaffAsync(int hotelId, IReadOnlyCollection<string> roles)
    {
        var query = string.Join('&', roles.Select(role => $"roles={Uri.EscapeDataString(role)}"));
        return await GetAsync<List<UserContact>>($"internal/v1/hotels/{hotelId}/staff?{query}");
    }

    public Task<int> FetchUserIdByEmail(string email) =>
        GetAsync<int>($"internal/v1/users/id?email={Uri.EscapeDataString(email)}");

    public Task<string> FetchEmailByUserId(int userId) => GetAsync<string>($"internal/v1/users/{userId}/email");

    /// <remarks>
    ///     Unlike the in-process facade of the monolith, this runs in the Identity service's own transaction: it is
    ///     committed there even if the caller's transaction later rolls back (call it once the caller's change is saved).
    /// </remarks>
    public Task<ReissuedSession?> AssignHotelToAdministratorAsync(int userId, int hotelId, SessionContext currentSession) =>
        PostAsync<ReissuedSession>($"internal/v1/users/{userId}/hotel-assignment",
            new { hotelId, rememberedSessionId = currentSession.RememberedSessionId });
}
