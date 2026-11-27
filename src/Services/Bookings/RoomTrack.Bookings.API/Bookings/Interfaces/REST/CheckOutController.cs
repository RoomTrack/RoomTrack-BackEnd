using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Bookings.Interfaces.REST.Resources;
using RoomTrack.Bookings.Interfaces.REST.Transform;
using RoomTrack.IAM.Interfaces.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;

namespace RoomTrack.Bookings.Interfaces.REST;

/// <summary>Check-out of a stay (US-14 digital, US-27 at the front desk).</summary>
[Authorize]
[ApiController]
[Route("api/v1/bookings/{bookingId:int}/check-out")]
[Produces("application/json")]
[SwaggerTag("Check-out of a stay")]
public class CheckOutController(ICheckInService checkInService, IBookingQueryService bookingQueryService) : ControllerBase
{
    /// <summary>Checks the guest out.</summary>
    /// <remarks>
    ///     The guest of the booking from the app, or the front desk of its hotel. Only a <c>CheckedIn</c> booking, on
    ///     any day of the stay. The booking becomes <c>Completed</c> (an early check-out frees the remaining nights),
    ///     the room access code stops working, the room goes to <c>Cleaning</c> ("pendiente de limpieza") with its
    ///     cleaning task, and the guest receives the summary of the stay by e-mail. Errors: 404 not the guest's booking
    ///     or outside the hotel of the staff member; 409 not checked in, or already checked out.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Policies.CheckOut)]
    [SwaggerOperation(Summary = "Check out", OperationId = "CheckOut")]
    [ProducesResponseType(typeof(BookingResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "id": 42, "code": "RT-7K2M9Q", "hotelId": 1, "roomId": 101, "roomNumber": "204", "guestName": "Ana Torres",
        "guestEmail": "ana.torres@example.com", "guestPhone": null, "checkInDate": "2026-11-24T00:00:00", "checkOutDate": "2026-11-26T00:00:00",
        "nights": 2, "pricePerNight": 180.00, "totalPrice": 360.00, "status": "Completed", "createdAt": "2026-11-20T15:12:00+00:00",
        "paymentDueAt": null, "confirmedAt": "2026-11-20T18:40:00+00:00", "cancelledAt": null, "cancellationReason": null,
        "checkedInAt": "2026-11-24T20:05:00+00:00", "guestProfileId": null, "userId": 25, "paymentInstructions": null,
        "checkedOutAt": "2026-11-26T15:40:00+00:00", "checkInStage": "Completed" }
        """)]
    [OpenApiResponseExample(StatusCodes.Status409Conflict, """
        { "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10", "title": "Business rule violation", "status": 409,
        "code": "folio.balance_pending",
        "detail": "Booking RT-7K2M9Q has 48.50 of additional charges pending: pay them before checking out.",
        "params": { "balance": 48.50, "charges": [ { "description": "Minibar: 2 aguas y 1 snack", "category": "Minibar", "amount": 18.50,
        "postedAt": "2026-11-24T22:10:00+00:00" }, { "description": "Lavandería (3 prendas)", "category": "Laundry", "amount": 30.00,
        "postedAt": "2026-11-25T14:00:00+00:00" } ] } }
        """)]
    public async Task<IActionResult> CheckOut(int bookingId)
    {
        var booking = await checkInService.Handle(new CheckOutCommand(bookingId, User.ToBookingRequester()));
        return Ok(BookingResourceFromEntityAssembler.ToResourceFromEntity(booking,
            await bookingQueryService.FetchRoomNumbersAsync([booking])));
    }
}
