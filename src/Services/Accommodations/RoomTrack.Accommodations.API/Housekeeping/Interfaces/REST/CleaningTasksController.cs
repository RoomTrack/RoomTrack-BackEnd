using RoomTrack.Accommodations.Domain.Model.Exceptions;
using RoomTrack.Accommodations.Domain.Model.Queries;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.Accommodations.Interfaces.REST.Authorization;
using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Commands;
using RoomTrack.Housekeeping.Domain.Model.Queries;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Housekeeping.Domain.Services;
using RoomTrack.Housekeeping.Interfaces.REST.Authorization;
using RoomTrack.Housekeeping.Interfaces.REST.Resources;
using RoomTrack.Housekeeping.Interfaces.REST.Transform;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Housekeeping.Interfaces.REST;

/// <summary>
///     Cleaning tasks of the rooms: the front desk assigns them to housekeeping, manually or automatically by workload
///     (US-05), and housekeeping updates them from their task list (US-06). Starting a task takes its room to Cleaning
///     and completing it makes the room Available; every change is recorded in the task's history.
/// </summary>
[Authorize(Policy = Policies.ViewCleaningTasks)]
[ApiController]
[Route("api/v1/cleaning-tasks")]
[Produces("application/json")]
[SwaggerTag("Cleaning tasks of the rooms")]
public class CleaningTasksController(
    ICleaningTaskCommandService cleaningTaskCommandService,
    ICleaningTaskQueryService cleaningTaskQueryService,
    IRoomQueryService roomQueryService,
    IAuthorizationService authorizationService) : ControllerBase
{
    /// <summary>Creates the cleaning task of a room.</summary>
    /// <remarks>
    ///     Reception and administrators create tasks for the rooms of their hotel; a room has at most one open task
    ///     (409 otherwise). With <c>assigneeUserId</c> it goes to that housekeeping staff member; without it, it is
    ///     assigned to the one with the fewest open tasks when the hotel has the automatic assignment on
    ///     (<c>PUT /cleaning-tasks/settings</c>), and left unassigned otherwise. Rooms that go to Cleaning get a task
    ///     automatically, so this is for extra cleanings. The assignee is notified.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Policies.ManageCleaningTasks)]
    [SwaggerOperation(Summary = "Create a cleaning task", OperationId = "CreateCleaningTask")]
    [ProducesResponseType(typeof(CleaningTaskResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCleaningTask([FromBody] CreateCleaningTaskResource resource)
    {
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(resource.RoomId!.Value));
        if (room is null) return NotFound();
        if (!(await authorizationService.AuthorizeAsync(User, room, RoomOperationsRequirement.Instance)).Succeeded)
            return Forbid();

        var task = await cleaningTaskCommandService.Handle(new CreateCleaningTaskCommand(room.Id, resource.ToPriority(),
            resource.Notes, resource.AssigneeUserId, Actor()));
        return CreatedAtAction(nameof(GetCleaningTask), new { cleaningTaskId = task.Id }, CleaningTaskResourceAssembler.ToResource(task));
    }

    /// <summary>Cleaning tasks of a hotel, most urgent first (Urgent → Low, then the oldest).</summary>
    /// <remarks>
    ///     Hotel staff see their hotel (<c>hotelId</c> optional); a chain admin must send <c>hotelId</c>.
    ///     <c>assignedToMe=true&amp;open=true</c> is the housekeeping task list of US-06. The history is only returned
    ///     by <c>GET /cleaning-tasks/{id}</c>.
    /// </remarks>
    /// <param name="hotelId">The hotel.</param>
    /// <param name="status">Pending, InProgress, Completed or Cancelled.</param>
    /// <param name="priority">Low, Normal, High or Urgent.</param>
    /// <param name="roomId">Only the tasks of this room.</param>
    /// <param name="assigneeUserId">Only the tasks assigned to this user.</param>
    /// <param name="assignedToMe">Only the tasks assigned to the requester.</param>
    /// <param name="open">Only Pending and InProgress tasks.</param>
    /// <param name="limit">At most this many (1-500, 100 by default).</param>
    [HttpGet]
    [SwaggerOperation(Summary = "List the cleaning tasks of a hotel", OperationId = "GetCleaningTasks")]
    [ProducesResponseType(typeof(IEnumerable<CleaningTaskResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCleaningTasks([FromQuery] int? hotelId, [FromQuery] CleaningTaskStatus? status,
        [FromQuery] CleaningTaskPriority? priority, [FromQuery] int? roomId, [FromQuery] int? assigneeUserId,
        [FromQuery] bool assignedToMe = false, [FromQuery] bool open = false, [FromQuery] int limit = 100)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;

        var tasks = await cleaningTaskQueryService.Handle(new GetCleaningTasksQuery(targetHotelId, status, priority, roomId,
            assignedToMe ? User.GetUserId() : assigneeUserId, open, limit));
        return Ok(tasks.Select(task => CleaningTaskResourceAssembler.ToResource(task, withHistory: false)));
    }

    /// <summary>How the hotel distributes its cleaning tasks.</summary>
    /// <param name="hotelId">The hotel (optional for hotel staff; required for a chain admin).</param>
    [HttpGet("settings")]
    [SwaggerOperation(Summary = "Get the housekeeping settings of a hotel", OperationId = "GetHousekeepingSettings")]
    [ProducesResponseType(typeof(HousekeepingSettingsResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHousekeepingSettings([FromQuery] int? hotelId)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var settings = await cleaningTaskQueryService.Handle(new GetHousekeepingSettingsQuery(targetHotelId));
        return Ok(CleaningTaskResourceAssembler.ToResource(settings));
    }

    /// <summary>Turns the automatic assignment of the cleaning tasks of a hotel on or off.</summary>
    /// <remarks>
    ///     Administrators only. With <c>autoAssign</c> on, every new task without an explicit assignee (including the
    ///     ones created when a room goes to Cleaning) goes to the active housekeeping staff member with the fewest
    ///     open tasks (US-05).
    /// </remarks>
    /// <param name="hotelId">The hotel (optional for a hotel admin; required for a chain admin).</param>
    /// <param name="resource">The settings.</param>
    [HttpPut("settings")]
    [Authorize(Policy = Policies.ManageHotels)]
    [SwaggerOperation(Summary = "Update the housekeeping settings of a hotel", OperationId = "UpdateHousekeepingSettings")]
    [ProducesResponseType(typeof(HousekeepingSettingsResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateHousekeepingSettings([FromQuery] int? hotelId,
        [FromBody] UpdateHousekeepingSettingsResource resource)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var settings = await cleaningTaskCommandService.Handle(
            new ConfigureHousekeepingCommand(targetHotelId, resource.AutoAssign!.Value, User.GetUserId()));
        return Ok(CleaningTaskResourceAssembler.ToResource(settings));
    }

    /// <summary>A cleaning task with its history and impediments.</summary>
    [HttpGet("{cleaningTaskId:int}")]
    [SwaggerOperation(Summary = "Get a cleaning task", OperationId = "GetCleaningTask")]
    [ProducesResponseType(typeof(CleaningTaskResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCleaningTask(int cleaningTaskId)
    {
        var (task, denied) = await FindInScopeAsync(cleaningTaskId);
        return denied ?? Ok(CleaningTaskResourceAssembler.ToResource(task!));
    }

    /// <summary>Assigns or reassigns a cleaning task.</summary>
    /// <remarks>
    ///     Reception and administrators assign (or reassign) it to any housekeeping staff member of the hotel, or,
    ///     without <c>assigneeUserId</c>, to the one with the fewest open tasks (someone other than the current
    ///     assignee when possible). Housekeeping can only take an unassigned task for themselves. A reassigned task
    ///     leaves the previous assignee's list; both are notified.
    /// </remarks>
    [HttpPost("{cleaningTaskId:int}/assign")]
    [Authorize(Policy = Policies.AssignCleaningTasks)]
    [SwaggerOperation(Summary = "Assign a cleaning task", OperationId = "AssignCleaningTask")]
    [ProducesResponseType(typeof(CleaningTaskResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> AssignCleaningTask(int cleaningTaskId, [FromBody] AssignCleaningTaskResource? resource) =>
        ExecuteInScopeAsync(cleaningTaskId, () =>
            cleaningTaskCommandService.Handle(new AssignCleaningTaskCommand(cleaningTaskId, resource?.AssigneeUserId, Actor())));

    /// <summary>Starts cleaning: Pending → InProgress, and the room goes to Cleaning.</summary>
    /// <remarks>
    ///     Only its assignee (or an administrator) starts it; a housekeeping staff member starting an unassigned task
    ///     takes it. The room's new status is visible to the whole hotel. 409 when the room is occupied or blocked by an
    ///     incident. Starting a task already in progress is a no-op (safe to replay from an offline queue).
    /// </remarks>
    [HttpPost("{cleaningTaskId:int}/start")]
    [Authorize(Policy = Policies.HandleCleaningTasks)]
    [SwaggerOperation(Summary = "Start a cleaning task", OperationId = "StartCleaningTask")]
    [ProducesResponseType(typeof(CleaningTaskResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> StartCleaningTask(int cleaningTaskId) =>
        ExecuteInScopeAsync(cleaningTaskId, () =>
            cleaningTaskCommandService.Handle(new StartCleaningTaskCommand(cleaningTaskId, Actor())));

    /// <summary>Finishes cleaning: InProgress → Completed, and the room becomes Available.</summary>
    /// <remarks>
    ///     Only its assignee (or an administrator) completes it. Reception is notified that the room can be assigned
    ///     to a new guest. 409 while an incident blocks the room (it is resolved first). Completing a task already
    ///     completed is a no-op (safe to replay from an offline queue).
    /// </remarks>
    [HttpPost("{cleaningTaskId:int}/complete")]
    [Authorize(Policy = Policies.HandleCleaningTasks)]
    [SwaggerOperation(Summary = "Complete a cleaning task", OperationId = "CompleteCleaningTask")]
    [ProducesResponseType(typeof(CleaningTaskResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> CompleteCleaningTask(int cleaningTaskId) =>
        ExecuteInScopeAsync(cleaningTaskId, () =>
            cleaningTaskCommandService.Handle(new CompleteCleaningTaskCommand(cleaningTaskId, Actor())));

    /// <summary>Reports a damage found while cleaning: an incident is created for maintenance and linked to the task.</summary>
    /// <remarks>
    ///     Only its assignee (or an administrator), on an open task. The incident follows <c>/incidents</c> (maintenance
    ///     is notified); with <c>blocksRoom</c> the room goes to Maintenance and the task cannot be completed until the
    ///     incident is resolved, when the room returns to Cleaning. The task lists the incident in
    ///     <c>impedimentIncidentIds</c>.
    /// </remarks>
    [HttpPost("{cleaningTaskId:int}/impediments")]
    [Authorize(Policy = Policies.HandleCleaningTasks)]
    [SwaggerOperation(Summary = "Report an impediment of a cleaning task", OperationId = "ReportCleaningImpediment")]
    [ProducesResponseType(typeof(CleaningTaskResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ReportCleaningImpediment(int cleaningTaskId, [FromBody] ReportCleaningImpedimentResource resource) =>
        ExecuteInScopeAsync(cleaningTaskId, () =>
            cleaningTaskCommandService.Handle(new ReportCleaningImpedimentCommand(cleaningTaskId, resource.Title!,
                resource.Description, resource.ToPriority(), resource.BlocksRoom, Actor())));

    /// <summary>Re-classifies the urgency of an open cleaning task.</summary>
    [HttpPut("{cleaningTaskId:int}/priority")]
    [Authorize(Policy = Policies.ManageCleaningTasks)]
    [SwaggerOperation(Summary = "Change the priority of a cleaning task", OperationId = "ChangeCleaningTaskPriority")]
    [ProducesResponseType(typeof(CleaningTaskResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ChangeCleaningTaskPriority(int cleaningTaskId, [FromBody] ChangeCleaningTaskPriorityResource resource) =>
        ExecuteInScopeAsync(cleaningTaskId, () =>
            cleaningTaskCommandService.Handle(new ChangeCleaningTaskPriorityCommand(cleaningTaskId, resource.ToPriority(), Actor())));

    /// <summary>Cancels an open cleaning task (the room status does not change).</summary>
    [HttpPost("{cleaningTaskId:int}/cancel")]
    [Authorize(Policy = Policies.ManageCleaningTasks)]
    [SwaggerOperation(Summary = "Cancel a cleaning task", OperationId = "CancelCleaningTask")]
    [ProducesResponseType(typeof(CleaningTaskResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> CancelCleaningTask(int cleaningTaskId, [FromBody] CancelCleaningTaskResource? resource) =>
        ExecuteInScopeAsync(cleaningTaskId, () =>
            cleaningTaskCommandService.Handle(new CancelCleaningTaskCommand(cleaningTaskId, resource?.Reason, Actor())));

    private CleaningTaskActor Actor() => new(User.GetUserId(), User.GetUsername(),
        User.IsHotelAdmin() || User.IsChainAdmin() ? CleaningTaskRole.Administrator
        : User.IsInRole(UserRoles.Reception) ? CleaningTaskRole.FrontDesk
        : CleaningTaskRole.Housekeeper);

    /// <summary>The hotel of the request (the requester's own unless a chain admin names one), or the 403 result.</summary>
    private (int HotelId, IActionResult? Denied) HotelInScope(int? hotelId)
    {
        var targetHotelId = hotelId ?? (User.IsChainAdmin() ? null : User.GetHotelId());
        if (targetHotelId is null)
            throw new InvalidFieldException("hotelId", AccommodationErrorCodes.HotelRequired, "Send the hotelId of the cleaning tasks.");
        if (!User.IsChainAdmin() && User.GetHotelId() != targetHotelId) return (0, Forbid());
        return (targetHotelId.Value, null);
    }

    /// <summary>The task, or the 404/403 result when it does not exist or is outside the requester's hotel.</summary>
    private async Task<(CleaningTask? Task, IActionResult? Denied)> FindInScopeAsync(int cleaningTaskId)
    {
        var task = await cleaningTaskQueryService.Handle(new GetCleaningTaskByIdQuery(cleaningTaskId));
        if (task is null) return (null, NotFound());
        if (!(await authorizationService.AuthorizeAsync(User, task, CleaningTaskScopeRequirement.Instance)).Succeeded)
            return (null, Forbid());
        return (task, null);
    }

    private async Task<IActionResult> ExecuteInScopeAsync(int cleaningTaskId, Func<Task<CleaningTask?>> command)
    {
        var (_, denied) = await FindInScopeAsync(cleaningTaskId);
        if (denied is not null) return denied;
        var task = await command();
        return task is null ? NotFound() : Ok(CleaningTaskResourceAssembler.ToResource(task));
    }
}
