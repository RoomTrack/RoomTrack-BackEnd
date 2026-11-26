using RoomTrack.Shared.Infrastructure.Interfaces.ASP.Validation;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using RoomTrack.Shifts.Application;
using RoomTrack.Shifts.Domain;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Shifts.Interfaces;

/// <summary>A new shift of an area (US-31).</summary>
public record DefineShiftResource : IValidatableObject
{
    /// <summary>The date (yyyy-MM-dd, today or later).</summary>
    /// <example>2026-12-01</example>
    [Required]
    public DateOnly? Date { get; init; }

    /// <summary>Morning (07-15), Afternoon (15-23) or Night (23-07), case-insensitive.</summary>
    /// <example>Morning</example>
    [Required]
    public string? Period { get; init; }

    /// <summary>FrontDesk, Housekeeping or Maintenance, case-insensitive.</summary>
    /// <example>Housekeeping</example>
    [Required]
    public string? Area { get; init; }

    /// <summary>People the area needs (1-50).</summary>
    /// <example>3</example>
    [Required]
    public int? MinimumStaff { get; init; }

    /// <summary>The staff members who work it (IAM user ids of the hotel's staff).</summary>
    public IReadOnlyList<int>? AssigneeUserIds { get; init; }

    public ShiftPeriod ToPeriod() => Enum.Parse<ShiftPeriod>(Period!, ignoreCase: true);

    public ShiftArea ToArea() => Enum.Parse<ShiftArea>(Area!, ignoreCase: true);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        ShiftEnumValidation.Validate<ShiftPeriod>(Period, "period").Concat(ShiftEnumValidation.Validate<ShiftArea>(Area, "area"));
}

internal static class ShiftEnumValidation
{
    public static IEnumerable<ValidationResult> Validate<TEnum>(string? value, string field) where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value)
            && (int.TryParse(value, out _) || !Enum.TryParse<TEnum>(value, true, out var parsed) || !Enum.IsDefined(parsed)))
            yield return new CodedValidationResult(ErrorCodes.FieldNotAllowed,
                $"{field} must be one of: {string.Join(", ", Enum.GetNames<TEnum>())}.", [field],
                new Dictionary<string, object?> { ["allowed"] = Enum.GetNames<TEnum>() });
    }
}

/// <summary>A staff member to add to a shift.</summary>
public record AssignShiftResource
{
    /// <summary>IAM user id of a staff member of the hotel.</summary>
    /// <example>7</example>
    [Required]
    public int? UserId { get; init; }
}

/// <summary>A request to exchange a shift with a colleague.</summary>
public record RequestShiftSwapResource
{
    /// <summary>The requester's shift.</summary>
    /// <example>41</example>
    [Required]
    public int? MyShiftId { get; init; }

    /// <summary>The colleague.</summary>
    /// <example>8</example>
    [Required]
    public int? ColleagueUserId { get; init; }

    /// <summary>The colleague's shift.</summary>
    /// <example>44</example>
    [Required]
    public int? ColleagueShiftId { get; init; }

    /// <summary>Why, optional (up to 300 characters).</summary>
    /// <example>Tengo cita médica en la mañana.</example>
    public string? Reason { get; init; }
}

/// <summary>A staff member of a shift.</summary>
/// <param name="UserId">IAM user id.</param>
/// <param name="DisplayName">Their name.</param>
public record ShiftAssigneeResource(int UserId, string DisplayName);

/// <summary>A shift of an area.</summary>
/// <param name="Id">Shift id.</param>
/// <param name="HotelId">The hotel.</param>
/// <param name="Date">The date.</param>
/// <param name="Period">Morning, Afternoon or Night.</param>
/// <param name="Area">FrontDesk, Housekeeping or Maintenance.</param>
/// <param name="MinimumStaff">People the area needs.</param>
/// <param name="CoverageGap">People missing (0: covered).</param>
/// <param name="Assignees">Who works it.</param>
public record WorkShiftResource(int Id, int HotelId, DateOnly Date, string Period, string Area, int MinimumStaff, int CoverageGap,
    IReadOnlyList<ShiftAssigneeResource> Assignees);

