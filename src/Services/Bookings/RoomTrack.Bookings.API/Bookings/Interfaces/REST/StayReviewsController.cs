using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Bookings.Interfaces.REST.Resources;
using RoomTrack.Bookings.Interfaces.REST.Transform;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using RoomTrack.Shared.Infrastructure.Interfaces.ASP.Csv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Bookings.Interfaces.REST;

/// <summary>
///     Evaluation of the stays (US-21): the guest rates the finished stay and its areas and leaves a comment, stored on
///     the server. The hotel reads the evaluations and the satisfaction of the guests by area (US-26).
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
[SwaggerTag("Evaluation of the stays and satisfaction of the guests")]
public class StayReviewsController(
    IStayReviewCommandService commandService,
    IStayReviewQueryService queryService,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>The longest period a listing or report covers.</summary>
    public const int MaxPeriodDays = 366;

    /// <summary>Evaluates a finished stay.</summary>
    /// <remarks>
    ///     The guest of the booking, once it is checked out (<c>Completed</c>) and up to 30 days after. An overall rating
    ///     from 1 to 5, optional ratings of the front desk, housekeeping and maintenance, and an optional comment.
    ///     Errors: 404 not the guest's booking; 409 <c>stay_review.stay_not_finished</c>, <c>stay_review.window_closed</c>
    ///     or <c>stay_review.already_submitted</c>.
    /// </remarks>
    [HttpPost("api/v1/bookings/{bookingId:int}/review")]
    [Authorize(Policy = Policies.ReviewStays)]
    [SwaggerOperation(Summary = "Evaluate a stay", OperationId = "SubmitStayReview")]
    [ProducesResponseType(typeof(StayReviewResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""
        { "overallRating": 4, "frontDeskRating": 5, "housekeepingRating": 4, "maintenanceRating": 3,
          "comment": "Excelente atención en recepción. La ducha tardaba en calentar." }
        """)]
    public async Task<IActionResult> SubmitStayReview(int bookingId, [FromBody] SubmitStayReviewResource resource)
    {
        var review = await commandService.Handle(new SubmitStayReviewCommand(bookingId, User.ToBookingRequester(),
            resource.ToRatings(), resource.Comment));
        return CreatedAtAction(nameof(GetStayReview), new { bookingId }, StayReviewResourceAssembler.ToResource(review));
    }

    /// <summary>The evaluation of a stay.</summary>
    /// <remarks>The guest of the booking or the staff of its hotel. 404 when the booking cannot be seen or was not evaluated.</remarks>
    [HttpGet("api/v1/bookings/{bookingId:int}/review")]
    [Authorize(Policy = Policies.ReadBookings)]
    [SwaggerOperation(Summary = "Get the evaluation of a stay", OperationId = "GetStayReview")]
    [ProducesResponseType(typeof(StayReviewResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStayReview(int bookingId)
    {
        var review = await queryService.Handle(new GetStayReviewByBookingIdQuery(bookingId, User.ToBookingRequester()));
        return review is null ? NotFound() : Ok(StayReviewResourceAssembler.ToResource(review));
    }

    /// <summary>The evaluations of the guests of a hotel, newest first, with their comments.</summary>
    /// <remarks>Administrators of the hotel or chain administrators. Without dates: the last 30 days.</remarks>
    /// <param name="hotelId">The hotel (optional for a hotel administrator; required for a chain admin).</param>
    /// <param name="from">From (ISO 8601, inclusive).</param>
    /// <param name="to">To (ISO 8601, exclusive). By default, now.</param>
    /// <param name="limit">At most this many (1-200, 50 by default).</param>
    [HttpGet("api/v1/stay-reviews")]
    [Authorize(Policy = Policies.ViewStayReviews)]
    [SwaggerOperation(Summary = "List the evaluations of the stays", OperationId = "GetStayReviews")]
    [ProducesResponseType(typeof(IEnumerable<StayReviewResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStayReviews([FromQuery] int? hotelId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, [FromQuery] int limit = 50)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var (start, end) = Period(from, to);
        var reviews = await queryService.Handle(new GetStayReviewsQuery(targetHotelId, start, end, Math.Clamp(limit, 1, 200)));
        return Ok(reviews.Select(StayReviewResourceAssembler.ToResource));
    }

    /// <summary>The satisfaction of the guests by area (US-26).</summary>
    /// <remarks>
    ///     From the evaluations submitted in the period: the overall rating and that of the front desk, housekeeping and
    ///     maintenance (always the three), each with the number of ratings, the average (1-5), the share of satisfied
    ///     guests (4 or 5) and the distribution of the ratings. Administrators of the hotel or chain administrators.
    ///     Without dates: the last 30 days.
    /// </remarks>
    /// <param name="hotelId">The hotel (optional for a hotel administrator; required for a chain admin).</param>
    /// <param name="from">From (ISO 8601, inclusive).</param>
    /// <param name="to">To (ISO 8601, exclusive). By default, now.</param>
    [HttpGet("api/v1/stay-reviews/satisfaction")]
    [Authorize(Policy = Policies.ViewStayReviews)]
    [SwaggerOperation(Summary = "Get the satisfaction of the guests by area", OperationId = "GetStaySatisfaction")]
    [ProducesResponseType(typeof(SatisfactionReportResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "hotelId": 1, "from": "2026-11-01T00:00:00+00:00", "to": "2026-12-01T00:00:00+00:00",
          "overall": { "count": 4, "average": 4.25, "satisfiedPercent": 75, "distribution": [0, 0, 1, 1, 2] },
          "areas": [
            { "area": "FrontDesk", "count": 4, "average": 4.75, "satisfiedPercent": 100, "distribution": [0, 0, 0, 1, 3] },
            { "area": "Housekeeping", "count": 3, "average": 4, "satisfiedPercent": 66.7, "distribution": [0, 0, 1, 1, 1] },
            { "area": "Maintenance", "count": 2, "average": 2.5, "satisfiedPercent": 0, "distribution": [0, 1, 1, 0, 0] } ] }
        """)]
    public async Task<IActionResult> GetStaySatisfaction([FromQuery] int? hotelId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var (start, end) = Period(from, to);
        var report = await queryService.Handle(new GetStaySatisfactionQuery(targetHotelId, start, end));
        return Ok(StayReviewResourceAssembler.ToResource(report));
    }

    /// <summary>Downloads the evaluations of the guests as CSV, with their comments (US-34).</summary>
    /// <remarks>Every evaluation of the period, newest first. UTF-8 with BOM, comma separated.</remarks>
    /// <param name="hotelId">The hotel (optional for a hotel administrator; required for a chain admin).</param>
    /// <param name="from">From (ISO 8601, inclusive).</param>
    /// <param name="to">To (ISO 8601, exclusive). By default, now.</param>
    [HttpGet("api/v1/stay-reviews/export")]
    [Authorize(Policy = Policies.ViewStayReviews)]
    [SwaggerOperation(Summary = "Download the evaluations of the stays as CSV", OperationId = "ExportStayReviews")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportStayReviews([FromQuery] int? hotelId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var (start, end) = Period(from, to);
        var reviews = await queryService.Handle(new GetStayReviewsQuery(targetHotelId, start, end, StayReviewExportLimit));
        return CsvFile.Download(FileName("evaluaciones", targetHotelId, start, end),
            ["id", "reserva", "habitacion", "huesped", "general", "recepcion", "limpieza", "mantenimiento", "comentario", "fecha"],
            reviews.Select(r => new object?[]
            {
                r.Id, r.BookingId, r.RoomId, r.GuestUserId, r.OverallRating, r.FrontDeskRating, r.HousekeepingRating,
                r.MaintenanceRating, r.Comment, r.SubmittedAt
            }));
    }

    /// <summary>Downloads the satisfaction of the guests by area as CSV (US-34).</summary>
    /// <remarks>One row for the stay as a whole and one per area, with the count of each rating from 1 to 5.</remarks>
    /// <param name="hotelId">The hotel (optional for a hotel administrator; required for a chain admin).</param>
    /// <param name="from">From (ISO 8601, inclusive).</param>
    /// <param name="to">To (ISO 8601, exclusive). By default, now.</param>
    [HttpGet("api/v1/stay-reviews/satisfaction/export")]
    [Authorize(Policy = Policies.ViewStayReviews)]
    [SwaggerOperation(Summary = "Download the satisfaction by area as CSV", OperationId = "ExportStaySatisfaction")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportStaySatisfaction([FromQuery] int? hotelId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var (targetHotelId, denied) = HotelInScope(hotelId);
        if (denied is not null) return denied;
        var (start, end) = Period(from, to);
        var report = await queryService.Handle(new GetStaySatisfactionQuery(targetHotelId, start, end));
        var rows = new List<(string Area, SatisfactionScore Score)> { ("General", report.Overall) };
        rows.AddRange(report.Areas.Select(a => (AreaLabel(a.Area), a.Score)));
        return CsvFile.Download(FileName("satisfaccion", targetHotelId, start, end),
            ["area", "evaluaciones", "promedio", "porcentaje_satisfechos", "notas_1", "notas_2", "notas_3", "notas_4", "notas_5"],
            rows.Select(row => new object?[] { row.Area, row.Score.Count, row.Score.Average, row.Score.SatisfiedPercent }
                .Concat(row.Score.Distribution.Cast<object?>())));
    }

    /// <summary>The most evaluations a CSV download holds.</summary>
    private const int StayReviewExportLimit = 5000;

    private static string AreaLabel(SatisfactionArea area) => area switch
    {
        SatisfactionArea.FrontDesk => "Recepción",
        SatisfactionArea.Housekeeping => "Limpieza",
        _ => "Mantenimiento"
    };

    private static string FileName(string report, int hotelId, DateTimeOffset from, DateTimeOffset to) =>
        $"{report}-hotel{hotelId}-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";

    private (int HotelId, IActionResult? Denied) HotelInScope(int? hotelId)
    {
        var targetHotelId = hotelId ?? (User.IsChainAdmin() ? null : User.GetHotelId());
        if (targetHotelId is null)
            throw new InvalidFieldException("hotelId", BookingErrorCodes.ReviewHotelRequired, "Send the hotelId of the evaluations.");
        if (!User.IsChainAdmin() && User.GetHotelId() != targetHotelId) return (0, Forbid());
        return (targetHotelId.Value, null);
    }

    private (DateTimeOffset From, DateTimeOffset To) Period(DateTimeOffset? from, DateTimeOffset? to)
    {
        var end = to ?? timeProvider.GetUtcNow();
        var start = from ?? end.AddDays(-30);
        if (start >= end)
            throw new InvalidFieldException("from", BookingErrorCodes.ReviewPeriodInvalid, "from must be before to.");
        if (end - start > TimeSpan.FromDays(MaxPeriodDays))
            throw new InvalidFieldException("from", BookingErrorCodes.ReviewPeriodInvalid,
                $"The period can cover at most {MaxPeriodDays} days.",
                new Dictionary<string, object?> { ["maxDays"] = MaxPeriodDays });
        return (start, end);
    }
}
