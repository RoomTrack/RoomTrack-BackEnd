using RoomTrack.Profiles.Application.Internal.Queries;
using RoomTrack.Profiles.Domain.Model.Aggregates;

namespace RoomTrack.Profiles.Application.Internal.QueryServices;

public interface IStaffProfileQueryService
{
    Task<StaffProfile?> Handle(GetStaffProfileByIdQuery query);
    Task<StaffProfile?> Handle(GetStaffProfileByUserIdQuery query);
    Task<StaffProfile?> Handle(GetStaffProfileByEmployeeCodeQuery query);
    Task<IEnumerable<StaffProfile>> Handle(GetStaffProfilesByHotelIdQuery query);
    Task<IEnumerable<StaffProfile>> Handle(GetAllStaffProfilesQuery query);
}