/// <summary>A shift without enough people (US-31).</summary>
/// <param name="Shift">The shift.</param>
/// <param name="Missing">People missing.</param>
/// <param name="IsCritical">Critical area (front desk, maintenance) or nobody assigned: highlighted first.</param>
public record CoverageGapResource(WorkShiftResource Shift, int Missing, bool IsCritical);

/// <summary>A shift swap request.</summary>
/// <param name="Id">Request id.</param>
/// <param name="RequesterUserId">Who asked.</param>
/// <param name="RequesterShiftId">The shift they give away.</param>
/// <param name="ColleagueUserId">The colleague.</param>
/// <param name="ColleagueShiftId">The colleague's shift.</param>
/// <param name="Reason">Why.</param>
/// <param name="Status">Pending, Approved or Rejected.</param>
/// <param name="RequestedAt">When.</param>
/// <param name="DecidedByUserId">The administrator who decided.</param>
/// <param name="DecidedAt">When.</param>
public record ShiftSwapResource(int Id, int RequesterUserId, int RequesterShiftId, int ColleagueUserId, int ColleagueShiftId,
    string? Reason, string Status, DateTimeOffset RequestedAt, int? DecidedByUserId, DateTimeOffset? DecidedAt);

/// <summary>
///     Staff shifts (US-31): the administrators define the shifts of each area and assign the staff, review the
///     coverage gaps and approve shift swaps; every staff member sees their own schedule on their panel.
/// </summary>
[Authorize(Policy = Policies.UseShifts)]
[ApiController]
[Produces("application/json")]
[SwaggerTag("Staff shifts, coverage and shift swaps")]
public class ShiftsController(ShiftService shiftService) : ControllerBase
{
    private const string ShiftExample = """
        { "id": 41, "hotelId": 1, "date": "2026-12-01", "period": "Morning", "area": "Housekeeping", "minimumStaff": 3,
          "coverageGap": 1, "assignees": [ { "userId": 7, "displayName": "Rosa Quispe" }, { "userId": 8, "displayName": "Ana Flores" } ] }
        """;

    /// <summary>Defines a shift of an area and assigns its staff.</summary>
    /// <remarks>
    ///     Administrators of the hotel. Each assignee must be an active staff member of the hotel (staff profile with a
    ///     current assignment) not working another shift of the same period that day. One shift per area, date and
    ///     period (409 otherwise).
    /// </remarks>
    [HttpPost("api/v1/shifts")]
    [Authorize(Policy = Policies.ManageShifts)]
    [SwaggerOperation(Summary = "Define a shift", OperationId = "DefineShift")]
    [ProducesResponseType(typeof(WorkShiftResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "date": "2026-12-01", "period": "Morning", "area": "Housekeeping", "minimumStaff": 3, "assigneeUserIds": [7, 8] }""")]
    [OpenApiResponseExample(StatusCodes.Status201Created, ShiftExample)]
    public async Task<IActionResult> DefineShift([FromQuery] int? hotelId, [FromBody] DefineShiftResource resource)
    {
        var shift = await shiftService.DefineAsync(HotelOf(hotelId), resource.Date!.Value, resource.ToPeriod(), resource.ToArea(),
            resource.MinimumStaff!.Value, resource.AssigneeUserIds ?? [], User.GetUserId());
        return CreatedAtAction(nameof(GetShift), new { shiftId = shift.Id }, ToResource(shift));
    }

    /// <summary>A shift.</summary>
    [HttpGet("api/v1/shifts/{shiftId:int}")]
    [SwaggerOperation(Summary = "Get a shift", OperationId = "GetShift")]
    [ProducesResponseType(typeof(WorkShiftResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetShift(int shiftId)
    {
        var shift = await shiftService.FindAsync(shiftId);
        return shift is null || !InScope(shift.HotelId) ? NotFound() : Ok(ToResource(shift));
    }

    /// <summary>Adds a staff member to a shift.</summary>
    [HttpPost("api/v1/shifts/{shiftId:int}/assignees")]
    [Authorize(Policy = Policies.ManageShifts)]
    [SwaggerOperation(Summary = "Assign a staff member to a shift", OperationId = "AssignShift")]
    [ProducesResponseType(typeof(WorkShiftResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "userId": 9 }""")]
    public async Task<IActionResult> AssignShift(int shiftId, [FromBody] AssignShiftResource resource)
    {
        if (await shiftService.FindAsync(shiftId) is not { } existing || !InScope(existing.HotelId)) return NotFound();
        var shift = await shiftService.AssignAsync(shiftId, resource.UserId!.Value);
        return shift is null ? NotFound() : Ok(ToResource(shift));
    }

