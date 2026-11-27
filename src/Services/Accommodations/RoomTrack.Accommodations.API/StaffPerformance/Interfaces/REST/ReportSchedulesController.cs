using System.ComponentModel.DataAnnotations;
using RoomTrack.Accommodations.Domain.Model.Exceptions;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Authentication.ScheduledJobs;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using RoomTrack.StaffPerformance.Domain.Model.Aggregates;
using RoomTrack.StaffPerformance.Domain.Model.Commands;
using RoomTrack.StaffPerformance.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.StaffPerformance.Interfaces.REST;

/// <summary>
///     Automatic delivery of the staff reports by e-mail (US-34): an administrator chooses the reports (productivity,
///     service times), the frequency (weekly on Mondays, monthly on the 1st) and the recipients.
/// </summary>
[Authorize(Policy = Policies.ViewAnalytics)]
[ApiController]
[Route("api/v1/staff-performance/report-schedules")]
[Produces("application/json")]
[SwaggerTag("Scheduled delivery of the staff reports by e-mail")]
public class ReportSchedulesController(
    IReportScheduleCommandService commandService,
    IReportScheduleQueryService queryService) : ControllerBase
{
    /// <summary>The report schedules of a hotel.</summary>
    /// <param name="hotelId">The hotel (optional for a hotel administrator; required for a chain admin).</param>
    [HttpGet]
    [SwaggerOperation(Summary = "List the report schedules", OperationId = "GetReportSchedules")]
    [ProducesResponseType(typeof(IEnumerable<ReportScheduleResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReportSchedules([FromQuery] int? hotelId)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        return Ok((await queryService.ListByHotelAsync(targetHotelId)).Select(ReportScheduleResource.From));
    }

    /// <summary>Schedules the delivery of the staff reports by e-mail.</summary>
    /// <remarks>
    ///     Weekly: every Monday at 08:00 (Lima) with the previous 7 days. Monthly: the 1st at 08:00 with the previous month.
    ///     1 to 10 recipients and at least one report. Use <c>POST /{id}/send</c> to receive one right away.
    /// </remarks>
    /// <param name="hotelId">The hotel (optional for a hotel administrator; required for a chain admin).</param>
    /// <param name="resource">The schedule.</param>
    [HttpPost]
    [SwaggerOperation(Summary = "Schedule the staff reports", OperationId = "CreateReportSchedule")]
    [ProducesResponseType(typeof(ReportScheduleResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [OpenApiRequestExample("""
        { "frequency": "Weekly", "recipients": ["gerencia@hotel.pe", "operaciones@hotel.pe"],
          "includeProductivity": true, "includeServiceTimes": true, "isActive": true }
        """)]
    public async Task<IActionResult> CreateReportSchedule([FromQuery] int? hotelId, [FromBody] SaveReportScheduleResource resource)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var schedule = await commandService.Handle(new CreateReportScheduleCommand(targetHotelId, resource.ToSettings(),
            User.GetUserId()));
        return CreatedAtAction(nameof(GetReportSchedule), new { reportScheduleId = schedule.Id }, ReportScheduleResource.From(schedule));
    }

    /// <summary>A report schedule.</summary>
    [HttpGet("{reportScheduleId:int}")]
    [SwaggerOperation(Summary = "Get a report schedule", OperationId = "GetReportSchedule")]
    [ProducesResponseType(typeof(ReportScheduleResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReportSchedule(int reportScheduleId)
    {
        var schedule = await FindInScopeAsync(reportScheduleId);
        return schedule is null ? NotFound() : Ok(ReportScheduleResource.From(schedule));
    }

    /// <summary>Changes a report schedule (<c>isActive: false</c> pauses it).</summary>
    [HttpPut("{reportScheduleId:int}")]
    [SwaggerOperation(Summary = "Update a report schedule", OperationId = "UpdateReportSchedule")]
    [ProducesResponseType(typeof(ReportScheduleResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReportSchedule(int reportScheduleId, [FromBody] SaveReportScheduleResource resource)
    {
        if (await FindInScopeAsync(reportScheduleId) is null) return NotFound();
        var schedule = await commandService.Handle(new UpdateReportScheduleCommand(reportScheduleId, resource.ToSettings()));
        return schedule is null ? NotFound() : Ok(ReportScheduleResource.From(schedule));
    }

    /// <summary>Removes a report schedule.</summary>
    [HttpDelete("{reportScheduleId:int}")]
    [SwaggerOperation(Summary = "Remove a report schedule", OperationId = "DeleteReportSchedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReportSchedule(int reportScheduleId)
    {
        if (await FindInScopeAsync(reportScheduleId) is null) return NotFound();
        return await commandService.Handle(new DeleteReportScheduleCommand(reportScheduleId)) ? NoContent() : NotFound();
    }

    /// <summary>Sends the reports of a schedule right away, with the last week or month until now.</summary>
    /// <remarks>Its next automatic delivery does not change.</remarks>
    [HttpPost("{reportScheduleId:int}/send")]
    [SwaggerOperation(Summary = "Send the reports of a schedule now", OperationId = "SendReportScheduleNow")]
    [ProducesResponseType(typeof(ReportScheduleResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendReportScheduleNow(int reportScheduleId)
    {
        if (await FindInScopeAsync(reportScheduleId) is null) return NotFound();
        var schedule = await commandService.Handle(new SendReportNowCommand(reportScheduleId));
        return schedule is null ? NotFound() : Ok(ReportScheduleResource.From(schedule));
    }

    private (int HotelId, IActionResult? Denied) HotelInScope(int? hotelId)
    {
        var targetHotelId = hotelId ?? (User.IsChainAdmin() ? null : User.GetHotelId());
        if (targetHotelId is null)
            throw new InvalidFieldException("hotelId", AccommodationErrorCodes.HotelRequired, "Send the hotelId of the schedules.");
        if (!User.IsChainAdmin() && User.GetHotelId() != targetHotelId) return (0, Forbid());
        return (targetHotelId.Value, null);
    }

    /// <summary>The schedule, when it exists and belongs to a hotel the requester administers.</summary>
    private async Task<ReportSchedule?> FindInScopeAsync(int reportScheduleId)
    {
        var schedule = await queryService.FindByIdAsync(reportScheduleId);
        return schedule is not null && (User.IsChainAdmin() || User.GetHotelId() == schedule.HotelId) ? schedule : null;
    }
}

/// <summary>Scheduled job of the reports, called by the external scheduler with <c>X-Cron-Key</c>.</summary>
[ApiController]
[Route("api/v1/staff-performance/report-schedules")]
[Produces("application/json")]
[Tags("ReportSchedules")]
public class ReportScheduleJobsController(IReportScheduleCommandService commandService) : ControllerBase
{
    /// <summary>Sends every scheduled report that is due (US-34). Run it hourly.</summary>
    [HttpPost("runs")]
    [Authorize(Policy = ScheduledJobsAuthenticationExtensions.RunScheduledJobsPolicy)]
    [SwaggerOperation(Summary = "Send the due scheduled reports", OperationId = "SendDueReports")]
    [ProducesResponseType(typeof(ReportRunResultResource), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendDueReports() =>
        Ok(new ReportRunResultResource(await commandService.Handle(new SendDueReportsCommand())));
}

/// <summary>How many scheduled reports were sent.</summary>
public record ReportRunResultResource(int Sent);

/// <summary>A report schedule, as the administrator writes it.</summary>
public record SaveReportScheduleResource
{
    /// <summary>Weekly (Mondays, previous 7 days) or Monthly (the 1st, previous month).</summary>
    /// <example>Weekly</example>
    [Required]
    public ReportFrequency? Frequency { get; init; }

    /// <summary>1 to 10 e-mails.</summary>
    [Required]
    public IReadOnlyList<string>? Recipients { get; init; }

    /// <summary>Include the productivity of the staff (US-24).</summary>
    public bool IncludeProductivity { get; init; } = true;

    /// <summary>Include the service times (US-25).</summary>
    public bool IncludeServiceTimes { get; init; } = true;

    /// <summary>False pauses the deliveries.</summary>
    public bool IsActive { get; init; } = true;

    public ReportScheduleSettings ToSettings() =>
        new(Frequency!.Value, Recipients ?? [], IncludeProductivity, IncludeServiceTimes, IsActive);
}

/// <summary>A report schedule of a hotel.</summary>
public record ReportScheduleResource(
    int Id,
    int HotelId,
    ReportFrequency Frequency,
    IReadOnlyList<string> Recipients,
    bool IncludeProductivity,
    bool IncludeServiceTimes,
    bool IsActive,
    DateTimeOffset NextRunAt,
    DateTimeOffset? LastSentAt)
{
    public static ReportScheduleResource From(ReportSchedule schedule) => new(schedule.Id, schedule.HotelId, schedule.Frequency,
        schedule.Recipients, schedule.IncludeProductivity, schedule.IncludeServiceTimes, schedule.IsActive, schedule.NextRunAt,
        schedule.LastSentAt);
}
