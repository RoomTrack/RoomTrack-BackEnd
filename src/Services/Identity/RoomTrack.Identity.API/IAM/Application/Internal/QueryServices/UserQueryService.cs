using RoomTrack.IAM.Domain.Model.Aggregates;
using RoomTrack.IAM.Domain.Model.Queries;
using RoomTrack.IAM.Domain.Model.ValueObjects;
using RoomTrack.IAM.Domain.Repositories;
using RoomTrack.IAM.Domain.Services;

namespace RoomTrack.IAM.Application.Internal.QueryServices;

public class UserQueryService(
    IUserRepository userRepository,
    IUserScopeService userScopeService) : IUserQueryService
{
    public async Task<User?> Handle(GetUserByIdQuery query)
    {
        var target = await userRepository.FindByIdAsync(query.Id);
        if (target == null) return null;

        // Scope verification
        if (query.ActorUserId.HasValue)
        {
            var actor = await userRepository.FindByIdAsync(query.ActorUserId.Value);
            if (actor == null || !userScopeService.CanAccessUser(actor, target))
                return null; 
        }

        return target;
    }

    public async Task<IEnumerable<User>> Handle(GetUsersByScopeQuery query)
    {
        var actor = await userRepository.FindByIdAsync(query.ActorUserId);
        if (actor == null) return Enumerable.Empty<User>();

        var allUsers = await userRepository.ListAsync();
        
        // Reutilizamos la lógica centralizada del dominio. DDD 10/10.
        return allUsers.Where(target => userScopeService.CanAccessUser(actor, target));
    }

    public async Task<User?> Handle(GetUserByEmailQuery query)
    {
        return await userRepository.FindByEmailAsync(new Email(query.Email));
    }

    public Task<User?> Handle(GetCurrentUserQuery query) => userRepository.FindByIdAsync(query.UserId);

    public async Task<UserSession> Handle(GetUserSessionQuery query)
    {
        var user = await userRepository.FindByIdAsync(query.UserId);
        return user?.GetSession(query.TokenVersion) ?? UserSession.NotFound();
    }
}
