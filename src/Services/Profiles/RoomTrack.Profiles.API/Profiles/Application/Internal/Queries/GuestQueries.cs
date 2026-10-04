using BackendAwRoomTrack.Domain.Profiles.Domain.Model.ValueObjects;

namespace BackendAwRoomTrack.API.Profiles.Application.Internal.Queries;

public record GetGuestProfileByIdQuery(GuestProfileId ProfileId);

public record GetGuestProfileByEmailQuery(EmailAddress Email);

public record GetGuestProfileByUserIdQuery(UserId UserId);

public record GetAllGuestProfilesQuery;
