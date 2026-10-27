using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.IAM.Interfaces.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace RoomTrack.Housekeeping.Interfaces.REST.Authorization;

/// <summary>Requirement "the requester works at the hotel of this cleaning task".</summary>
public sealed class CleaningTaskScopeRequirement : IAuthorizationRequirement
{
    public static readonly CleaningTaskScopeRequirement Instance = new();

    private CleaningTaskScopeRequirement() { }
}

/// <summary>
///     Hotel staff see and handle the cleaning tasks of the hotel they are assigned to; a chain administrator those of
///     every hotel. Which roles may call each endpoint is decided by its policy.
/// </summary>
public sealed class CleaningTaskScopeAuthorizationHandler : AuthorizationHandler<CleaningTaskScopeRequirement, CleaningTask>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, CleaningTaskScopeRequirement requirement, CleaningTask task)
    {
        var user = context.User;
        if (user.IsChainAdmin() || (user.GetHotelId() is { } hotelId && hotelId == task.HotelId))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
