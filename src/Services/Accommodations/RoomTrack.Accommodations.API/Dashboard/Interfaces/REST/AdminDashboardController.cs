using RoomTrack.Dashboard.Application;
using RoomTrack.Dashboard.Domain.Model;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Dashboard.Interfaces.REST;

/// <summary>
///     The administrator's panel of a hotel (US-18): every counter of the panel in one call, so the app does not build
///     them from other screens. The monthly summary (revenue, bookings, occupancy, cancellations) stays in
///     <c>GET /analytics/performance/monthly</c>.
/// </summary>
[Authorize(Policy = Policies.ViewAnalytics)]
[ApiController]
[Route("api/v1/hotels/{hotelId:int}/dashboard")]
[Produces("application/json")]
[SwaggerTag("Administrator's panel of a hotel")]
public class AdminDashboardController(IAdminDashboardQueryService queryService) : ControllerBase
{
    public const int DefaultOverdueMinutes = 60;
    public const int MaxOverdueMinutes = 24 * 60;

    /// <summary>The panel of the hotel now.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><c>rooms</c>: by status and the occupancy (occupied over all the rooms).</item>
    ///         <item><c>cleaning</c>: open tasks, unassigned ones and the overdue ones (open for <c>overdueAfterMinutes</c>).</item>
    ///         <item><c>incidents</c>: open, critical, high, blocking a room, escalated (US-23) and recurring (US-10).</item>
    ///         <item><c>guestRequests</c>: open, pending and late (past the attention time promised to the guest).</item>
    ///         <item>
    ///             <c>frontDesk</c>: arrivals and departures of today, guests in house and the latest evaluations (null when
    ///             the Bookings service is not reachable).
    ///         </item>
    ///         <item><c>attention</c>: escalated and critical incidents and late requests, the most urgent first (at most 10).</item>
    ///     </list>
    ///     Administrators of the hotel or chain administrators.
    /// </remarks>
    /// <param name="hotelId">The hotel.</param>
    /// <param name="overdueAfterMinutes">When an open cleaning task counts as overdue (1-1440, 60 by default).</param>
    [HttpGet]
    [SwaggerOperation(Summary = "Get the administrator's panel of a hotel", OperationId = "GetAdminDashboard")]
    [ProducesResponseType(typeof(AdminDashboard), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "hotelId": 1, "generatedAt": "2026-11-26T15:00:00+00:00",
          "rooms": { "total": 20, "available": 8, "occupied": 9, "cleaning": 2, "maintenance": 1, "occupancyPercent": 45 },
          "cleaning": { "open": 4, "unassigned": 1, "overdue": 1, "overdueAfterMinutes": 60 },
          "incidents": { "open": 3, "critical": 1, "high": 1, "blockingRooms": 1, "escalated": 1, "recurring": 0 },
          "guestRequests": { "open": 2, "pending": 1, "late": 1 },
          "frontDesk": { "arrivalsToday": 3, "inHouse": 9, "departuresToday": 2, "reviewsLast30Days": 12,
                         "averageRatingLast30Days": 4.33,
                         "latestReviews": [ { "bookingId": 41, "roomId": 12, "overallRating": 5, "frontDeskRating": 5,
                                              "housekeepingRating": 4, "maintenanceRating": null,
                                              "comment": "Excelente atención", "submittedAt": "2026-11-25T18:00:00+00:00" } ] },
          "attention": [ { "kind": "Incident", "referenceId": 52, "roomId": 12, "roomNumber": "204", "title": "Cerradura rota",
                           "reason": "Escalated", "since": "2026-11-26T13:40:00+00:00" } ] }
        """)]
    public async Task<IActionResult> GetAdminDashboard(int hotelId, [FromQuery] int overdueAfterMinutes = DefaultOverdueMinutes)
    {
        if (!User.IsChainAdmin() && User.GetHotelId() != hotelId) return Forbid();
        if (overdueAfterMinutes is < 1 or > MaxOverdueMinutes)
            throw new InvalidFieldException("overdueAfterMinutes", "dashboard.overdue_minutes_invalid",
                $"overdueAfterMinutes goes from 1 to {MaxOverdueMinutes}.",
                new Dictionary<string, object?> { ["min"] = 1, ["max"] = MaxOverdueMinutes });
        return Ok(await queryService.Handle(new GetAdminDashboardQuery(hotelId, TimeSpan.FromMinutes(overdueAfterMinutes))));
    }
}
