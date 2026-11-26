using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.Commands;
using RoomTrack.Payments.Domain.Model.Queries;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Payments.Domain.Services;
using RoomTrack.Payments.Interfaces.REST.Resources;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Payments.Interfaces.REST;

/// <summary>
///     The folio of a stay (US-15/16/17): additional charges, the guarantee deposit, the (split) payment of the balance
///     and the invoice data; and the receipts of every payment with the payment history of the hotel.
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
[SwaggerTag("Folio of the stay, receipts and payment history")]
public class FoliosController(IFolioCommandService commandService, IFolioQueryService queryService) : ControllerBase
{
    private const string FolioRoute = "api/v1/bookings/{bookingId:int}/folio";

    private const string FolioExample = """
        { "bookingId": 42, "bookingCode": "RT-7K2M9Q", "status": "Open", "chargesTotal": 48.50, "paidTotal": 0, "balance": 48.50,
          "charges": [
            { "id": 1, "description": "Minibar: 2 aguas y 1 snack", "category": "Minibar", "amount": 18.50, "postedByUserId": 10,
              "postedAt": "2026-11-24T22:10:00+00:00" },
            { "id": 2, "description": "Lavandería (3 prendas)", "category": "Laundry", "amount": 30.00, "postedByUserId": 10,
              "postedAt": "2026-11-25T14:00:00+00:00" } ],
          "payments": [],
          "deposit": { "amount": 200.00, "status": "Held", "method": "CardAtFrontDesk", "heldAt": "2026-11-23T20:05:00+00:00",
                       "releasedAt": null },
          "invoiceData": null }
        """;

