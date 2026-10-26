using RoomTrack.Accommodations.Domain.Model.Exceptions;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Domain.Model.Queries;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.GuestRequests.Domain.Model.Commands;
using RoomTrack.GuestRequests.Domain.Model.Exceptions;
using RoomTrack.GuestRequests.Domain.Model.Queries;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.GuestRequests.Domain.Services;
using RoomTrack.GuestRequests.Interfaces.REST.Resources;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.GuestRequests.Interfaces.REST;

/// <summary>
///     Requests of the guests during their stay (US-19) and their coordination by the front desk and the operational
///     areas (US-11).
/// </summary>
[Authorize(Policy = Policies.ViewGuestRequests)]
[ApiController]
[Route("api/v1/guest-requests")]
[Produces("application/json")]
[SwaggerTag("Guest requests: sent by the guests, routed by the front desk, attended by the areas")]
public class GuestRequestsController(
    IGuestRequestCommandService commandService,
    IGuestRequestQueryService queryService,
    IRoomQueryService roomQueryService) : ControllerBase
{
    /// <summary>Sends (guest) or registers (front desk) a request.</summary>
    /// <remarks>
    ///     A guest: for the room of their stay today (409 <c>guest_request.no_current_stay</c> without one); it arrives
    ///     at the front desk unless they choose the <c>area</c>, and the answer says when it will be attended
    ///     (<c>estimatedAttentionBy</c>). The front desk: for the <c>roomId</c> and the <c>area</c> that attends it; the
    ///     staff of that area is notified at once.
    /// </remarks>
    [HttpPost]
    [SwaggerOperation(Summary = "Send or register a guest request", OperationId = "CreateGuestRequest")]
    [ProducesResponseType(typeof(GuestRequestResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateGuestRequest([FromBody] CreateGuestRequestResource resource)
    {
        var actor = Actor();
        GuestRequest request;
        if (actor.IsGuest)
            request = await commandService.Handle(new SendGuestRequestCommand(resource.Title!, resource.Description, resource.Area, actor));
        else if (actor.CanManage)
        {
            if (resource.RoomId is not { } roomId)
                throw new InvalidFieldException("roomId", GuestRequestErrorCodes.RoomRequired, "Send the roomId of the guest.");
            if (resource.Area is not { } area)
                throw new InvalidFieldException("area", GuestRequestErrorCodes.AreaRequired, "Send the area that attends the request.");
            var room = await roomQueryService.Handle(new GetRoomByIdQuery(roomId));
            if (room is null) return NotFound();
            if (!InHotel(room.HotelId)) return Forbid();
            request = await commandService.Handle(new RegisterGuestRequestCommand(roomId, resource.Title!, resource.Description, area, actor));
        }
        else return Forbid();

        return CreatedAtAction(nameof(GetGuestRequest), new { guestRequestId = request.Id }, GuestRequestResourceAssembler.ToResource(request));
    }

    /// <summary>Lists requests, newest first: a guest their own; hotel staff those of their hotel.</summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List guest requests", OperationId = "GetGuestRequests")]
    [ProducesResponseType(typeof(IEnumerable<GuestRequestResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetGuestRequests([FromQuery] int? hotelId, [FromQuery] ServiceArea? area,
        [FromQuery] GuestRequestStatus? status, [FromQuery] int? roomId, [FromQuery] bool assignedToMe = false,
        [FromQuery] bool open = false, [FromQuery] int limit = 100)
    {
        if (User.IsGuest())
            return Ok((await queryService.Handle(new GetGuestRequestsOfGuestQuery(User.GetUserId(), limit)))
                .Select(request => GuestRequestResourceAssembler.ToResource(request, withHistory: false)));

        var targetHotelId = hotelId ?? User.GetHotelId();
        if (targetHotelId is null)
            throw new InvalidFieldException("hotelId", AccommodationErrorCodes.HotelRequired, "Send the hotelId of the requests.");
        if (!InHotel(targetHotelId.Value)) return Forbid();

        var requests = await queryService.Handle(new GetGuestRequestsQuery(targetHotelId.Value, area, status, roomId,
            assignedToMe ? User.GetUserId() : null, open, limit));
        return Ok(requests.Select(request => GuestRequestResourceAssembler.ToResource(request, withHistory: false)));
    }

    /// <summary>Gets a request with its history (a guest only their own).</summary>
    [HttpGet("{guestRequestId:int}")]
    [SwaggerOperation(Summary = "Get a guest request", OperationId = "GetGuestRequest")]
    [ProducesResponseType(typeof(GuestRequestResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGuestRequest(int guestRequestId)
    {
        var request = await queryService.Handle(new GetGuestRequestByIdQuery(guestRequestId));
        return request is null || !CanSee(request) ? NotFound() : Ok(GuestRequestResourceAssembler.ToResource(request));
    }

    /// <summary>Derives the request to the area that attends it (front desk, US-11).</summary>
    [HttpPost("{guestRequestId:int}/route")]
    [Authorize(Policy = Policies.RegisterGuestRequests)]
    [SwaggerOperation(Summary = "Route a guest request to an area", OperationId = "RouteGuestRequest")]
    [ProducesResponseType(typeof(GuestRequestResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> RouteGuestRequest(int guestRequestId, [FromBody] RouteGuestRequestResource resource) =>
        ExecuteInScopeAsync(guestRequestId, () =>
            commandService.Handle(new RouteGuestRequestCommand(guestRequestId, resource.Area!.Value, Actor())));

    /// <summary>Puts a staff member of the area in charge (the front desk), or takes it for oneself (the area staff).</summary>
    [HttpPost("{guestRequestId:int}/assign")]
    [Authorize(Policy = Policies.HandleGuestRequests)]
    [SwaggerOperation(Summary = "Assign a guest request", OperationId = "AssignGuestRequest")]
    [ProducesResponseType(typeof(GuestRequestResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> AssignGuestRequest(int guestRequestId, [FromBody] AssignGuestRequestResource resource) =>
        ExecuteInScopeAsync(guestRequestId, () =>
            commandService.Handle(new AssignGuestRequestCommand(guestRequestId, resource.AssigneeUserId!.Value, Actor())));

    /// <summary>The area starts attending it; the guest sees it "en proceso".</summary>
    [HttpPost("{guestRequestId:int}/start")]
    [Authorize(Policy = Policies.HandleGuestRequests)]
    [SwaggerOperation(Summary = "Start attending a guest request", OperationId = "StartGuestRequest")]
    [ProducesResponseType(typeof(GuestRequestResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> StartGuestRequest(int guestRequestId) =>
        ExecuteInScopeAsync(guestRequestId, () => commandService.Handle(new StartGuestRequestCommand(guestRequestId, Actor())));

    /// <summary>The area resolved it; the guest receives the confirmation.</summary>
    [HttpPost("{guestRequestId:int}/resolve")]
    [Authorize(Policy = Policies.HandleGuestRequests)]
    [SwaggerOperation(Summary = "Resolve a guest request", OperationId = "ResolveGuestRequest")]
    [ProducesResponseType(typeof(GuestRequestResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ResolveGuestRequest(int guestRequestId, [FromBody] CloseGuestRequestResource? resource) =>
        ExecuteInScopeAsync(guestRequestId, () =>
            commandService.Handle(new ResolveGuestRequestCommand(guestRequestId, resource?.Note, Actor())));

    /// <summary>The guest withdraws a request nobody started, or the front desk cancels it.</summary>
    [HttpPost("{guestRequestId:int}/cancel")]
    [SwaggerOperation(Summary = "Cancel a guest request", OperationId = "CancelGuestRequest")]
    [ProducesResponseType(typeof(GuestRequestResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> CancelGuestRequest(int guestRequestId, [FromBody] CloseGuestRequestResource? resource) =>
        ExecuteInScopeAsync(guestRequestId, () =>
            commandService.Handle(new CancelGuestRequestCommand(guestRequestId, resource?.Note, Actor())));

    /// <summary>The role that decides what the requester may do with a request (one per account).</summary>
    private GuestRequestActor Actor()
    {
        string[] byPrecedence = [UserRoles.ChainAdmin, UserRoles.Admin, UserRoles.Reception, UserRoles.Housekeeping,
            UserRoles.Maintenance, UserRoles.Guest];
        return new GuestRequestActor(User.GetUserId(), User.GetUsername(),
            byPrecedence.FirstOrDefault(User.IsInRole) ?? UserRoles.Guest);
    }

    private bool InHotel(int hotelId) => User.IsChainAdmin() || User.GetHotelId() == hotelId;

    /// <summary>A guest sees their own requests; hotel staff those of their hotel.</summary>
    private bool CanSee(GuestRequest request) =>
        User.IsGuest() ? request.GuestUserId == User.GetUserId() : InHotel(request.HotelId);

    private async Task<IActionResult> ExecuteInScopeAsync(int guestRequestId, Func<Task<GuestRequest?>> command)
    {
        var request = await queryService.Handle(new GetGuestRequestByIdQuery(guestRequestId));
        if (request is null || !CanSee(request)) return NotFound();
        var changed = await command();
        return changed is null ? NotFound() : Ok(GuestRequestResourceAssembler.ToResource(changed));
    }
}

