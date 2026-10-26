using RoomTrack.Profiles.Application.Internal.Queries;
using RoomTrack.Profiles.Domain.Model.Aggregates;
using RoomTrack.Profiles.Domain.Repositories;

namespace RoomTrack.Profiles.Application.Internal.QueryServices;

public class StaffProfileQueryService(IStaffProfileRepository staffProfileRepository) : IStaffProfileQueryService
{
    public async Task<StaffProfile?> Handle(GetStaffProfileByIdQuery query)
    {
        return await staffProfileRepository.FindByIdAsync(query.ProfileId);
    }

    public async Task<StaffProfile?> Handle(GetStaffProfileByUserIdQuery query)
    {
        return await staffProfileRepository.FindByUserIdAsync(query.UserId);
    }

    public async Task<StaffProfile?> Handle(GetStaffProfileByEmployeeCodeQuery query)
    {
        return await staffProfileRepository.FindByEmployeeCodeAsync(query.Code);
    }

    public async Task<IEnumerable<StaffProfile>> Handle(GetStaffProfilesByHotelIdQuery query)
    {
        return await staffProfileRepository.FindByTargetIdAsync(query.HotelId);
    }

    public async Task<IEnumerable<StaffProfile>> Handle(GetAllStaffProfilesQuery query)
    {
        return await staffProfileRepository.ListAsync();
    }
}
