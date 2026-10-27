using RoomTrack.Profiles.Application.Internal.Queries;
using RoomTrack.Profiles.Domain.Model.Aggregates;
using RoomTrack.Profiles.Domain.Repositories;

namespace RoomTrack.Profiles.Application.Internal.QueryServices;

public class GuestProfileQueryService(IGuestProfileRepository guestProfileRepository) : IGuestProfileQueryService
{
    public async Task<GuestProfile?> Handle(GetGuestProfileByIdQuery query)
    {
        return await guestProfileRepository.FindByIdAsync(query.ProfileId);
    }

    public async Task<GuestProfile?> Handle(GetGuestProfileByEmailQuery query)
    {
        return await guestProfileRepository.FindByEmailAsync(query.Email);
    }

    public async Task<GuestProfile?> Handle(GetGuestProfileByUserIdQuery query)
    {
        return await guestProfileRepository.FindByUserIdAsync(query.UserId);
    }

    public async Task<IEnumerable<GuestProfile>> Handle(GetAllGuestProfilesQuery query)
    {
        return await guestProfileRepository.ListAsync();
    }
}
