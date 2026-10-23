using BackendAwRoomTrack.API.Incidents.Domain.Model.Commands;
using BackendAwRoomTrack.API.Incidents.Domain.Services;
using BackendAwRoomTrack.API.Incidents.Interfaces.REST.Resources;
using BackendAwRoomTrack.API.Shared.Infrastructure.Authentication.ScheduledJobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BackendAwRoomTrack.API.Incidents.Interfaces.REST;

/// <summary>
///     Scheduled jobs of the incidents, called by the external scheduler with <c>X-Cron-Key</c> (kept apart from
///     <see cref="IncidentsController"/>, whose user-token authorization would otherwise be combined with it).
/// </summary>
[ApiController]
[Route("api/v1/incidents")]
[Produces("application/json")]
[Tags("Incidents")]
public class IncidentJobsController(IIncidentCommandService incidentCommandService) : ControllerBase
{
    /// <summary>Alerts the hotel administrators of critical incidents nobody started in time (US-09).</summary>
    /// <remarks>
    ///     Every Critical incident still Pending <c>Incidents__CriticalEscalationAfterMinutes</c> (60) after its report:
    ///     its hotel admins (or the chain admins, if the hotel has none) get an e-mail. Idempotent: one alert per
    ///     incident. Run it hourly.
    /// </remarks>
    [HttpPost("escalations")]
    [Authorize(Policy = ScheduledJobsAuthenticationExtensions.RunScheduledJobsPolicy)]
    [SwaggerOperation(Summary = "Escalate late critical incidents", OperationId = "EscalateLateIncidents")]
    [ProducesResponseType(typeof(IncidentEscalationsResultResource), StatusCodes.Status200OK)]
    public async Task<IActionResult> EscalateLateIncidents()
    {
        var escalated = await incidentCommandService.Handle(new EscalateLateIncidentsCommand());
        return Ok(new IncidentEscalationsResultResource(escalated));
    }
}