    /// <summary>The folio of a stay: charges, payments, balance and deposit.</summary>
    /// <remarks>
    ///     The guest of the booking (from the app, before checking out) or the front desk of its hotel. A stay without
    ///     charges has an empty folio (balance 0). 404 when the booking is not the guest's or outside the hotel.
    /// </remarks>
    [HttpGet(FolioRoute)]
    [Authorize(Policy = Policies.ReadFolios)]
    [SwaggerOperation(Summary = "Get the folio of a stay", OperationId = "GetFolio")]
    [ProducesResponseType(typeof(GuestFolioResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [OpenApiResponseExample(StatusCodes.Status200OK, FolioExample)]
    public async Task<IActionResult> GetFolio(int bookingId)
    {
        var folio = await queryService.Handle(new GetFolioByBookingIdQuery(bookingId, Actor()));
        return folio is null ? NotFound() : Ok(FolioResourceAssembler.ToResource(folio));
    }

    /// <summary>Posts an additional charge to the folio (minibar, laundry, room service…).</summary>
    /// <remarks>
    ///     Front desk of the hotel, while the guest is checked in. The folio goes back to <c>Open</c> and the check-out
    ///     is refused until the balance is paid (409 <c>folio.balance_pending</c> with the charges).
    /// </remarks>
    [HttpPost(FolioRoute + "/charges")]
    [Authorize(Policy = Policies.ManageFolios)]
    [SwaggerOperation(Summary = "Post an additional charge", OperationId = "PostFolioCharge")]
    [ProducesResponseType(typeof(GuestFolioResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "description": "Minibar: 2 aguas y 1 snack", "category": "Minibar", "amount": 18.50 }""")]
    [OpenApiResponseExample(StatusCodes.Status201Created, FolioExample)]
    public async Task<IActionResult> PostFolioCharge(int bookingId, [FromBody] PostFolioChargeResource resource)
    {
        var folio = await commandService.Handle(new PostFolioChargeCommand(bookingId, resource.Description!, resource.ToCategory(),
            resource.Amount!.Value, Actor()));
        return CreatedAtAction(nameof(GetFolio), new { bookingId }, FolioResourceAssembler.ToResource(folio));
    }

    /// <summary>Holds the refundable guarantee deposit of the stay (US-15).</summary>
    /// <remarks>
    ///     Front desk of the hotel, at check-in (Confirmed or CheckedIn booking). The deposit is visible in the folio
    ///     and released automatically at check-out (its refund starts, US-16). 409 when one is already held.
    /// </remarks>
    [HttpPost(FolioRoute + "/deposit")]
    [Authorize(Policy = Policies.ManageFolios)]
    [SwaggerOperation(Summary = "Hold the guarantee deposit", OperationId = "HoldDeposit")]
    [ProducesResponseType(typeof(GuestFolioResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "amount": 200.00, "method": "CardAtFrontDesk", "operationNumber": "POS-778812" }""")]
    public async Task<IActionResult> HoldDeposit(int bookingId, [FromBody] HoldDepositResource resource)
    {
        var folio = await commandService.Handle(new HoldDepositCommand(bookingId, resource.Amount!.Value, resource.ToMethod(),
            resource.OperationNumber, Actor()));
        return Ok(FolioResourceAssembler.ToResource(folio));
    }

    /// <summary>Pays the balance of the folio, possibly split between two or more methods (US-16).</summary>
    /// <remarks>
    ///     The guest (from the app) or the front desk. Each part is a payment of its own and gets its receipt (boleta,
    ///     or factura with the invoice data). The parts together cannot exceed the balance (409
    ///     <c>folio.overpayment</c>); paid in full, the folio becomes <c>Settled</c> ("saldado").
    /// </remarks>
    [HttpPost(FolioRoute + "/payments")]
    [Authorize(Policy = Policies.SettleFolios)]
    [SwaggerOperation(Summary = "Pay the balance of the folio", OperationId = "PayFolio")]
    [ProducesResponseType(typeof(FolioPaymentResultResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""
        { "parts": [ { "amount": 30.00, "method": "Yape", "operationNumber": "00987654" },
                     { "amount": 18.50, "method": "Cash" } ] }
        """)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        { "folio": { "bookingId": 42, "bookingCode": "RT-7K2M9Q", "status": "Settled", "chargesTotal": 48.50, "paidTotal": 48.50,
                     "balance": 0, "charges": [], "payments": [
                       { "id": 7, "amount": 30.00, "method": "Yape", "operationNumber": "00987654", "paidAt": "2026-11-26T15:00:00+00:00" },
                       { "id": 8, "amount": 18.50, "method": "Cash", "operationNumber": null, "paidAt": "2026-11-26T15:00:00+00:00" } ],
                     "deposit": null, "invoiceData": null },
          "receipts": [
            { "id": 15, "number": "B001-00000015", "type": "Boleta", "bookingId": 42, "bookingCode": "RT-7K2M9Q",
              "source": "FolioPayment", "concept": "Consumos adicionales - reserva RT-7K2M9Q", "currency": "PEN",
              "subtotal": 25.42, "igv": 4.58, "total": 30.00, "method": "Yape", "customerName": "Ana Torres",
              "customerDocument": null, "customerAddress": null, "customerEmail": "ana@correo.pe",
              "issuedAt": "2026-11-26T15:00:00+00:00", "sentCount": 1, "lastSentAt": "2026-11-26T15:00:00+00:00" } ] }
        """)]
    public async Task<IActionResult> PayFolio(int bookingId, [FromBody] PayFolioResource resource)
    {
        var (folio, receipts) = await commandService.Handle(new PayFolioCommand(bookingId,
            resource.Parts!.Select(part => part.ToPart()).ToList(), Actor()));
        return Ok(new FolioPaymentResultResource(FolioResourceAssembler.ToResource(folio),
            receipts.Select(FolioResourceAssembler.ToResource).ToList()));
    }

    /// <summary>Gives the company data for an invoice (factura) before the check-out (US-17).</summary>
    /// <remarks>
    ///     The guest or the front desk, while the folio is open. The receipts issued afterwards (the balance of the
    ///     folio, or the booking payment if still pending) are facturas to that RUC; the earlier ones stay boletas.
    /// </remarks>
    [HttpPut(FolioRoute + "/invoice-data")]
    [Authorize(Policy = Policies.SettleFolios)]
    [SwaggerOperation(Summary = "Request an invoice for the stay", OperationId = "RequestInvoice")]
    [ProducesResponseType(typeof(GuestFolioResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [OpenApiRequestExample("""{ "ruc": "20123456789", "businessName": "Inversiones Andinas S.A.C.", "address": "Av. Larco 123, Miraflores, Lima" }""")]
    public async Task<IActionResult> RequestInvoice(int bookingId, [FromBody] InvoiceDataResource resource)
    {
        var folio = await commandService.Handle(new RequestInvoiceCommand(bookingId, resource.Ruc, resource.BusinessName,
            resource.Address, Actor()));
        return Ok(FolioResourceAssembler.ToResource(folio));
    }

    /// <summary>The receipts of a booking (booking payment and folio payments), oldest first.</summary>
    [HttpGet("api/v1/bookings/{bookingId:int}/receipts")]
    [Authorize(Policy = Policies.ReadFolios)]
    [SwaggerOperation(Summary = "List the receipts of a booking", OperationId = "GetReceiptsOfBooking")]
    [ProducesResponseType(typeof(IEnumerable<ReceiptResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceiptsOfBooking(int bookingId)
    {
        var receipts = await queryService.Handle(new GetReceiptsByBookingIdQuery(bookingId, Actor()));
        return receipts is null ? NotFound() : Ok(receipts.Select(FolioResourceAssembler.ToResource));
    }

    /// <summary>A receipt.</summary>
    [HttpGet("api/v1/receipts/{receiptId:int}")]
    [Authorize(Policy = Policies.ReadFolios)]
    [SwaggerOperation(Summary = "Get a receipt", OperationId = "GetReceipt")]
    [ProducesResponseType(typeof(ReceiptResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceipt(int receiptId)
    {
        var receipt = await queryService.Handle(new GetReceiptByIdQuery(receiptId, Actor()));
        return receipt is null ? NotFound() : Ok(FolioResourceAssembler.ToResource(receipt));
    }

    /// <summary>Re-sends a receipt to the guest's e-mail (US-17).</summary>
    /// <remarks>Front desk and administrators of the hotel of the receipt. <c>sentCount</c> grows with each send.</remarks>
    [HttpPost("api/v1/receipts/{receiptId:int}/resend")]
    [Authorize(Policy = Policies.ManageFolios)]
    [SwaggerOperation(Summary = "Re-send a receipt", OperationId = "ResendReceipt")]
    [ProducesResponseType(typeof(ReceiptResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendReceipt(int receiptId)
    {
        var receipt = await commandService.Handle(new ResendReceiptCommand(receiptId, Actor()));
        return receipt is null ? NotFound() : Ok(FolioResourceAssembler.ToResource(receipt));
    }

    /// <summary>The payment history of a hotel: charges, refunds, deposits and receipts by date (US-17).</summary>
    /// <remarks>
    ///     Front desk and administrators of the hotel (a chain admin sends <c>hotelId</c>). Newest first. Refunds and
    ///     released deposits are negative amounts. Without dates: the last 30 days.
    /// </remarks>
    /// <param name="hotelId">The hotel (optional for hotel staff; required for a chain admin).</param>
    /// <param name="from">From (ISO 8601).</param>
    /// <param name="to">To (ISO 8601).</param>
    [HttpGet("api/v1/payments/history")]
    [Authorize(Policy = Policies.ManageFolios)]
    [SwaggerOperation(Summary = "Get the payment history of a hotel", OperationId = "GetPaymentHistory")]
    [ProducesResponseType(typeof(IEnumerable<PaymentHistoryEntryResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [OpenApiResponseExample(StatusCodes.Status200OK, """
        [ { "occurredAt": "2026-11-26T15:00:00+00:00", "type": "Receipt", "bookingId": 42, "amount": 30.00, "method": "Yape",
            "reference": "B001-00000015", "receiptId": 15 },
          { "occurredAt": "2026-11-26T15:00:00+00:00", "type": "FolioPayment", "bookingId": 42, "amount": 30.00, "method": "Yape",
            "reference": "00987654", "receiptId": null },
          { "occurredAt": "2026-11-26T14:30:00+00:00", "type": "DepositReleased", "bookingId": 42, "amount": -200.00,
            "method": "CardAtFrontDesk", "reference": "POS-778812", "receiptId": null },
          { "occurredAt": "2026-11-20T18:00:00+00:00", "type": "Refund", "bookingId": 39, "amount": -360.00, "method": "Plin",
            "reference": "MANUAL-PL-4411", "receiptId": null } ]
        """)]
    public async Task<IActionResult> GetPaymentHistory([FromQuery] int? hotelId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var targetHotelId = hotelId ?? (User.IsChainAdmin() ? null : User.GetHotelId());
        if (targetHotelId is null)
            throw new InvalidFieldException("hotelId", ErrorCodes.FieldRequired, "Send the hotelId of the history.");
        if (!User.IsChainAdmin() && User.GetHotelId() != targetHotelId) return Forbid();
        var until = to ?? DateTimeOffset.UtcNow;
        var since = from ?? until.AddDays(-30);
        if (since > until)
            throw new InvalidFieldException("from", ErrorCodes.InvalidRequest, "'from' must be before 'to'.");
        var entries = await queryService.Handle(new GetPaymentHistoryQuery(targetHotelId.Value, since, until));
        return Ok(entries.Select(entry => new PaymentHistoryEntryResource(entry.OccurredAt, entry.Type, entry.BookingId,
            entry.Amount, entry.Method, entry.Reference, entry.ReceiptId)));
    }

    private FolioActor Actor() => new(User.GetUserId(), User.IsGuest(), User.GetHotelId(), User.IsChainAdmin());
}

public static class FolioResourceAssembler
{
    public static GuestFolioResource ToResource(GuestFolio folio) =>
        new(folio.BookingId, folio.BookingCode, folio.Status.ToString(), folio.ChargesTotal, folio.PaidTotal, folio.Balance,
            folio.Charges.Select(charge => new FolioChargeResource(charge.Id, charge.Description, charge.Category.ToString(),
                charge.Amount, charge.PostedByUserId, charge.PostedAt)).ToList(),
            folio.Payments.Select(payment => new FolioPaymentResource(payment.Id, payment.Amount, payment.Method.ToString(),
                payment.OperationNumber, payment.PaidAt)).ToList(),
            folio.DepositStatus == DepositStatus.None
                ? null
                : new FolioDepositResource(folio.DepositAmount ?? 0, folio.DepositStatus.ToString(), folio.DepositMethod?.ToString(),
                    folio.DepositHeldAt, folio.DepositReleasedAt),
            folio.InvoiceData is { } invoice ? new InvoiceDataView(invoice.Ruc, invoice.BusinessName, invoice.Address) : null);

    public static ReceiptResource ToResource(Receipt receipt) =>
        new(receipt.Id, receipt.FullNumber, receipt.Type.ToString(), receipt.BookingId, receipt.BookingCode, receipt.Source.ToString(),
            receipt.Concept, Receipt.Currency, receipt.Subtotal, receipt.Igv, receipt.Total, receipt.Method.ToString(),
            receipt.CustomerName, receipt.CustomerDocument, receipt.CustomerAddress, receipt.CustomerEmail, receipt.IssuedAt,
            receipt.SentCount, receipt.LastSentAt);
}
