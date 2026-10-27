using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Queries;

namespace RoomTrack.GuestRequests.Domain.Repositories;

public interface IGuestRequestRepository
{
    Task AddAsync(GuestRequest request);
    Task<GuestRequest?> FindByIdAsync(int id);
    Task<IReadOnlyList<GuestRequest>> ListAsync(GetGuestRequestsQuery query);
    Task<IReadOnlyList<GuestRequest>> ListOfGuestAsync(GetGuestRequestsOfGuestQuery query);
}
