using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Payments.Domain.Model.Commands;
using RoomTrack.Payments.Domain.Model.Queries;
using RoomTrack.Payments.Domain.Services;
using RoomTrack.Payments.Interfaces.REST.Resources;
using RoomTrack.Payments.Interfaces.REST.Transform;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Payments.Interfaces.REST;

/// <summary>
///     Payments of bookings (D1: paying confirms the booking). The hotel registers the payments it receives (Yape,
///     Plin, transfer, cash, card on its POS); the guest can also pay from the app with a card through the integrated
///     card gateway (US-15), which only sends the API the card's token: the API never receives card data.
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
[SwaggerTag("Payments: payments that confirm bookings (registered by the hotel or by card from the app)")]
public class PaymentsController(
    IPaymentCommandService paymentCommandService,
    IPaymentQueryService paymentQueryService) : ControllerBase
{
    private const string YapePaymentExample = """
        { "id": 15, "bookingId": 42, "transactionId": "YAPE-00123456", "amount": 360.00, "status": "Completed",
          "method": "Yape", "operationNumber": "00123456", "note": "Pagó al llegar", "recordedByUserId": 10,
          "paymentDate": "2026-11-23 19:45:12", "refundedAt": null }
        """;

    private const string CardPaymentExample = """
        { "id": 16, "bookingId": 43, "transactionId": "chr_sbx_5f1c2a9e0b7d4c3a", "amount": 480.00, "status": "Completed",
          "method": "OnlineCard", "operationNumber": "VISA •••• 4242", "note": null, "recordedByUserId": null,
          "paymentDate": "2026-11-24 15:02:41", "refundedAt": null }
        """;

    private const string CardDeclinedExample = """
        { "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10", "title": "Business rule violation", "status": 409,
          "code": "payment.card_declined", "detail": "The card was declined: La tarjeta no tiene fondos suficientes.",
          "params": { "paymentId": 17, "declineCode": "insufficient_funds", "reason": "La tarjeta no tiene fondos suficientes.",
                      "alternatives": ["OnlineCard", "Yape", "Plin", "BankTransfer", "Cash", "CardAtFrontDesk"] } }
        """;

    /// <summary>Registers the payment of a booking and confirms it.</summary>
    /// <remarks>
    ///     Reception, admin (bookings of their hotel) and chain admin. The amount is always the booking total
    ///     (<c>totalPrice</c>); only a Pending booking can be paid. On success the booking becomes Confirmed in the same
    ///     transaction and the guest receives a confirmation e-mail. <c>operationNumber</c> is required for every method
    ///     except Cash. <c>OnlineCard</c> is refused (400): only the guest pays by card, from the app.
    /// </remarks>
    [HttpPost("api/v1/bookings/{bookingId:int}/payments")]
    [Authorize(Policy = Policies.RegisterPayments)]
    [SwaggerOperation(Summary = "Register the payment of a booking", OperationId = "RegisterPayment")]
    [ProducesResponseType(typeof(PaymentResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "method": "Yape", "operationNumber": "00123456", "note": "Pagó al llegar" }""")]
    [OpenApiResponseExample(StatusCodes.Status201Created, YapePaymentExample)]
    public async Task<IActionResult> RegisterPayment(int bookingId, [FromBody] RegisterPaymentResource resource)
    {
        var payment = await paymentCommandService.Handle(new RegisterPaymentCommand(bookingId, resource.ToMethod(),
            resource.OperationNumber, resource.Note, User.GetUserId(), User.GetHotelId(), User.IsChainAdmin()));
        return CreatedAtAction(nameof(GetPaymentByBooking), new { bookingId = payment.BookingId },
            PaymentResourceFromEntityAssembler.ToResourceFromEntity(payment));
    }

    /// <summary>Pays one's own booking with a card through the card gateway (US-15).</summary>
    /// <remarks>
    ///     The guest of a <b>Pending</b> booking, from the app (e.g. while doing the check-in). The app tokenizes the
    ///     card with the gateway's SDK and sends only <c>cardToken</c>; the gateway charges the booking total. Approved:
    ///     201 with the payment (<c>OnlineCard</c>, the card as "VISA •••• 4242"), the booking becomes
    ///     <c>Confirmed</c> in the same transaction, the guest gets the confirmation e-mail and the receipt, and the
    ///     check-in is enabled. Declined: 409 <c>payment.card_declined</c> with the reason and the
    ///     <c>alternatives</c> (another card or the methods the hotel accepts); the failed attempt is kept and the
    ///     booking stays Pending, so the guest can try again. The gateway runs in sandbox mode: <c>tok_visa</c> and
    ///     <c>tok_mastercard</c> are approved; <c>tok_declined_insufficient_funds</c>,
    ///     <c>tok_declined_expired_card</c> and <c>tok_declined_card</c> are declined. Errors: 400 <c>cardToken</c>;
    ///     404 not the guest's booking; 409 already paid or not pending.
    /// </remarks>
    [HttpPost("api/v1/bookings/{bookingId:int}/payments/card")]
    [Authorize(Policy = Policies.PayBookingsByCard)]
    [SwaggerOperation(Summary = "Pay a booking by card from the app", OperationId = "PayBookingByCard")]
    [ProducesResponseType(typeof(PaymentResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "cardToken": "tok_visa" }""")]
    [OpenApiResponseExample(StatusCodes.Status201Created, CardPaymentExample)]
    [OpenApiResponseExample(StatusCodes.Status409Conflict, CardDeclinedExample)]
    public async Task<IActionResult> PayBookingByCard(int bookingId, [FromBody] CardPaymentResource resource)
    {
        var payment = await paymentCommandService.Handle(new PayBookingByCardCommand(bookingId, User.GetUserId(), resource.CardToken!));
        return CreatedAtAction(nameof(GetPaymentByBooking), new { bookingId = payment.BookingId },
            PaymentResourceFromEntityAssembler.ToResourceFromEntity(payment));
    }

    /// <summary>Gets the payment of a booking.</summary>
    /// <remarks>
    ///     The completed (or refunded) payment if any, otherwise the latest attempt. Guests only for their own bookings,
    ///     staff for the bookings of their hotel (404 otherwise).
    /// </remarks>
    [HttpGet("api/v1/payments/booking/{bookingId:int}")]
    [Authorize(Policy = Policies.ReadPayments)]
    [SwaggerOperation(Summary = "Get the payment of a booking", OperationId = "GetPaymentByBooking")]
    [ProducesResponseType(typeof(PaymentResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [OpenApiResponseExample(StatusCodes.Status200OK, YapePaymentExample)]
    public async Task<IActionResult> GetPaymentByBooking(int bookingId)
    {
        var query = new GetPaymentByBookingIdQuery(bookingId,
            User.IsGuest() ? User.GetUserId() : null,
            User.IsGuest() || User.IsChainAdmin() ? null : User.GetHotelId() ?? 0);
        var payment = await paymentQueryService.Handle(query);
        return payment is null ? NotFound() : Ok(PaymentResourceFromEntityAssembler.ToResourceFromEntity(payment));
    }
}
