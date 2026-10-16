using BackendAwRoomTrack.API.Accommodations.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Queries;
using BackendAwRoomTrack.API.Accommodations.Domain.Services;
using BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Authorization;
using BackendAwRoomTrack.API.IAM.Interfaces.Authorization;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Commands;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Queries;
using BackendAwRoomTrack.API.Incidents.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Incidents.Domain.Services;
using BackendAwRoomTrack.API.Incidents.Interfaces.REST.Authorization;
using BackendAwRoomTrack.API.Incidents.Interfaces.REST.Resources;
using BackendAwRoomTrack.API.Incidents.Interfaces.REST.Transform;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BackendAwRoomTrack.API.Incidents.Interfaces.REST;

/// <summary>
///     Incidents of the rooms: the hotel staff report them (US-08) and maintenance follows them until they are
///     resolved (US-09). Every change is recorded in the incident's history and notified by e-mail.
/// </summary>
[Authorize(Policy = Policies.ViewIncidents)]
[ApiController]
[Route("api/v1/incidents")]
[Produces("application/json")]
[SwaggerTag("Incidents of the rooms")]
public class IncidentsController(
    IIncidentCommandService incidentCommandService,
    IIncidentQueryService incidentQueryService,
    IRoomQueryService roomQueryService,
    IAuthorizationService authorizationService) : ControllerBase
{
    /// <summary>Reports an incident of a room.</summary>
    /// <remarks>
    ///     Reception, housekeeping, maintenance and administrators report incidents of the rooms of their hotel. The
    ///     incident starts Pending and unassigned; the maintenance staff of the hotel is notified (and its
    ///     administrators, if it is Critical). With <c>blocksRoom</c> the room goes to Maintenance and cannot be made
    ///     Available until every blocking incident of it is resolved.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Policies.ReportIncidents)]
    [SwaggerOperation(Summary = "Report an incident", OperationId = "ReportIncident")]
    [ProducesResponseType(typeof(IncidentResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReportIncident([FromBody] ReportIncidentResource resource)
    {
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(resource.RoomId!.Value));
        if (room is null) return NotFound();
        if (!(await authorizationService.AuthorizeAsync(User, room, RoomOperationsRequirement.Instance)).Succeeded)
            return Forbid();

        var incident = await incidentCommandService.Handle(new ReportIncidentCommand(room.Id, resource.Title!,
            resource.Description, resource.ToPriority(), resource.BlocksRoom, Actor()));
        return CreatedAtAction(nameof(GetIncident), new { incidentId = incident.Id }, IncidentResourceAssembler.ToResource(incident));
    }

    /// <summary>Incidents of a hotel, most urgent first (Critical → Low, then the oldest report).</summary>
    /// <remarks>
    ///     Hotel staff see their hotel (<c>hotelId</c> optional); a chain admin must send <c>hotelId</c>.
    ///     <c>assignedToMe=true</c> lists the requester's incidents (the maintenance panel of US-09);
    ///     <c>open=true</c> leaves out the resolved ones. The history is only returned by <c>GET /incidents/{id}</c>.
    /// </remarks>
    /// <param name="hotelId">The hotel.</param>
    /// <param name="status">Pending, InProgress or Resolved.</param>
    /// <param name="priority">Low, Medium, High or Critical.</param>
    /// <param name="roomId">Only the incidents of this room (its incident history).</param>
    /// <param name="assigneeUserId">Only the incidents assigned to this user.</param>
    /// <param name="assignedToMe">Only the incidents assigned to the requester.</param>
    /// <param name="open">Only Pending and InProgress incidents.</param>
    /// <param name="limit">At most this many (1-500, 100 by default).</param>
    [HttpGet]
    [SwaggerOperation(Summary = "List the incidents of a hotel", OperationId = "GetIncidents")]
    [ProducesResponseType(typeof(IEnumerable<IncidentResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIncidents([FromQuery] int? hotelId, [FromQuery] IncidentStatus? status,
        [FromQuery] IncidentPriority? priority, [FromQuery] int? roomId, [FromQuery] int? assigneeUserId,
        [FromQuery] bool assignedToMe = false, [FromQuery] bool open = false, [FromQuery] int limit = 100)
    {
        var targetHotelId = hotelId ?? (User.IsChainAdmin() ? null : User.GetHotelId());
        if (targetHotelId is null)
            throw new InvalidFieldException("hotelId", AccommodationErrorCodes.HotelRequired, "Send the hotelId of the incidents.");
        if (!User.IsChainAdmin() && User.GetHotelId() != targetHotelId) return Forbid();

        var incidents = await incidentQueryService.Handle(new GetIncidentsQuery(targetHotelId.Value, status, priority, roomId,
            assignedToMe ? User.GetUserId() : assigneeUserId, open, limit));
        return Ok(incidents.Select(incident => IncidentResourceAssembler.ToResource(incident, withHistory: false)));
    }

    /// <summary>An incident with its history.</summary>
    [HttpGet("{incidentId:int}")]
    [SwaggerOperation(Summary = "Get an incident", OperationId = "GetIncident")]
    [ProducesResponseType(typeof(IncidentResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIncident(int incidentId)
    {
        var (incident, denied) = await FindInScopeAsync(incidentId);
        return denied ?? Ok(IncidentResourceAssembler.ToResource(incident!));
    }

    /// <summary>Assigns an incident to a maintenance staff member of its hotel.</summary>
    /// <remarks>
    ///     Administrators assign (or reassign) it to any maintenance staff member of the hotel; a maintenance staff
    ///     member can only take an unassigned incident for themselves. The assignee is notified.
    /// </remarks>
    [HttpPost("{incidentId:int}/assign")]
    [Authorize(Policy = Policies.HandleIncidents)]
    [SwaggerOperation(Summary = "Assign an incident", OperationId = "AssignIncident")]
    [ProducesResponseType(typeof(IncidentResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> AssignIncident(int incidentId, [FromBody] AssignIncidentResource resource) =>
        ExecuteInScopeAsync(incidentId, () =>
            incidentCommandService.Handle(new AssignIncidentCommand(incidentId, resource.AssigneeUserId!.Value, Actor())));

    /// <summary>Starts working on an incident: Pending → InProgress.</summary>
    /// <remarks>
    ///     Only its assignee (or an administrator) starts it; a maintenance staff member starting an unassigned incident
    ///     takes it. The reporter is notified.
    /// </remarks>
    [HttpPost("{incidentId:int}/start")]
    [Authorize(Policy = Policies.HandleIncidents)]
    [SwaggerOperation(Summary = "Start working on an incident", OperationId = "StartIncident")]
    [ProducesResponseType(typeof(IncidentResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> StartIncident(int incidentId) =>
        ExecuteInScopeAsync(incidentId, () => incidentCommandService.Handle(new StartIncidentCommand(incidentId, Actor())));

    /// <summary>Resolves an incident with what was done: InProgress → Resolved.</summary>
    /// <remarks>
    ///     Only its assignee (or an administrator) resolves it, with <c>resolutionNotes</c>. When it blocked its room and
    ///     no other blocking incident of the room is open, the room goes from Maintenance to Cleaning (US-09: the room
    ///     is ready for cleaning or use). The reporter is notified.
    /// </remarks>
    [HttpPost("{incidentId:int}/resolve")]
    [Authorize(Policy = Policies.HandleIncidents)]
    [SwaggerOperation(Summary = "Resolve an incident", OperationId = "ResolveIncident")]
    [ProducesResponseType(typeof(IncidentResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ResolveIncident(int incidentId, [FromBody] ResolveIncidentResource resource) =>
        ExecuteInScopeAsync(incidentId, () =>
            incidentCommandService.Handle(new ResolveIncidentCommand(incidentId, resource.ResolutionNotes, Actor())));

    /// <summary>Re-classifies the urgency of an open incident.</summary>
    [HttpPut("{incidentId:int}/priority")]
    [Authorize(Policy = Policies.HandleIncidents)]
    [SwaggerOperation(Summary = "Change the priority of an incident", OperationId = "ChangeIncidentPriority")]
    [ProducesResponseType(typeof(IncidentResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ChangeIncidentPriority(int incidentId, [FromBody] ChangeIncidentPriorityResource resource) =>
        ExecuteInScopeAsync(incidentId, () =>
            incidentCommandService.Handle(new ChangeIncidentPriorityCommand(incidentId, resource.ToPriority(), Actor())));

    private IncidentActor Actor() =>
        new(User.GetUserId(), User.GetUsername(), User.IsHotelAdmin() || User.IsChainAdmin());

    /// <summary>The incident, or the 404/403 result when it does not exist or is outside the requester's hotel.</summary>
    private async Task<(Incident? Incident, IActionResult? Denied)> FindInScopeAsync(int incidentId)
    {
        var incident = await incidentQueryService.Handle(new GetIncidentByIdQuery(incidentId));
        if (incident is null) return (null, NotFound());
        if (!(await authorizationService.AuthorizeAsync(User, incident, IncidentScopeRequirement.Instance)).Succeeded)
            return (null, Forbid());
        return (incident, null);
    }

    private async Task<IActionResult> ExecuteInScopeAsync(int incidentId, Func<Task<Incident?>> command)
    {
        var (_, denied) = await FindInScopeAsync(incidentId);
        if (denied is not null) return denied;
        var incident = await command();
        return incident is null ? NotFound() : Ok(IncidentResourceAssembler.ToResource(incident));
    }
}
