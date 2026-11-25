using RoomTrack.Accommodations.Domain.Model.Exceptions;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using RoomTrack.ShiftHandovers.Domain.Model.Aggregates;
using RoomTrack.ShiftHandovers.Domain.Model.Commands;
using RoomTrack.ShiftHandovers.Domain.Model.Queries;
using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;
using RoomTrack.ShiftHandovers.Domain.Services;
using RoomTrack.ShiftHandovers.Interfaces.REST.Resources;
using RoomTrack.ShiftHandovers.Interfaces.REST.Transform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.ShiftHandovers.Interfaces.REST;

/// <summary>
///     Shift handovers (US-32): at the end of a shift the staff member leaves a summary with the tasks and incidents
///     still open; the next shift of the area reads it on its panel and confirms it, recording who and when.
/// </summary>
[Authorize(Policy = Policies.UseShifts)]
[ApiController]
[Route("api/v1/shift-handovers")]
[Produces("application/json")]
[SwaggerTag("Shift handovers between the staff of an area")]
public class ShiftHandoversController(
    IShiftHandoverCommandService commandService,
    IShiftHandoverQueryService queryService) : ControllerBase
{
    /// <summary>Ends the shift with a handover for the next one.</summary>
    /// <remarks>
    ///     Reception, housekeeping and maintenance hand over the area of their role (housekeeping: their open cleaning
    ///     tasks, the unassigned ones and the open incidents they reported; maintenance: their open incidents and the unassigned ones; reception: the
    ///     urgent cleaning tasks and the high or critical incidents of the hotel). An administrator names the
    ///     <c>area</c> and hands over all of its open items. The staff of the area is notified. 409
    ///     <c>shift_handover.empty</c> without notes nor open items.
    /// </remarks>
    [HttpPost]
    [SwaggerOperation(Summary = "Hand over a shift", OperationId = "CreateShiftHandover")]
    [ProducesResponseType(typeof(ShiftHandoverResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""
        { "notes": "La 204 pidió almohadas extra para las 20:00. La 310 sigue con olor a humedad, avisé a mantenimiento." }
        """)]
    [OpenApiResponseExample(StatusCodes.Status201Created, """
        { "id": 9, "hotelId": 1, "area": "Housekeeping", "authorUserId": 7, "authorEmail": "rosa@roomtrack.pe",
          "notes": "La 204 pidió almohadas extra para las 20:00.", "createdAt": "2026-11-25T23:00:00+00:00",
          "items": [ { "kind": "CleaningTask", "referenceId": 55, "roomId": 12, "summary": "Limpieza de la habitación 12",
                       "status": "InProgress", "priority": "High", "assigneeUserId": 7 } ],
          "acknowledgements": [], "acknowledgedByMe": false }
        """)]
    public async Task<IActionResult> CreateShiftHandover([FromBody] CreateShiftHandoverResource resource)
    {
        var hotelId = User.GetHotelId()
                      ?? throw new InvalidFieldException("hotelId", AccommodationErrorCodes.HotelRequired,
                          "Only the staff of a hotel hands over a shift.");
        var handover = await commandService.Handle(new CreateShiftHandoverCommand(hotelId, resource.Area, resource.Notes, Actor()));
        return CreatedAtAction(nameof(GetShiftHandover), new { shiftHandoverId = handover.Id },
            ShiftHandoverResourceAssembler.ToResource(handover, User.GetUserId()));
    }

    /// <summary>The last handover of an area: what the shift starting now reads on its panel.</summary>
    /// <param name="area">FrontDesk, Housekeeping or Maintenance (by default, the area of the requester's role).</param>
    /// <param name="hotelId">The hotel (optional for hotel staff; required for a chain admin).</param>
    [HttpGet("latest")]
    [SwaggerOperation(Summary = "Get the latest handover of an area", OperationId = "GetLatestShiftHandover")]
    [ProducesResponseType(typeof(ShiftHandoverResource), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLatestShiftHandover([FromQuery] ShiftArea? area, [FromQuery] int? hotelId)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var targetArea = area ?? Actor().Area
                         ?? throw new InvalidFieldException("area", "shift_handover.area_required",
                             "Name the area: FrontDesk, Housekeeping or Maintenance.");
        var handover = await queryService.Handle(new GetLatestShiftHandoverQuery(targetHotelId, targetArea));
        return handover is null ? NoContent() : Ok(ShiftHandoverResourceAssembler.ToResource(handover, User.GetUserId()));
    }

    /// <summary>The handovers of a hotel, newest first.</summary>
    /// <param name="hotelId">The hotel (optional for hotel staff; required for a chain admin).</param>
    /// <param name="area">Only this area.</param>
    /// <param name="limit">At most this many (1-100, 20 by default).</param>
    [HttpGet]
    [SwaggerOperation(Summary = "List the shift handovers", OperationId = "GetShiftHandovers")]
    [ProducesResponseType(typeof(IEnumerable<ShiftHandoverResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetShiftHandovers([FromQuery] int? hotelId, [FromQuery] ShiftArea? area,
        [FromQuery] int limit = 20)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var handovers = await queryService.Handle(new GetShiftHandoversQuery(targetHotelId, area, limit));
        return Ok(handovers.Select(handover => ShiftHandoverResourceAssembler.ToResource(handover, User.GetUserId())));
    }

    /// <summary>A shift handover with its items and confirmations.</summary>
    [HttpGet("{shiftHandoverId:int}")]
    [SwaggerOperation(Summary = "Get a shift handover", OperationId = "GetShiftHandover")]
    [ProducesResponseType(typeof(ShiftHandoverResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetShiftHandover(int shiftHandoverId)
    {
        var (handover, denied) = await FindInScopeAsync(shiftHandoverId);
        return denied ?? Ok(ShiftHandoverResourceAssembler.ToResource(handover!, User.GetUserId()));
    }

    /// <summary>Confirms reading a handover: who and when are recorded.</summary>
    /// <remarks>Any staff member of the hotel except its author. Confirming twice keeps the first confirmation.</remarks>
    [HttpPost("{shiftHandoverId:int}/acknowledge")]
    [SwaggerOperation(Summary = "Confirm reading a shift handover", OperationId = "AcknowledgeShiftHandover")]
    [ProducesResponseType(typeof(ShiftHandoverResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "id": 9, "hotelId": 1, "area": "Housekeeping", "authorUserId": 7, "authorEmail": "rosa@roomtrack.pe",
          "notes": "La 204 pidió almohadas extra para las 20:00.", "createdAt": "2026-11-25T23:00:00+00:00", "items": [],
          "acknowledgements": [ { "userId": 8, "email": "carla@roomtrack.pe", "acknowledgedAt": "2026-11-25T23:10:00+00:00" } ],
          "acknowledgedByMe": true }
        """)]
    public async Task<IActionResult> AcknowledgeShiftHandover(int shiftHandoverId)
    {
        var (_, denied) = await FindInScopeAsync(shiftHandoverId);
        if (denied is not null) return denied;
        var handover = await commandService.Handle(new AcknowledgeShiftHandoverCommand(shiftHandoverId, Actor()));
        return handover is null ? NotFound() : Ok(ShiftHandoverResourceAssembler.ToResource(handover, User.GetUserId()));
    }

    private ShiftHandoverActor Actor() => new(User.GetUserId(), User.GetUsername(),
        User.IsInRole(UserRoles.Housekeeping) ? ShiftArea.Housekeeping
        : User.IsInRole(UserRoles.Maintenance) ? ShiftArea.Maintenance
        : User.IsInRole(UserRoles.Reception) ? ShiftArea.FrontDesk
        : null);

    private (int HotelId, IActionResult? Denied) HotelInScope(int? hotelId)
    {
        var targetHotelId = hotelId ?? (User.IsChainAdmin() ? null : User.GetHotelId());
        if (targetHotelId is null)
            throw new InvalidFieldException("hotelId", AccommodationErrorCodes.HotelRequired, "Send the hotelId of the handovers.");
        if (!User.IsChainAdmin() && User.GetHotelId() != targetHotelId) return (0, Forbid());
        return (targetHotelId.Value, null);
    }

    private async Task<(ShiftHandover? Handover, IActionResult? Denied)> FindInScopeAsync(int shiftHandoverId)
    {
        var handover = await queryService.Handle(new GetShiftHandoverByIdQuery(shiftHandoverId));
        if (handover is null) return (null, NotFound());
        if (!User.IsChainAdmin() && User.GetHotelId() != handover.HotelId) return (null, Forbid());
        return (handover, null);
    }
}
