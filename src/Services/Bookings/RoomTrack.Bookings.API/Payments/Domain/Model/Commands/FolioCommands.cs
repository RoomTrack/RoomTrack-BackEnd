using RoomTrack.Payments.Domain.Model.ValueObjects;

namespace RoomTrack.Payments.Domain.Model.Commands;

/// <summary>Who acts on a folio: the guest of the booking or the front desk of its hotel.</summary>
/// <param name="UserId">IAM user id.</param>
/// <param name="IsGuest">The guest (only their own bookings).</param>
/// <param name="StaffHotelId">The hotel of a staff member.</param>
/// <param name="AllHotels">A chain admin.</param>
public sealed record FolioActor(int UserId, bool IsGuest, int? StaffHotelId, bool AllHotels)
{
    public bool OperatesHotel(int hotelId) => !IsGuest && (AllHotels || StaffHotelId == hotelId);
}

/// <summary>The front desk posts an additional charge (US-16).</summary>
public record PostFolioChargeCommand(int BookingId, string Description, FolioChargeCategory Category, decimal Amount, FolioActor Actor);

/// <summary>The front desk holds the guarantee deposit at check-in (US-15).</summary>
public record HoldDepositCommand(int BookingId, decimal Amount, PaymentMethod Method, string? OperationNumber, FolioActor Actor);

/// <summary>The guest or the front desk pays the balance, possibly split between methods (US-16).</summary>
public record PayFolioCommand(int BookingId, IReadOnlyList<FolioPaymentPart> Parts, FolioActor Actor);

/// <summary>The guest gives the invoice data before the check-out (US-17).</summary>
public record RequestInvoiceCommand(int BookingId, string? Ruc, string? BusinessName, string? Address, FolioActor Actor);

/// <summary>The front desk re-sends a receipt to the guest's e-mail (US-17).</summary>
public record ResendReceiptCommand(int ReceiptId, FolioActor Actor);
