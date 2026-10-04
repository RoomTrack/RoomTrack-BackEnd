using BackendAwRoomTrack.API.Profiles.Application.Internal.Queries;
using BackendAwRoomTrack.Domain.Profiles.Domain.Model.Aggregates;

namespace BackendAwRoomTrack.API.Profiles.Application.Internal.QueryServices;

public interface IGuestProfileQueryService
{
    Task<GuestProfile?> Handle(GetGuestProfileByIdQuery query);
    Task<GuestProfile?> Handle(GetGuestProfileByEmailQuery query);
    Task<GuestProfile?> Handle(GetGuestProfileByUserIdQuery query);
    Task<IEnumerable<GuestProfile>> Handle(GetAllGuestProfilesQuery query);
}
