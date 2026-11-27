using System.Globalization;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Bookings.Interfaces.REST.Resources;
using RoomTrack.Bookings.Interfaces.REST.Transform;
using RoomTrack.IAM.Interfaces.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Swashbuckle.AspNetCore.Annotations;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;

namespace RoomTrack.Bookings.Interfaces.REST;

/// <summary>
///     Bookings: guest self-service and centralized management by the hotel.
/// </summary>
/// <remarks>
///     Visibility (R4): a guest sees their own bookings (others answer 404); reception, housekeeping, maintenance and
///     admin see the bookings of their hotel; a chain admin sees all. Every booking starts Pending and is confirmed
///     only when its payment is registered (<c>POST /bookings/{id}/payments</c>, D1).
/// </remarks>
[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[SwaggerTag("Bookings: guest bookings, hotel calendar, changes, cancellation policy and payment hold")]
public class BookingsController(
    IBookingCommandService bookingCommandService,
    IBookingQueryService bookingQueryService) : ControllerBase
{
    /// <summary>Gets a booking.</summary>
    [HttpGet("{bookingId:int}")]
    [Authorize(Policy = Policies.ReadBookings)]
    [SwaggerOperation(Summary = "Get a booking", OperationId = "GetBookingById")]
    [ProducesResponseType(typeof(BookingResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "id": 42, "code": "RT-7K2M9Q", "hotelId": 1, "roomId": 101, "roomNumber": "204", "guestName": "Ana Torres",
        "guestEmail": "ana.torres@example.com", "guestPhone": "+51987654321", "checkInDate": "2026-11-24T00:00:00",
        "checkOutDate": "2026-11-26T00:00:00", "nights": 2, "pricePerNight": 180.00, "totalPrice": 360.00, "status": "Pending",
        "createdAt": "2026-11-20T15:12:00+00:00", "paymentDueAt": "2026-11-21T15:12:00+00:00", "confirmedAt": null, "cancelledAt": null,
        "cancellationReason": null, "checkedInAt": null, "guestProfileId": null, "userId": 25,
        "paymentInstructions": { "accountHolder": "Hotel Miraflores SAC", "yapeNumber": "987654321", "plinNumber": null,
        "bankName": "BCP", "bankAccountNumber": "193-2345678-0-12", "bankAccountCci": "00219300234567801214" },
        "checkedOutAt": null, "checkInStage": null }
        """)]
    public async Task<IActionResult> GetBookingById(int bookingId)
    {
        var booking = await bookingQueryService.Handle(new GetBookingByIdQuery(bookingId, User.ToBookingRequester()));
        if (booking is null) return NotFound();
        return Ok(await ToResourceWithPaymentInstructionsAsync(booking));
    }

    /// <summary>Books a room (guest self-service; reservation taken by the staff).</summary>
    /// <remarks>
    ///     Availability is checked under a lock of the room row, so two simultaneous requests cannot book the same
    ///     nights. The booking is created <b>Pending</b> with a unique <c>code</c>, the total price and a payment
    ///     deadline (<c>paymentDueAt</c>, 24 h by default); the guest receives an e-mail with the code, the total and
    ///     how to pay. Without a payment by the deadline it is cancelled automatically.
    ///     <list type="bullet">
    ///         <item>A guest books for themselves: name and e-mail come from their account (<c>guestName</c> may
    ///         override the name; <c>userId</c>/<c>guestProfileId</c> are ignored).</item>
    ///         <item>Reception, admin and chain_admin book rooms of their hotel for a guest account (<c>userId</c>) or
    ///         for a guest without an account (<c>guestName</c> + <c>guestEmail</c>, optional <c>guestPhone</c>).</item>
    ///     </list>
    ///     The response includes <c>paymentInstructions</c>: the payment methods of the hotel.
    ///     Errors: 400 invalid dates (check-out not after check-in, check-in in the past) or guest data; 403 room of
    ///     another hotel; 409 the hotel has no payment methods yet (<c>booking.hotel_payment_settings_missing</c>),
    ///     or the room is no longer free for those nights or is under maintenance.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Policies.PlaceBookings)]
    [SwaggerOperation(Summary = "Book a room", OperationId = "CreateBooking")]
    [ProducesResponseType(typeof(BookingResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""
        { "roomId": 101, "checkInDate": "2026-11-24", "checkOutDate": "2026-11-26", "guestName": "Ana Torres",
        "guestEmail": "ana.torres@example.com", "guestPhone": "+51987654321" }
        """)]
    [OpenApiResponseExample(StatusCodes.Status201Created, """
        { "id": 42, "code": "RT-7K2M9Q", "hotelId": 1, "roomId": 101, "roomNumber": "204", "guestName": "Ana Torres",
        "guestEmail": "ana.torres@example.com", "guestPhone": "+51987654321", "checkInDate": "2026-11-24T00:00:00",
        "checkOutDate": "2026-11-26T00:00:00", "nights": 2, "pricePerNight": 180.00, "totalPrice": 360.00, "status": "Pending",
        "createdAt": "2026-11-20T15:12:00+00:00", "paymentDueAt": "2026-11-21T15:12:00+00:00", "confirmedAt": null, "cancelledAt": null,
        "cancellationReason": null, "checkedInAt": null, "guestProfileId": null, "userId": 25,
        "paymentInstructions": { "accountHolder": "Hotel Miraflores SAC", "yapeNumber": "987654321", "plinNumber": null,
        "bankName": null, "bankAccountNumber": null, "bankAccountCci": null }, "checkedOutAt": null, "checkInStage": null }
        """)]
    [OpenApiResponseExample(StatusCodes.Status409Conflict, """
        { "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10", "title": "Business rule violation", "status": 409,
        "code": "booking.room_unavailable",
        "detail": "Room 101 is no longer available for 2026-11-24..2026-11-26: another booking holds some of those nights. Search again for available rooms." }
        """)]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingResource resource)
    {
        var booking = await bookingCommandService.Handle(
            CreateBookingCommandFromResourceAssembler.ToCommandFromResource(resource, User.ToBookingRequester()));
        return CreatedAtAction(nameof(GetBookingById), new { bookingId = booking.Id },
            await ToResourceWithPaymentInstructionsAsync(booking));
    }

    /// <summary>
    ///     Lists the bookings visible to the requester, newest first. Without <paramref name="page"/> and
    ///     <paramref name="pageSize"/> the whole list is returned (as before); with either of them, only that page
    ///     (page 1 and 20 bookings by default, at most 100) and the total of the list in the <c>X-Total-Count</c> header.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.ReadBookings)]
    [SwaggerOperation(Summary = "List bookings", OperationId = "GetAllBookings")]
    [ProducesResponseType(typeof(IEnumerable<BookingResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        [ { "id": 42, "code": "RT-7K2M9Q", "hotelId": 1, "roomId": 101, "roomNumber": "204", "guestName": "Ana Torres",
        "guestEmail": "ana.torres@example.com", "guestPhone": null, "checkInDate": "2026-11-24T00:00:00", "checkOutDate": "2026-11-26T00:00:00",
        "nights": 2, "pricePerNight": 180.00, "totalPrice": 360.00, "status": "Confirmed", "createdAt": "2026-11-20T15:12:00+00:00",
        "paymentDueAt": null, "confirmedAt": "2026-11-20T18:40:00+00:00", "cancelledAt": null, "cancellationReason": null,
        "checkedInAt": null, "guestProfileId": null, "userId": 25, "paymentInstructions": null, "checkedOutAt": null,
        "checkInStage": "InProgress" } ]
        """)]
    public async Task<IActionResult> GetAllBookings([FromQuery] int? page = null, [FromQuery] int? pageSize = null)
    {
        if (page is null && pageSize is null)
        {
            var bookings = await bookingQueryService.Handle(new GetBookingsQuery(User.ToBookingRequester()));
            return Ok(await ToResourcesAsync(bookings));
        }

        var result = await bookingQueryService.Handle(new GetBookingsPageQuery(User.ToBookingRequester(),
            new PageRequest(page ?? 1, pageSize ?? PageRequest.DefaultPageSize)));
        Response.Headers[TotalCountHeader] = result.TotalCount.ToString(CultureInfo.InvariantCulture);
        return Ok(await ToResourcesAsync(result.Items));
    }

    /// <summary>Total of a paged list (exposed to the browser apps by the gateway's CORS policy).</summary>
    public const string TotalCountHeader = "X-Total-Count";

    /// <summary>Lists the bookings of a room (hotel staff of that room's hotel).</summary>
    [HttpGet("room/{roomId:int}")]
    [Authorize(Policy = Policies.ReadRoomBookings)]
    [SwaggerOperation(Summary = "List the bookings of a room", OperationId = "GetBookingsByRoomId")]
    [ProducesResponseType(typeof(IEnumerable<BookingResource>), StatusCodes.Status200OK)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        [ { "id": 42, "code": "RT-7K2M9Q", "hotelId": 1, "roomId": 101, "roomNumber": "204", "guestName": "Ana Torres",
        "guestEmail": "ana.torres@example.com", "guestPhone": null, "checkInDate": "2026-11-24T00:00:00", "checkOutDate": "2026-11-26T00:00:00",
        "nights": 2, "pricePerNight": 180.00, "totalPrice": 360.00, "status": "CheckedIn", "createdAt": "2026-11-20T15:12:00+00:00",
        "paymentDueAt": null, "confirmedAt": "2026-11-20T18:40:00+00:00", "cancelledAt": null, "cancellationReason": null,
        "checkedInAt": "2026-11-24T20:05:00+00:00", "guestProfileId": null, "userId": 25, "paymentInstructions": null,
        "checkedOutAt": null, "checkInStage": "Completed" } ]
        """)]
    public async Task<IActionResult> GetBookingsByRoomId(int roomId)
    {
        var bookings = await bookingQueryService.Handle(new GetBookingsByRoomIdQuery(roomId, User.ToBookingRequester()));
        return Ok(await ToResourcesAsync(bookings));
    }

    /// <summary>Booking calendar of a hotel.</summary>
    /// <remarks>
    ///     Active bookings (Pending, Confirmed, CheckedIn) that hold at least one night between <c>from</c> and
    ///     <c>to</c> (excluded), sorted by check-in, plus one entry per date with the ids of the arrivals, departures and
    ///     in-house bookings. At most 92 days. Admin and reception: their hotel (<c>hotelId</c> optional); a chain
    ///     admin: any hotel, or all when <c>hotelId</c> is omitted.
    /// </remarks>
    /// <param name="from">First date (yyyy-MM-dd).</param>
    /// <param name="to">Last date, excluded (yyyy-MM-dd).</param>
    /// <param name="hotelId">The hotel.</param>
    [HttpGet("calendar")]
    [Authorize(Policy = Policies.ManageBookings)]
    [SwaggerOperation(Summary = "Booking calendar of a hotel", OperationId = "GetBookingCalendar")]
    [ProducesResponseType(typeof(BookingCalendarResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "hotelId": 1, "from": "2026-11-24", "to": "2026-11-26",
        "bookings": [ { "id": 42, "code": "RT-7K2M9Q", "roomId": 101, "roomNumber": "204", "guestName": "Ana Torres",
        "guestEmail": "ana.torres@example.com", "guestPhone": null, "checkInDate": "2026-11-24", "checkOutDate": "2026-11-26",
        "nights": 2, "status": "Confirmed", "totalPrice": 360.00, "paymentDueAt": null } ],
        "days": [ { "date": "2026-11-24", "arrivals": [42], "departures": [], "inHouse": [42], "occupiedRooms": 1 },
        { "date": "2026-11-25", "arrivals": [], "departures": [], "inHouse": [42], "occupiedRooms": 1 } ] }
        """)]
    public async Task<IActionResult> GetCalendar([FromQuery, BindRequired] DateOnly from, [FromQuery, BindRequired] DateOnly to,
        [FromQuery] int? hotelId)
    {
        var window = new DateRange(from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue));
        var calendar = await bookingQueryService.Handle(new GetBookingCalendarQuery(User.ToBookingRequester(), hotelId, window));
        return Ok(BookingResourceFromEntityAssembler.ToResource(calendar,
            await bookingQueryService.FetchRoomNumbersAsync(calendar.Bookings)));
    }

    /// <summary>Changes the dates and/or the room of a booking.</summary>
    /// <remarks>
    ///     Only Pending or Confirmed bookings of the requester's hotel. The new stay is validated like a new booking
    ///     (under the room row lock; the booking does not conflict with itself). A Pending booking takes the price of
    ///     the new room; a paid (Confirmed) booking can only change to a stay with the same total. The guest receives
    ///     an e-mail with the before and after.
    /// </remarks>
    [HttpPatch("{bookingId:int}")]
    [Authorize(Policy = Policies.ManageBookings)]
    [SwaggerOperation(Summary = "Change a booking", OperationId = "RescheduleBooking")]
    [ProducesResponseType(typeof(BookingResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "checkInDate": "2026-11-25", "checkOutDate": "2026-11-27", "roomId": 102 }""")]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "id": 42, "code": "RT-7K2M9Q", "hotelId": 1, "roomId": 102, "roomNumber": "205", "guestName": "Ana Torres",
        "guestEmail": "ana.torres@example.com", "guestPhone": null, "checkInDate": "2026-11-25T00:00:00", "checkOutDate": "2026-11-27T00:00:00",
        "nights": 2, "pricePerNight": 180.00, "totalPrice": 360.00, "status": "Confirmed", "createdAt": "2026-11-20T15:12:00+00:00",
        "paymentDueAt": null, "confirmedAt": "2026-11-20T18:40:00+00:00", "cancelledAt": null, "cancellationReason": null,
        "checkedInAt": null, "guestProfileId": null, "userId": 25, "paymentInstructions": null, "checkedOutAt": null, "checkInStage": null }
        """)]
    public async Task<IActionResult> RescheduleBooking(int bookingId, [FromBody] RescheduleBookingResource resource)
    {
        if (resource.CheckInDate is null && resource.CheckOutDate is null && resource.RoomId is null)
            throw new InvalidFieldException(nameof(resource.CheckInDate), BookingErrorCodes.ChangeRequiresAField,
                "Send at least one of checkInDate, checkOutDate or roomId.");

        var booking = await bookingCommandService.Handle(new RescheduleBookingCommand(bookingId, User.ToBookingRequester(),
            resource.CheckInDate, resource.CheckOutDate, resource.RoomId));
        return Ok(await ToResourceAsync(booking));
    }

    /// <summary>Cancels a booking.</summary>
    /// <remarks>
    ///     Cancellation policy: only Pending or Confirmed bookings, and not on or after the check-in day (hotel time).
    ///     The nights are released at once. A paid booking gets its payment marked Refunded (the money is returned
    ///     outside the system). The guest receives an e-mail. A guest can only cancel their own bookings; staff those
    ///     of their hotel.
    /// </remarks>
    [HttpPost("{bookingId:int}/cancel")]
    [Authorize(Policy = Policies.CancelBookings)]
    [SwaggerOperation(Summary = "Cancel a booking", OperationId = "CancelBooking")]
    [ProducesResponseType(typeof(BookingResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "id": 42, "code": "RT-7K2M9Q", "hotelId": 1, "roomId": 101, "roomNumber": "204", "guestName": "Ana Torres",
        "guestEmail": "ana.torres@example.com", "guestPhone": null, "checkInDate": "2026-11-24T00:00:00", "checkOutDate": "2026-11-26T00:00:00",
        "nights": 2, "pricePerNight": 180.00, "totalPrice": 360.00, "status": "Cancelled", "createdAt": "2026-11-20T15:12:00+00:00",
        "paymentDueAt": null, "confirmedAt": null, "cancelledAt": "2026-11-21T09:30:00+00:00", "cancellationReason": "GuestRequest",
        "checkedInAt": null, "guestProfileId": null, "userId": 25, "paymentInstructions": null, "checkedOutAt": null, "checkInStage": null }
        """)]
    public async Task<IActionResult> CancelBooking(int bookingId)
    {
        var booking = await bookingCommandService.Handle(new CancelBookingCommand(bookingId, User.ToBookingRequester()));
        return Ok(await ToResourceAsync(booking));
    }

    /// <summary>Detail and create responses also say how to pay a Pending booking (the hotel's payment methods).</summary>
    private async Task<BookingResource> ToResourceWithPaymentInstructionsAsync(Domain.Model.Aggregates.Booking booking) =>
        BookingResourceFromEntityAssembler.ToResourceFromEntity(booking,
            await bookingQueryService.FetchRoomNumbersAsync([booking]),
            await bookingQueryService.FetchPaymentInstructionsAsync(booking));

    private async Task<BookingResource> ToResourceAsync(Domain.Model.Aggregates.Booking booking) =>
        BookingResourceFromEntityAssembler.ToResourceFromEntity(booking, await bookingQueryService.FetchRoomNumbersAsync([booking]));

    /// <summary>Room numbers of the whole list are resolved in one batch (no query per booking).</summary>
    private async Task<IEnumerable<BookingResource>> ToResourcesAsync(IEnumerable<Domain.Model.Aggregates.Booking> bookings)
    {
        var list = bookings.ToList();
        var numbers = await bookingQueryService.FetchRoomNumbersAsync(list);
        return list.Select(booking => BookingResourceFromEntityAssembler.ToResourceFromEntity(booking, numbers));
    }
}
