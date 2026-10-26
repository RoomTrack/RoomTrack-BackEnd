using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Bookings.Interfaces.REST.Resources;
using RoomTrack.Bookings.Interfaces.REST.Transform;
using RoomTrack.IAM.Interfaces.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

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
    public async Task<IActionResult> CheckOut(int bookingId)
    {
        var booking = await checkInService.Handle(new CheckOutCommand(bookingId, User.ToBookingRequester()));
        return Ok(BookingResourceFromEntityAssembler.ToResourceFromEntity(booking,
            await bookingQueryService.FetchRoomNumbersAsync([booking])));
    }
}
