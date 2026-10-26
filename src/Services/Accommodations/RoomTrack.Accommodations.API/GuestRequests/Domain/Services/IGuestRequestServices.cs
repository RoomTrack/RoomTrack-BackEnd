using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Commands;
using RoomTrack.GuestRequests.Domain.Model.Queries;

namespace RoomTrack.GuestRequests.Domain.Services;

public interface IGuestRequestCommandService
{
    Task<GuestRequest> Handle(SendGuestRequestCommand command);
    Task<GuestRequest> Handle(RegisterGuestRequestCommand command);
    Task<GuestRequest?> Handle(RouteGuestRequestCommand command);
    Task<GuestRequest?> Handle(AssignGuestRequestCommand command);
    Task<GuestRequest?> Handle(StartGuestRequestCommand command);
    Task<GuestRequest?> Handle(ResolveGuestRequestCommand command);
    Task<GuestRequest?> Handle(CancelGuestRequestCommand command);
}

public interface IGuestRequestQueryService
{
    Task<GuestRequest?> Handle(GetGuestRequestByIdQuery query);
    Task<IReadOnlyList<GuestRequest>> Handle(GetGuestRequestsQuery query);
    Task<IReadOnlyList<GuestRequest>> Handle(GetGuestRequestsOfGuestQuery query);
}
