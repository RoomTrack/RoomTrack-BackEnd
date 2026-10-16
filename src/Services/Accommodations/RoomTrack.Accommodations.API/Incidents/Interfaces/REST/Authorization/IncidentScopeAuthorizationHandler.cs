using BackendAwRoomTrack.API.IAM.Interfaces.Authorization;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Aggregates;
using Microsoft.AspNetCore.Authorization;

namespace BackendAwRoomTrack.API.Incidents.Interfaces.REST.Authorization;

/// <summary>Requirement "the requester works at the hotel of this incident".</summary>
public sealed class IncidentScopeRequirement : IAuthorizationRequirement
{
    public static readonly IncidentScopeRequirement Instance = new();

    private IncidentScopeRequirement() { }
}

/// <summary>
///     Hotel staff see and handle the incidents of the hotel they are assigned to; a chain administrator those of every
///     hotel. Which roles may call each endpoint is decided by its policy.
/// </summary>
public sealed class IncidentScopeAuthorizationHandler : AuthorizationHandler<IncidentScopeRequirement, Incident>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, IncidentScopeRequirement requirement, Incident incident)
    {
        var user = context.User;
        if (user.IsChainAdmin() || (user.GetHotelId() is { } hotelId && hotelId == incident.HotelId))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
