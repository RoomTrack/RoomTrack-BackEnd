using RoomTrack.Profiles.Application.Internal.Queries;
using RoomTrack.Profiles.Domain.Model.Aggregates;

namespace RoomTrack.Profiles.Application.Internal.QueryServices;

public interface IGuestProfileQueryService
{
    Task<GuestProfile?> Handle(GetGuestProfileByIdQuery query);
    Task<GuestProfile?> Handle(GetGuestProfileByEmailQuery query);
    Task<GuestProfile?> Handle(GetGuestProfileByUserIdQuery query);
    Task<IEnumerable<GuestProfile>> Handle(GetAllGuestProfilesQuery query);
}
