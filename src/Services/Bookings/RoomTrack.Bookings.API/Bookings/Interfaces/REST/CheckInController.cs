using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Bookings.Interfaces.REST.Resources;
using RoomTrack.Bookings.Interfaces.REST.Transform;
using RoomTrack.IAM.Interfaces.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Bookings.Interfaces.REST;

/// <summary>
///     Check-in of a booking: digital, by the guest (US-13), or at the front desk for a guest who arrives without having
///     done it (US-27).
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

    /// <summary>Completes the digital check-in with the identity document.</summary>
    /// <remarks>
    ///     <c>multipart/form-data</c> with <c>documentType</c> (DNI, PASSPORT, CE), <c>documentNumber</c>,
    ///     <c>nationality</c> (ISO alpha-2) and <c>document</c> (JPG, PNG or PDF, ≤ 5 MB, checked by content).
    ///     The guest of a <b>Confirmed</b> (paid) booking, or the front desk of its hotel registering the document of a
    ///     guest at the desk (assisted check-in, US-27), from the check-in day until the day before the check-out
    ///     (hotel time). The document format is validated automatically; when it passes, the check-in is
    ///     approved at once: the booking becomes <c>CheckedIn</c>, the room <c>Occupied</c>, the response carries the
    ///     6-digit room <c>accessCode</c> (valid until the check-out) and the hotel's housekeeping is notified by
    ///     e-mail. Errors: 400 per field (<c>documentNumber</c>, <c>nationality</c>, <c>documentType</c>,
    ///     <c>document</c>); 404 not the guest's booking; 409 not confirmed, outside the window, already checked in, or
    ///     the room is not ready (then request assistance).
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
    public async Task<IActionResult> CompleteCheckIn(int bookingId, [FromForm] CheckInFormResource form)
    {
        using var buffer = new MemoryStream();
        await form.Document!.CopyToAsync(buffer);
        var result = await checkInService.Handle(new CompleteDigitalCheckInCommand(bookingId, User.ToBookingRequester(),
            form.ToDocumentType(), form.DocumentNumber!, form.Nationality!, buffer.ToArray()));
        return CreatedAtAction(nameof(GetCheckIn), new { bookingId }, ToResource(result));
    }

    /// <summary>Gets the check-in of a booking (the access code only for its guest).</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ReadBookings)]
    [SwaggerOperation(Summary = "Get the check-in of a booking", OperationId = "GetCheckIn")]
    [ProducesResponseType(typeof(CheckInResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
        result.CheckIn.AccessCodeValidUntil, result.CheckIn.CompletedAt);
}
