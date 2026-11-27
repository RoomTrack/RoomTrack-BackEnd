using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Bookings.Interfaces.REST.Resources;
using RoomTrack.Bookings.Interfaces.REST.Transform;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Bookings.Interfaces.REST;

/// <summary>
///     Check-in of a booking: digital, by the guest (US-13), before arriving or at the hotel, or at the front desk for a
///     guest who arrives without having done it (US-27).
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/bookings/{bookingId:int}/check-in")]
[Produces("application/json")]
[Tags("Check-in")]
public class CheckInController(ICheckInService checkInService) : ControllerBase
{
    /// <summary>Largest multipart request accepted (the document itself is limited to 5 MB).</summary>
    private const long MaxRequestBytes = 6 * 1024 * 1024;

    private const string CompletedCheckInExample = """
        { "bookingId": 42, "bookingCode": "RT-7K2M9Q", "bookingStatus": "CheckedIn", "roomId": 101, "status": "Approved",
          "documentType": "DNI", "documentNumberMasked": "•••••912", "nationality": "PE", "accessCode": "482915",
          "accessCodeValidUntil": "2026-11-26T17:00:00+00:00", "submittedAt": "2026-11-24T20:00:00+00:00",
          "completedAt": "2026-11-24T20:00:00+00:00" }
        """;

    private const string PreArrivalCheckInExample = """
        { "bookingId": 42, "bookingCode": "RT-7K2M9Q", "bookingStatus": "Confirmed", "roomId": 101, "status": "PendingArrival",
          "documentType": "DNI", "documentNumberMasked": "•••••912", "nationality": "PE", "accessCode": null,
          "accessCodeValidUntil": null, "submittedAt": "2026-11-22T23:10:00+00:00", "completedAt": null }
        """;

    private const string RoomNotReadyExample = """
        { "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10", "title": "Business rule violation", "status": 409,
          "code": "check_in.room_not_ready", "detail": "Your room is not ready yet: it should be ready in about 25 minutes.",
          "params": { "roomId": 101, "roomStatus": "Cleaning", "estimatedReadyAt": "2026-11-24T20:25:00+00:00",
                      "estimatedWaitMinutes": 25 } }
        """;

