using BackendAwRoomTrack.API.IAM.Application.OutboundServices;
using BackendAwRoomTrack.API.IAM.Domain.Model.Queries;
using BackendAwRoomTrack.API.IAM.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.IAM.Domain.Services;

namespace RoomTrack.Identity.API.Infrastructure.Sessions;

/// <summary>The Identity service validates the sessions of its own tokens straight from its database.</summary>
public class LocalUserSessionValidator(IUserQueryService userQueryService) : IUserSessionValidator
{
    public Task<UserSession> GetSessionAsync(int userId, int tokenVersion, CancellationToken cancellationToken = default) =>
        userQueryService.Handle(new GetUserSessionQuery(userId, tokenVersion));
}