    /// <summary>Removes a staff member from a shift.</summary>
    [HttpDelete("api/v1/shifts/{shiftId:int}/assignees/{userId:int}")]
    [Authorize(Policy = Policies.ManageShifts)]
    [SwaggerOperation(Summary = "Remove a staff member from a shift", OperationId = "UnassignShift")]
    [ProducesResponseType(typeof(WorkShiftResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnassignShift(int shiftId, int userId)
    {
        if (await shiftService.FindAsync(shiftId) is not { } existing || !InScope(existing.HotelId)) return NotFound();
        var shift = await shiftService.UnassignAsync(shiftId, userId);
        return shift is null ? NotFound() : Ok(ToResource(shift));
    }

    /// <summary>The shift schedule of a hotel between two dates.</summary>
    /// <param name="hotelId">The hotel (optional for hotel staff; required for a chain admin).</param>
    /// <param name="from">From (yyyy-MM-dd; today by default).</param>
    /// <param name="to">To (yyyy-MM-dd; 7 days after <c>from</c> by default).</param>
    /// <param name="area">Only this area.</param>
    [HttpGet("api/v1/shifts")]
    [SwaggerOperation(Summary = "Get the shift schedule of a hotel", OperationId = "GetShiftSchedule")]
    [ProducesResponseType(typeof(IEnumerable<WorkShiftResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [OpenApiResponseExample(StatusCodes.Status200OK, "[" + ShiftExample + "]")]
    public async Task<IActionResult> GetShiftSchedule([FromQuery] int? hotelId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] ShiftArea? area)
    {
        var (since, until) = Range(from, to);
        var shifts = await shiftService.ScheduleAsync(HotelOf(hotelId), since, until, area);
        return Ok(shifts.Select(ToResource));
    }

    /// <summary>The requester's own shifts: what each staff member sees on their panel (US-31).</summary>
    [HttpGet("api/v1/shifts/mine")]
    [SwaggerOperation(Summary = "Get my shifts", OperationId = "GetMyShifts")]
    [ProducesResponseType(typeof(IEnumerable<WorkShiftResource>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyShifts([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (since, until) = Range(from, to);
        return Ok((await shiftService.ScheduleOfAsync(User.GetUserId(), since, until)).Select(ToResource));
    }

    /// <summary>The coverage gaps of a hotel: shifts with fewer people than they need, critical areas first (US-31).</summary>
    [HttpGet("api/v1/shifts/coverage-gaps")]
    [Authorize(Policy = Policies.ManageShifts)]
    [SwaggerOperation(Summary = "Get the coverage gaps of the schedule", OperationId = "GetCoverageGaps")]
    [ProducesResponseType(typeof(IEnumerable<CoverageGapResource>), StatusCodes.Status200OK)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        [ { "shift": { "id": 45, "hotelId": 1, "date": "2026-12-01", "period": "Night", "area": "FrontDesk", "minimumStaff": 1,
                       "coverageGap": 1, "assignees": [] },
            "missing": 1, "isCritical": true } ]
        """)]
    public async Task<IActionResult> GetCoverageGaps([FromQuery] int? hotelId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (since, until) = Range(from, to);
        var gaps = await shiftService.CoverageGapsAsync(HotelOf(hotelId), since, until);
        return Ok(gaps.Select(gap => new CoverageGapResource(ToResource(gap.Shift), gap.Missing, gap.IsCritical)));
    }

    /// <summary>Asks to exchange one of the requester's shifts with a colleague's (US-31).</summary>
    [HttpPost("api/v1/shift-swaps")]
    [SwaggerOperation(Summary = "Request a shift swap", OperationId = "RequestShiftSwap")]
    [ProducesResponseType(typeof(ShiftSwapResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "myShiftId": 41, "colleagueUserId": 9, "colleagueShiftId": 44, "reason": "Tengo cita médica en la mañana." }""")]
    public async Task<IActionResult> RequestShiftSwap([FromBody] RequestShiftSwapResource resource)
    {
        if (await shiftService.FindAsync(resource.MyShiftId!.Value) is not { } mine || !InScope(mine.HotelId)) return NotFound();
        var request = await shiftService.RequestSwapAsync(User.GetUserId(), mine.Id, resource.ColleagueUserId!.Value,
            resource.ColleagueShiftId!.Value, resource.Reason);
        return CreatedAtAction(nameof(GetShiftSwaps), null, ToResource(request));
    }

    /// <summary>The shift swap requests of a hotel (administrators) or the requester's own (staff).</summary>
    [HttpGet("api/v1/shift-swaps")]
    [SwaggerOperation(Summary = "List shift swap requests", OperationId = "GetShiftSwaps")]
    [ProducesResponseType(typeof(IEnumerable<ShiftSwapResource>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShiftSwaps([FromQuery] int? hotelId, [FromQuery] ShiftSwapStatus? status)
    {
        var mineOnly = !(User.IsHotelAdmin() || User.IsChainAdmin());
        var requests = await shiftService.SwapsAsync(HotelOf(hotelId), status, mineOnly ? User.GetUserId() : null);
        return Ok(requests.Select(ToResource));
    }

    /// <summary>Approves a shift swap: both calendars are updated (US-31).</summary>
    [HttpPost("api/v1/shift-swaps/{requestId:int}/approve")]
    [Authorize(Policy = Policies.ManageShifts)]
    [SwaggerOperation(Summary = "Approve a shift swap", OperationId = "ApproveShiftSwap")]
    [ProducesResponseType(typeof(ShiftSwapResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveShiftSwap(int requestId)
    {
        if (await shiftService.FindSwapAsync(requestId) is not { } existing || !InScope(existing.HotelId)) return NotFound();
        var request = await shiftService.ApproveSwapAsync(requestId, User.GetUserId());
        return request is null ? NotFound() : Ok(ToResource(request));
    }

    /// <summary>Rejects a shift swap.</summary>
    [HttpPost("api/v1/shift-swaps/{requestId:int}/reject")]
    [Authorize(Policy = Policies.ManageShifts)]
    [SwaggerOperation(Summary = "Reject a shift swap", OperationId = "RejectShiftSwap")]
    [ProducesResponseType(typeof(ShiftSwapResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectShiftSwap(int requestId)
    {
        if (await shiftService.FindSwapAsync(requestId) is not { } existing || !InScope(existing.HotelId)) return NotFound();
        var request = await shiftService.RejectSwapAsync(requestId, User.GetUserId());
        return request is null ? NotFound() : Ok(ToResource(request));
    }

    private bool InScope(int hotelId) => User.IsChainAdmin() || User.GetHotelId() == hotelId;

    private int HotelOf(int? hotelId)
    {
        var target = hotelId ?? (User.IsChainAdmin() ? null : User.GetHotelId())
                     ?? throw new InvalidFieldException("hotelId", ErrorCodes.FieldRequired, "Send the hotelId of the shifts.");
        if (!InScope(target))
            throw new InvalidFieldException("hotelId", ErrorCodes.FieldNotAllowed, "You can only manage the shifts of your hotel.");
        return target;
    }

    private (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to)
    {
        var since = from ?? shiftService.Today;
        var until = to ?? since.AddDays(7);
        if (until < since || until.DayNumber - since.DayNumber > 62)
            throw new InvalidFieldException("to", ErrorCodes.InvalidRequest, "Ask for at most 62 days, 'to' after 'from'.");
        return (since, until);
    }

    private static WorkShiftResource ToResource(WorkShift shift) =>
        new(shift.Id, shift.HotelId, shift.Date, shift.Period.ToString(), shift.Area.ToString(), shift.MinimumStaff, shift.CoverageGap,
            shift.Assignees.Select(assignee => new ShiftAssigneeResource(assignee.UserId, assignee.DisplayName)).ToList());

    private static ShiftSwapResource ToResource(ShiftSwapRequest request) =>
        new(request.Id, request.RequesterUserId, request.RequesterShiftId, request.ColleagueUserId, request.ColleagueShiftId,
            request.Reason, request.Status.ToString(), request.RequestedAt, request.DecidedByUserId, request.DecidedAt);
}
