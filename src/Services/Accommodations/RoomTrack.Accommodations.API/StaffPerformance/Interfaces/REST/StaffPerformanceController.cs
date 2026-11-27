using RoomTrack.Accommodations.Domain.Model.Exceptions;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using RoomTrack.Shared.Infrastructure.Interfaces.ASP.Csv;
using RoomTrack.StaffPerformance.Domain.Model.Exceptions;
using RoomTrack.StaffPerformance.Domain.Model.Queries;
using RoomTrack.StaffPerformance.Domain.Model.ValueObjects;
using RoomTrack.StaffPerformance.Domain.Services;
using RoomTrack.StaffPerformance.Interfaces.REST.Resources;
using RoomTrack.StaffPerformance.Interfaces.REST.Transform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.StaffPerformance.Interfaces.REST;

/// <summary>
///     Performance of the staff of a hotel: productivity by staff member and area (US-24) and average service times
///     (US-25), calculated from the cleaning tasks, incidents and guest requests finished in a period.
/// </summary>
[Authorize(Policy = Policies.ViewAnalytics)]
[ApiController]
[Route("api/v1/staff-performance")]
[Produces("application/json")]
[SwaggerTag("Staff productivity and service times of a hotel")]
public class StaffPerformanceController(IStaffPerformanceQueryService queryService, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>The longest period a report covers.</summary>
    public const int MaxPeriodDays = 366;

    /// <summary>The work finished by each staff member and each area (US-24).</summary>
    /// <remarks>
    ///     Counts the cleaning tasks completed, the incidents resolved and the guest requests resolved in the period,
    ///     by who finished them (most productive first) and by area (FrontDesk, Housekeeping, Maintenance; always the
    ///     three, to compare them). Administrators of the hotel or chain administrators. Without dates: the last 30 days.
    /// </remarks>
    /// <param name="hotelId">The hotel (optional for a hotel administrator; required for a chain admin).</param>
    /// <param name="from">From (ISO 8601, inclusive).</param>
    /// <param name="to">To (ISO 8601, exclusive). By default, now.</param>
    [HttpGet("productivity")]
    [SwaggerOperation(Summary = "Get the productivity of the staff", OperationId = "GetStaffProductivity")]
    [ProducesResponseType(typeof(ProductivityReportResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "hotelId": 1, "from": "2026-11-01T00:00:00+00:00", "to": "2026-12-01T00:00:00+00:00", "totalCompleted": 5,
          "collaborators": [
            { "userId": 7, "fullName": "Rosa Quispe", "email": "rosa@roomtrack.pe", "role": "Housekeeping",
              "cleaningTasksCompleted": 3, "incidentsResolved": 0, "guestRequestsResolved": 1, "totalCompleted": 4,
              "averageWorkMinutes": 32.5 },
            { "userId": 20, "fullName": "Jorge Ramos", "email": "jorge@roomtrack.pe", "role": "Maintenance",
              "cleaningTasksCompleted": 0, "incidentsResolved": 1, "guestRequestsResolved": 0, "totalCompleted": 1,
              "averageWorkMinutes": 45 } ],
          "areas": [
            { "area": "FrontDesk", "completed": 0, "collaborators": 0, "averagePerCollaborator": 0, "averageWorkMinutes": null },
            { "area": "Housekeeping", "completed": 4, "collaborators": 1, "averagePerCollaborator": 4, "averageWorkMinutes": 32.5 },
            { "area": "Maintenance", "completed": 1, "collaborators": 1, "averagePerCollaborator": 1, "averageWorkMinutes": 45 } ] }
        """)]
    public async Task<IActionResult> GetStaffProductivity([FromQuery] int? hotelId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var (start, end) = Period(from, to);
        var report = await queryService.Handle(new GetStaffProductivityQuery(targetHotelId, start, end));
        return Ok(StaffPerformanceResourceAssembler.ToResource(report));
    }

    /// <summary>How long it takes on average to clean a room, resolve an incident and attend a guest request (US-25).</summary>
    /// <remarks>
    ///     For the work finished in the period, in minutes: the response time (created or reported until started), the
    ///     work time (started until finished) and the total time (created or reported until finished). Incidents are only
    ///     the corrective ones: preventive maintenance is generated ahead of its due date. Administrators of the hotel or
    ///     chain administrators. Without dates: the last 30 days.
    /// </remarks>
    /// <param name="hotelId">The hotel (optional for a hotel administrator; required for a chain admin).</param>
    /// <param name="from">From (ISO 8601, inclusive).</param>
    /// <param name="to">To (ISO 8601, exclusive). By default, now.</param>
    [HttpGet("service-times")]
    [SwaggerOperation(Summary = "Get the average service times", OperationId = "GetServiceTimes")]
    [ProducesResponseType(typeof(ServiceTimesReportResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "hotelId": 1, "from": "2026-11-01T00:00:00+00:00", "to": "2026-12-01T00:00:00+00:00",
          "cleaning": { "count": 3, "averageResponseMinutes": 12.3, "averageWorkMinutes": 31.7, "averageTotalMinutes": 44, "longestTotalMinutes": 70 },
          "incidents": { "count": 1, "averageResponseMinutes": 20, "averageWorkMinutes": 45, "averageTotalMinutes": 65, "longestTotalMinutes": 65 },
          "guestRequests": { "count": 1, "averageResponseMinutes": 5, "averageWorkMinutes": 10, "averageTotalMinutes": 15, "longestTotalMinutes": 15 },
          "guestRequestsByArea": [
            { "area": "FrontDesk", "count": 0, "averageResponseMinutes": null, "averageWorkMinutes": null, "averageTotalMinutes": null, "longestTotalMinutes": null },
            { "area": "Housekeeping", "count": 1, "averageResponseMinutes": 5, "averageWorkMinutes": 10, "averageTotalMinutes": 15, "longestTotalMinutes": 15 },
            { "area": "Maintenance", "count": 0, "averageResponseMinutes": null, "averageWorkMinutes": null, "averageTotalMinutes": null, "longestTotalMinutes": null } ] }
        """)]
    public async Task<IActionResult> GetServiceTimes([FromQuery] int? hotelId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var (start, end) = Period(from, to);
        var report = await queryService.Handle(new GetServiceTimesQuery(targetHotelId, start, end));
        return Ok(StaffPerformanceResourceAssembler.ToResource(report));
    }

    /// <summary>Downloads the productivity report as CSV (US-34).</summary>
    /// <remarks>
    ///     The same data as <c>GET /productivity</c>, one row per staff member (<c>tipo</c> = colaborador) and one per area
    ///     (<c>tipo</c> = area). UTF-8 with BOM, comma separated, decimal point.
    /// </remarks>
    /// <param name="hotelId">The hotel (optional for a hotel administrator; required for a chain admin).</param>
    /// <param name="from">From (ISO 8601, inclusive).</param>
    /// <param name="to">To (ISO 8601, exclusive). By default, now.</param>
    [HttpGet("productivity/export")]
    [SwaggerOperation(Summary = "Download the productivity of the staff as CSV", OperationId = "ExportStaffProductivity")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportStaffProductivity([FromQuery] int? hotelId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var (start, end) = Period(from, to);
        var report = await queryService.Handle(new GetStaffProductivityQuery(targetHotelId, start, end));

        var rows = report.Collaborators
            .Select(c => new object?[]
            {
                "colaborador", c.UserId, c.FullName, c.Email, c.Role, c.CleaningTasksCompleted, c.IncidentsResolved,
                c.GuestRequestsResolved, c.TotalCompleted, null, null, c.AverageWorkMinutes
            })
            .Concat(report.Areas.Select(a => new object?[]
            {
                "area", null, AreaLabel(a.Area), null, null, null, null, null, a.Completed, a.Collaborators,
                a.AveragePerCollaborator, a.AverageWorkMinutes
            }));
        return CsvFile.Download(FileName("productividad", targetHotelId, start, end),
        [
            "tipo", "id_usuario", "nombre", "email", "rol", "limpiezas", "incidencias", "solicitudes", "total",
            "colaboradores", "promedio_por_colaborador", "minutos_promedio_trabajo"
        ], rows);
    }

    /// <summary>Downloads the service-time indicators as CSV (US-34).</summary>
    /// <remarks>The same data as <c>GET /service-times</c>, one row per indicator, in minutes.</remarks>
    /// <param name="hotelId">The hotel (optional for a hotel administrator; required for a chain admin).</param>
    /// <param name="from">From (ISO 8601, inclusive).</param>
    /// <param name="to">To (ISO 8601, exclusive). By default, now.</param>
    [HttpGet("service-times/export")]
    [SwaggerOperation(Summary = "Download the service times as CSV", OperationId = "ExportServiceTimes")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportServiceTimes([FromQuery] int? hotelId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var (start, end) = Period(from, to);
        var report = await queryService.Handle(new GetServiceTimesQuery(targetHotelId, start, end));

        var indicators = new List<(string Name, ServiceTimeIndicator Indicator)>
        {
            ("Limpieza de habitaciones", report.Cleaning),
            ("Incidencias correctivas", report.Incidents),
            ("Solicitudes de huéspedes", report.GuestRequests)
        };
        indicators.AddRange(report.GuestRequestsByArea.Select(a => ($"Solicitudes - {AreaLabel(a.Area)}", a.Indicator)));
        return CsvFile.Download(FileName("tiempos-de-atencion", targetHotelId, start, end),
            ["indicador", "cantidad", "espera_promedio_min", "trabajo_promedio_min", "total_promedio_min", "total_maximo_min"],
            indicators.Select(i => new object?[]
            {
                i.Name, i.Indicator.Count, i.Indicator.AverageResponseMinutes, i.Indicator.AverageWorkMinutes,
                i.Indicator.AverageTotalMinutes, i.Indicator.LongestTotalMinutes
            }));
    }

    private static string AreaLabel(WorkArea area) => area switch
    {
        WorkArea.FrontDesk => "Recepción",
        WorkArea.Housekeeping => "Limpieza",
        _ => "Mantenimiento"
    };

    private static string FileName(string report, int hotelId, DateTimeOffset from, DateTimeOffset to) =>
        $"{report}-hotel{hotelId}-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";

    private (int HotelId, IActionResult? Denied) HotelInScope(int? hotelId)
    {
        var targetHotelId = hotelId ?? (User.IsChainAdmin() ? null : User.GetHotelId());
        if (targetHotelId is null)
            throw new InvalidFieldException("hotelId", AccommodationErrorCodes.HotelRequired, "Send the hotelId of the report.");
        if (!User.IsChainAdmin() && User.GetHotelId() != targetHotelId) return (0, Forbid());
        return (targetHotelId.Value, null);
    }

    private (DateTimeOffset From, DateTimeOffset To) Period(DateTimeOffset? from, DateTimeOffset? to)
    {
        var end = to ?? timeProvider.GetUtcNow();
        var start = from ?? end.AddDays(-30);
        if (start >= end)
            throw new InvalidFieldException("from", StaffPerformanceErrorCodes.InvalidPeriod, "from must be before to.");
        if (end - start > TimeSpan.FromDays(MaxPeriodDays))
            throw new InvalidFieldException("from", StaffPerformanceErrorCodes.InvalidPeriod,
                $"The period can cover at most {MaxPeriodDays} days.",
                new Dictionary<string, object?> { ["maxDays"] = MaxPeriodDays });
        return (start, end);
    }
}
