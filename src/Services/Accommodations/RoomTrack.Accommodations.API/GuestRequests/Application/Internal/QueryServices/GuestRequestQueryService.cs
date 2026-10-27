using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Queries;
using RoomTrack.GuestRequests.Domain.Repositories;
using RoomTrack.GuestRequests.Domain.Services;

namespace RoomTrack.GuestRequests.Application.Internal.QueryServices;

public class GuestRequestQueryService(IGuestRequestRepository requestRepository) : IGuestRequestQueryService
{
    public Task<GuestRequest?> Handle(GetGuestRequestByIdQuery query) => requestRepository.FindByIdAsync(query.GuestRequestId);

    public Task<IReadOnlyList<GuestRequest>> Handle(GetGuestRequestsQuery query) =>
        requestRepository.ListAsync(query with { Limit = Math.Clamp(query.Limit, 1, 500) });

    public Task<IReadOnlyList<GuestRequest>> Handle(GetGuestRequestsOfGuestQuery query) =>
        requestRepository.ListOfGuestAsync(query with { Limit = Math.Clamp(query.Limit, 1, 500) });
}
