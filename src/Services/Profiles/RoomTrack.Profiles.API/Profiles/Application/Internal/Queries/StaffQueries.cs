using RoomTrack.Profiles.Domain.Model.ValueObjects;

namespace RoomTrack.Profiles.Application.Internal.Queries;

public record GetStaffProfileByIdQuery(StaffProfileId ProfileId);

public record GetStaffProfileByUserIdQuery(UserId UserId);

public record GetStaffProfileByEmployeeCodeQuery(EmployeeCode Code);

public record GetStaffProfilesByHotelIdQuery(TargetId HotelId);

public record GetAllStaffProfilesQuery;