    /// <summary>Completes the digital check-in with the identity document.</summary>
    /// <remarks>
    ///     <c>multipart/form-data</c> with <c>documentType</c> (DNI, PASSPORT, CE), <c>documentNumber</c>,
    ///     <c>nationality</c> (ISO alpha-2) and <c>document</c> (JPG, PNG or PDF, ≤ 5 MB, checked by content).
    ///     The guest of a <b>Confirmed</b> (paid) booking, or the front desk of its hotel registering the document of a
    ///     guest at the desk (assisted check-in, US-27). The document format is validated automatically.
    ///     <list type="bullet">
    ///         <item><b>Before the check-in day</b> (up to <c>Bookings__PreArrivalCheckInDays</c>, 3 days, US-13): the
    ///         check-in waits for the guest (<c>status</c> <c>PendingArrival</c>, the booking shows
    ///         <c>checkInStage</c> <c>InProgress</c>, "check-in en proceso"); no access code yet. At the hotel the guest
    ///         only confirms their identity (<c>POST …/check-in/arrival</c>).</item>
    ///         <item><b>From the check-in day until the day before the check-out</b>: approved at once. The booking
    ///         becomes <c>CheckedIn</c>, the room <c>Occupied</c>, the response carries the 6-digit room
    ///         <c>accessCode</c> (valid until the check-out) and the hotel's housekeeping is notified by e-mail.</item>
    ///     </list>
    ///     Errors: 400 per field (<c>documentNumber</c>, <c>nationality</c>, <c>documentType</c>, <c>document</c>); 404
    ///     not the guest's booking; 409 not confirmed, outside the window, already checked in, already done before the
    ///     arrival (<c>check_in.awaiting_arrival</c>), or the room is not ready (<c>check_in.room_not_ready</c> with the
    ///     estimated wait in <c>params</c>).
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Policies.CompleteCheckIn)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [SwaggerOperation(Summary = "Complete the digital check-in", OperationId = "CompleteCheckIn")]
    [ProducesResponseType(typeof(CheckInResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiResponseExample(StatusCodes.Status201Created, CompletedCheckInExample)]
    [OpenApiResponseExample(StatusCodes.Status409Conflict, RoomNotReadyExample)]
    public async Task<IActionResult> CompleteCheckIn(int bookingId, [FromForm] CheckInFormResource form)
    {
        using var buffer = new MemoryStream();
        await form.Document!.CopyToAsync(buffer);
        var result = await checkInService.Handle(new CompleteDigitalCheckInCommand(bookingId, User.ToBookingRequester(),
            form.ToDocumentType(), form.DocumentNumber!, form.Nationality!, buffer.ToArray()));
        return CreatedAtAction(nameof(GetCheckIn), new { bookingId }, ToResource(result));
    }

    /// <summary>Confirms the arrival of a guest who did the check-in before arriving (US-13).</summary>
    /// <remarks>
    ///     The guest at the hotel (or the front desk for them) only confirms their identity with the number of the
    ///     document sent with the digital check-in, from the check-in day on. The booking becomes <c>CheckedIn</c>, the
    ///     room <c>Occupied</c>, and the response carries the room <c>accessCode</c> (for the guest). Errors: 400
    ///     <c>check_in.identity_mismatch</c> (another number); 404 not the guest's booking; 409 no check-in waiting for
    ///     the arrival, the check-in day has not come, or the room is not ready (<c>check_in.room_not_ready</c> with
    ///     <c>params.estimatedWaitMinutes</c>).
    /// </remarks>
    [HttpPost("arrival")]
    [Authorize(Policy = Policies.CompleteCheckIn)]
    [SwaggerOperation(Summary = "Confirm the arrival of a guest who checked in before arriving", OperationId = "ConfirmCheckInArrival")]
    [ProducesResponseType(typeof(CheckInResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "documentNumber": "45678912" }""")]
    [OpenApiResponseExample(StatusCodes.Status200OK, CompletedCheckInExample)]
    [OpenApiResponseExample(StatusCodes.Status409Conflict, RoomNotReadyExample)]
    public async Task<IActionResult> ConfirmArrival(int bookingId, [FromBody] ConfirmArrivalResource resource)
    {
        var result = await checkInService.Handle(new ConfirmArrivalCommand(bookingId, User.ToBookingRequester(), resource.DocumentNumber!));
        return Ok(ToResource(result));
    }

    /// <summary>Gets the check-in of a booking (the access code only for its guest).</summary>
    /// <remarks>A check-in done before the arrival shows <c>PendingArrival</c> and no access code yet.</remarks>
    [HttpGet]
    [Authorize(Policy = Policies.ReadBookings)]
    [SwaggerOperation(Summary = "Get the check-in of a booking", OperationId = "GetCheckIn")]
    [ProducesResponseType(typeof(CheckInResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [OpenApiResponseExample(StatusCodes.Status200OK, PreArrivalCheckInExample)]
    public async Task<IActionResult> GetCheckIn(int bookingId)
    {
        var result = await checkInService.FindAsync(bookingId, User.ToBookingRequester());
        return result is null ? NotFound() : Ok(ToResource(result));
    }

    /// <summary>Asks the front desk for help with the check-in.</summary>
    /// <remarks>
    ///     The reception staff of the hotel (its admins if there is no reception user) receive an e-mail with the
    ///     booking, the guest's contact and the optional message. Only the guest of a Pending or Confirmed booking.
    /// </remarks>
    [HttpPost("assistance")]
    [Authorize(Policy = Policies.CompleteCheckIn)]
    [SwaggerOperation(Summary = "Request help with the check-in", OperationId = "RequestCheckInAssistance")]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "message": "The app does not accept my passport photo." }""")]
    [OpenApiResponseExample(StatusCodes.Status202Accepted, """{ "message": "The front desk has been notified and will contact you shortly." }""")]
    public async Task<IActionResult> RequestAssistance(int bookingId, [FromBody] CheckInAssistanceResource? resource)
    {
        await checkInService.Handle(new RequestCheckInAssistanceCommand(bookingId, User.ToBookingRequester(), resource?.Message));
        return Accepted(new { message = "The front desk has been notified and will contact you shortly." });
    }

    /// <summary>Rooms the guest can move into now, when the room of the booking is not ready (US-27).</summary>
    /// <remarks>
    ///     For the front desk, when the check-in is refused because the room is not ready (409): the rooms of the hotel
    ///     ready now and free until the check-out, same room type and same price first. A paid booking can move
    ///     (<c>PATCH /api/v1/bookings/{id}</c> with the <c>roomId</c>) to a room whose stay costs the same.
    /// </remarks>
    [HttpGet("alternatives")]
    [Authorize(Policy = Policies.ManageBookings)]
    [SwaggerOperation(Summary = "Rooms ready now for a guest whose room is not ready", OperationId = "GetCheckInAlternatives")]
    [ProducesResponseType(typeof(IEnumerable<AvailableRoomResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        [ { "id": 102, "hotelId": 1, "roomTypeId": 2, "roomTypeName": "Doble", "description": "Doble con vista a la calle",
            "amenities": ["WiFi", "TV"], "status": "Available", "available": true, "pricePerNight": 180.00, "nights": 2,
            "totalPrice": 360.00, "checkInDate": "2026-11-24", "checkOutDate": "2026-11-26", "number": "102" } ]
        """)]
    public async Task<IActionResult> GetAlternatives(int bookingId)
    {
        var rooms = await checkInService.FindAlternativeRoomsAsync(bookingId, User.ToBookingRequester());
        return Ok(rooms.Select(available => new AvailableRoomResource(
            available.Room.RoomId, available.Room.HotelId, available.Room.RoomTypeId, available.Room.RoomTypeName,
            available.Room.Description, available.Room.Amenities, available.Room.Status, true,
            available.Room.PricePerNight, available.Nights, available.TotalPrice,
            DateOnly.FromDateTime(available.Dates.CheckIn), DateOnly.FromDateTime(available.Dates.CheckOut), available.Room.Number)));
    }

    private static CheckInResource ToResource(CheckInResult result) => new(
        result.Booking.Id, result.Booking.Code.Value, result.Booking.Status.ToString(), result.Booking.RoomId,
        result.CheckIn.Status.ToString(),
        result.CheckIn.DocumentType switch
        {
            IdentityDocumentType.Dni => "DNI",
            IdentityDocumentType.Passport => "PASSPORT",
            _ => "CE"
        },
        result.CheckIn.Identity.MaskedNumber, result.CheckIn.Nationality, result.AccessCode?.Value,
        result.CheckIn.AccessCodeValidUntil, result.CheckIn.SubmittedAt, result.CheckIn.CompletedAt);
}
