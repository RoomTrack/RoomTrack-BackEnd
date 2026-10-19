using BackendAwRoomTrack.API.Profiles.Application.Internal.Queries;
using BackendAwRoomTrack.Domain.Profiles.Domain.Model.Aggregates;

namespace BackendAwRoomTrack.API.Profiles.Application.Internal.QueryServices;

public interface IStaffProfileQueryService
{
    Task<StaffProfile?> Handle(GetStaffProfileByIdQuery query);
    Task<StaffProfile?> Handle(GetStaffProfileByUserIdQuery query);
    Task<StaffProfile?> Handle(GetStaffProfileByEmployeeCodeQuery query);
    Task<IEnumerable<StaffProfile>> Handle(GetStaffProfilesByHotelIdQuery query);
    Task<IEnumerable<StaffProfile>> Handle(GetAllStaffProfilesQuery query);
}
