using BackendAwRoomTrack.Domain.Profiles.Domain.Model.ValueObjects;

namespace BackendAwRoomTrack.API.Profiles.Application.Internal.Queries;

public record GetStaffProfileByIdQuery(StaffProfileId ProfileId);

public record GetStaffProfileByUserIdQuery(UserId UserId);

public record GetStaffProfileByEmployeeCodeQuery(EmployeeCode Code);

public record GetStaffProfilesByHotelIdQuery(TargetId HotelId);

public record GetAllStaffProfilesQuery;
