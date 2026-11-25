using RoomTrack.Accommodations.Domain.Model.Exceptions;
using RoomTrack.Accommodations.Domain.Model.Queries;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Commands;
using RoomTrack.Incidents.Domain.Model.Queries;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Incidents.Domain.Services;
using RoomTrack.Incidents.Interfaces.REST.Resources;
using RoomTrack.Incidents.Interfaces.REST.Transform;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Authentication.ScheduledJobs;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Incidents.Interfaces.REST;

/// <summary>
///     Preventive maintenance of the rooms (US-30): recurring tasks that become incidents of kind Preventive on each
///     due date, with a reminder to the maintenance staff a few days before; and the maintenance history of a room,
///     corrective and preventive (US-10).
/// </summary>
[Authorize(Policy = Policies.ViewIncidents)]
[ApiController]
[Produces("application/json")]
[SwaggerTag("Preventive maintenance of the rooms and their maintenance history")]
public class PreventiveMaintenanceController(
    IPreventiveMaintenanceCommandService commandService,
    IPreventiveMaintenanceQueryService queryService,
    IRoomQueryService roomQueryService) : ControllerBase
{
    private const string PlansRoute = "api/v1/preventive-maintenance-plans";

    /// <summary>Schedules a recurring preventive task of a room.</summary>
    /// <remarks>
    ///     Maintenance and administrators of the room's hotel. On <c>firstDueOn</c> and then every
    ///     <c>frequencyDays</c> days the scheduler generates the task as an incident of kind <c>Preventive</c> (Pending,
    ///     Medium, not blocking the room) for maintenance; <c>reminderDaysBefore</c> days before each due date the
    ///     maintenance staff gets a reminder.
    /// </remarks>
    [HttpPost(PlansRoute)]
    [Authorize(Policy = Policies.HandleIncidents)]
    [SwaggerOperation(Summary = "Schedule a preventive maintenance plan", OperationId = "SchedulePreventiveMaintenance")]
    [ProducesResponseType(typeof(PreventiveMaintenancePlanResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [OpenApiRequestExample("""
        { "roomId": 12, "title": "Limpiar filtros del aire acondicionado",
          "description": "Retirar, lavar y secar los filtros; revisar el drenaje.",
          "frequencyDays": 90, "firstDueOn": "2026-12-01", "reminderDaysBefore": 3 }
        """)]
    [OpenApiResponseExample(StatusCodes.Status201Created, """
        { "id": 4, "hotelId": 1, "roomId": 12, "title": "Limpiar filtros del aire acondicionado",
          "description": "Retirar, lavar y secar los filtros; revisar el drenaje.", "frequencyDays": 90,
          "nextDueOn": "2026-12-01", "reminderDaysBefore": 3, "isActive": true, "generatedCount": 0,
          "lastIncidentId": null, "lastGeneratedAt": null, "createdByUserId": 20, "createdAt": "2026-11-25T14:00:00+00:00" }
        """)]
    public async Task<IActionResult> SchedulePreventiveMaintenance([FromBody] SchedulePreventiveMaintenanceResource resource)
    {
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(resource.RoomId!.Value));
        if (room is null) return NotFound();
        if (!InScope(room.HotelId)) return Forbid();

        var plan = await commandService.Handle(new SchedulePreventiveMaintenanceCommand(room.Id, resource.Title!,
            resource.Description, resource.FrequencyDays!.Value, resource.FirstDueOn!.Value, resource.ReminderDaysBefore ?? 3, Actor()));
        return CreatedAtAction(nameof(GetPreventiveMaintenancePlan), new { planId = plan.Id }, IncidentResourceAssembler.ToResource(plan));
    }

    /// <summary>The preventive maintenance plans of a hotel, next due first.</summary>
    /// <param name="hotelId">The hotel (optional for hotel staff; required for a chain admin).</param>
    /// <param name="roomId">Only the plans of this room.</param>
    /// <param name="active">Only the active plans.</param>
    [HttpGet(PlansRoute)]
    [SwaggerOperation(Summary = "List the preventive maintenance plans", OperationId = "GetPreventiveMaintenancePlans")]
    [ProducesResponseType(typeof(IEnumerable<PreventiveMaintenancePlanResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPreventiveMaintenancePlans([FromQuery] int? hotelId, [FromQuery] int? roomId,
        [FromQuery] bool active = false)
    {
        var targetHotelId = hotelId ?? (User.IsChainAdmin() ? null : User.GetHotelId());
        if (targetHotelId is null)
            throw new InvalidFieldException("hotelId", AccommodationErrorCodes.HotelRequired, "Send the hotelId of the plans.");
        if (!InScope(targetHotelId.Value)) return Forbid();
        var plans = await queryService.Handle(new GetPreventiveMaintenancePlansQuery(targetHotelId.Value, roomId, active));
        return Ok(plans.Select(IncidentResourceAssembler.ToResource));
    }

    /// <summary>A preventive maintenance plan.</summary>
    [HttpGet(PlansRoute + "/{planId:int}")]
    [SwaggerOperation(Summary = "Get a preventive maintenance plan", OperationId = "GetPreventiveMaintenancePlan")]
    [ProducesResponseType(typeof(PreventiveMaintenancePlanResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreventiveMaintenancePlan(int planId)
    {
        var (plan, denied) = await FindInScopeAsync(planId);
        return denied ?? Ok(IncidentResourceAssembler.ToResource(plan!));
    }

    /// <summary>Changes the frequency, next due date and reminder of a plan.</summary>
    [HttpPut(PlansRoute + "/{planId:int}/schedule")]
    [Authorize(Policy = Policies.HandleIncidents)]
    [SwaggerOperation(Summary = "Reschedule a preventive maintenance plan", OperationId = "ReschedulePreventiveMaintenance")]
    [ProducesResponseType(typeof(PreventiveMaintenancePlanResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "frequencyDays": 60, "nextDueOn": "2027-01-15", "reminderDaysBefore": 3 }""")]
    public async Task<IActionResult> ReschedulePreventiveMaintenance(int planId, [FromBody] ReschedulePreventiveMaintenanceResource resource)
    {
        var (_, denied) = await FindInScopeAsync(planId);
        if (denied is not null) return denied;
        var plan = await commandService.Handle(new ReschedulePreventiveMaintenanceCommand(planId, resource.FrequencyDays!.Value,
            resource.NextDueOn!.Value, resource.ReminderDaysBefore ?? 3));
        return plan is null ? NotFound() : Ok(IncidentResourceAssembler.ToResource(plan));
    }

    /// <summary>Stops a plan: no more tasks are generated (the ones already generated stay).</summary>
    [HttpPost(PlansRoute + "/{planId:int}/deactivate")]
    [Authorize(Policy = Policies.HandleIncidents)]
    [SwaggerOperation(Summary = "Deactivate a preventive maintenance plan", OperationId = "DeactivatePreventiveMaintenance")]
    [ProducesResponseType(typeof(PreventiveMaintenancePlanResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePreventiveMaintenance(int planId)
    {
        var (_, denied) = await FindInScopeAsync(planId);
        if (denied is not null) return denied;
        var plan = await commandService.Handle(new DeactivatePreventiveMaintenanceCommand(planId));
        return plan is null ? NotFound() : Ok(IncidentResourceAssembler.ToResource(plan));
    }

    /// <summary>The maintenance history of a room: every corrective and preventive task, newest first.</summary>
    /// <remarks>
    ///     US-10 and US-30: the incidents reported for the room and the preventive tasks generated by its plans, with
    ///     their dates and statuses; recurring incidents (more than 3 similar ones in a month) carry
    ///     <c>isRecurring</c>. Hotel staff of the room's hotel, or a chain admin.
    /// </remarks>
    /// <param name="roomId">The room.</param>
    /// <param name="limit">At most this many tasks (1-500, 200 by default).</param>
    [HttpGet("api/v1/rooms/{roomId:int}/maintenance-history")]
    [SwaggerOperation(Summary = "Get the maintenance history of a room", OperationId = "GetRoomMaintenanceHistory")]
    [ProducesResponseType(typeof(RoomMaintenanceHistoryResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "roomId": 12, "correctiveCount": 1, "preventiveCount": 1, "recurringCount": 0,
          "incidents": [
            { "id": 31, "hotelId": 1, "roomId": 12, "title": "Limpiar filtros del aire acondicionado", "description": null,
              "priority": "Medium", "status": "Resolved", "blocksRoom": false, "reportedByUserId": 20, "reportedByEmail": null,
              "reportedAt": "2026-12-01T05:00:00+00:00", "assigneeUserId": 20, "assignedAt": "2026-12-01T13:00:00+00:00",
              "startedAt": "2026-12-01T13:05:00+00:00", "resolvedAt": "2026-12-01T13:40:00+00:00", "resolvedByUserId": 20,
              "resolutionNotes": "Filtros lavados", "escalatedAt": null, "history": null,
              "kind": "Preventive", "preventivePlanId": 4, "isRecurring": false, "recurrenceCount": null },
            { "id": 18, "hotelId": 1, "roomId": 12, "title": "Ducha con fuga", "description": null, "priority": "High",
              "status": "Resolved", "blocksRoom": false, "reportedByUserId": 10, "reportedByEmail": "recepcion@roomtrack.pe",
              "reportedAt": "2026-11-10T15:00:00+00:00", "assigneeUserId": 20, "assignedAt": null, "startedAt": null,
              "resolvedAt": "2026-11-10T18:00:00+00:00", "resolvedByUserId": 20, "resolutionNotes": "Empaque cambiado",
              "escalatedAt": null, "history": null, "kind": "Corrective", "preventivePlanId": null,
              "isRecurring": false, "recurrenceCount": null } ],
          "preventivePlans": [
            { "id": 4, "hotelId": 1, "roomId": 12, "title": "Limpiar filtros del aire acondicionado", "description": null,
              "frequencyDays": 90, "nextDueOn": "2027-03-01", "reminderDaysBefore": 3, "isActive": true, "generatedCount": 1,
              "lastIncidentId": 31, "lastGeneratedAt": "2026-12-01T05:00:00+00:00", "createdByUserId": 20,
              "createdAt": "2026-11-25T14:00:00+00:00" } ] }
        """)]
    public async Task<IActionResult> GetRoomMaintenanceHistory(int roomId, [FromQuery] int limit = 200)
    {
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(roomId));
        if (room is null) return NotFound();
        if (!InScope(room.HotelId)) return Forbid();
        var history = await queryService.Handle(new GetRoomMaintenanceHistoryQuery(room.Id, room.HotelId, limit));
        return Ok(IncidentResourceAssembler.ToResource(history));
    }

    private IncidentActor Actor() =>
        new(User.GetUserId(), User.GetUsername(), User.IsHotelAdmin() || User.IsChainAdmin());

    private bool InScope(int hotelId) => User.IsChainAdmin() || User.GetHotelId() == hotelId;

    private async Task<(PreventiveMaintenancePlan? Plan, IActionResult? Denied)> FindInScopeAsync(int planId)
    {
        var plan = await queryService.Handle(new GetPreventiveMaintenancePlanByIdQuery(planId));
        if (plan is null) return (null, NotFound());
        return InScope(plan.HotelId) ? (plan, null) : (null, Forbid());
    }
}

/// <summary>
///     Scheduled job of the preventive maintenance, called by the external scheduler with <c>X-Cron-Key</c> (kept apart
///     from <see cref="PreventiveMaintenanceController"/>, whose user-token authorization would otherwise be combined).
/// </summary>
[ApiController]
[Route("api/v1/preventive-maintenance-plans")]
[Produces("application/json")]
[Tags("PreventiveMaintenance")]
public class PreventiveMaintenanceJobsController(IPreventiveMaintenanceCommandService commandService) : ControllerBase
{
    /// <summary>Generates the preventive tasks due today and reminds maintenance of the ones due soon (US-30).</summary>
    /// <remarks>
    ///     Every active plan due today (or overdue) becomes an incident of kind Preventive and moves to its next cycle;
    ///     every plan inside its reminder window sends one reminder per cycle to the maintenance staff (push).
    ///     Idempotent within a day. Run it daily (e.g. 06:00 Lima).
    /// </remarks>
    [HttpPost("runs")]
    [Authorize(Policy = ScheduledJobsAuthenticationExtensions.RunScheduledJobsPolicy)]
    [SwaggerOperation(Summary = "Run the preventive maintenance job", OperationId = "RunPreventiveMaintenance")]
    [ProducesResponseType(typeof(PreventiveMaintenanceRunResultResource), StatusCodes.Status200OK)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """{ "generated": 2, "reminded": 1 }""")]
    public async Task<IActionResult> RunPreventiveMaintenance()
    {
        var result = await commandService.Handle(new RunPreventiveMaintenanceCommand());
        return Ok(new PreventiveMaintenanceRunResultResource(result.Generated, result.Reminded));
    }
}
