using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.Accommodations.Interfaces.REST.Resources;
using RoomTrack.Shared.Infrastructure.Authentication.ScheduledJobs;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Accommodations.Interfaces.REST;

/// <summary>
///     Scheduled jobs of the rooms, called by the external scheduler with <c>X-Cron-Key</c> (kept apart from
///     <see cref="RoomsController"/>, whose user-token authorization would otherwise be combined with it).
/// </summary>
[ApiController]
[Route("api/v1/rooms")]
[Produces("application/json")]
[Tags("Rooms")]
public class RoomJobsController(IRoomCommandService roomCommandService) : ControllerBase
{
    /// <summary>Alerts the hotel administrators of rooms under maintenance for too long.</summary>
    /// <remarks>
    ///     Every room in Maintenance for at least <c>Rooms__MaintenanceAlertAfterHours</c> (24 h) whose current
    ///     maintenance period was not alerted yet: its hotel admins (or the chain admins, if the hotel has none) get an
    ///     e-mail. Idempotent: one alert per maintenance period; a new period (the room left Maintenance and came back)
    ///     can be alerted again. Run it hourly.
    /// </remarks>
    [HttpPost("maintenance-alerts")]
    [Authorize(Policy = ScheduledJobsAuthenticationExtensions.RunScheduledJobsPolicy)]
    [SwaggerOperation(Summary = "Alert rooms under maintenance for too long", OperationId = "RaiseMaintenanceAlerts")]
    [ProducesResponseType(typeof(MaintenanceAlertsResultResource), StatusCodes.Status200OK)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """{ "alerted": 1 }""")]
    public async Task<IActionResult> RaiseMaintenanceAlerts()
    {
        var alerted = await roomCommandService.Handle(new RaiseMaintenanceAlertsCommand());
        return Ok(new MaintenanceAlertsResultResource(alerted));
    }

    /// <summary>Alerts the hotel of rooms not ready for a guest who arrives soon (US-23).</summary>
    /// <remarks>
    ///     Every paid booking whose check-in day is today and whose check-in time (<c>Bookings__CheckInTime</c>, 15:00)
    ///     is at most <c>Rooms__ArrivalAlertLeadMinutes</c> (120) away, or already passed while the guest has not
    ///     checked in: when its room is not Available (still being cleaned, the previous guest has not left, or under
    ///     maintenance) the hotel administrators and the front desk get a push notification and an e-mail. Idempotent:
    ///     one alert per arrival. Run it hourly (with the 120-minute lead the hotel learns it 1-2 hours ahead).
    /// </remarks>
    [HttpPost("arrival-readiness-alerts")]
    [Authorize(Policy = ScheduledJobsAuthenticationExtensions.RunScheduledJobsPolicy)]
    [SwaggerOperation(Summary = "Alert rooms not ready for an arriving guest", OperationId = "RaiseArrivalReadinessAlerts")]
    [ProducesResponseType(typeof(ArrivalReadinessAlertsResultResource), StatusCodes.Status200OK)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """{ "alerted": 2 }""")]
    public async Task<IActionResult> RaiseArrivalReadinessAlerts()
    {
        var alerted = await roomCommandService.Handle(new RaiseArrivalReadinessAlertsCommand());
        return Ok(new ArrivalReadinessAlertsResultResource(alerted));
    }
}
