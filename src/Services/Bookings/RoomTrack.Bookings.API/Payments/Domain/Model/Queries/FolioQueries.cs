using RoomTrack.Payments.Domain.Model.Commands;

namespace RoomTrack.Payments.Domain.Model.Queries;

/// <summary>The folio of a booking (opened empty if it has none yet).</summary>
public record GetFolioByBookingIdQuery(int BookingId, FolioActor Actor);

/// <summary>The receipts of a booking.</summary>
public record GetReceiptsByBookingIdQuery(int BookingId, FolioActor Actor);

/// <summary>A receipt.</summary>
public record GetReceiptByIdQuery(int ReceiptId, FolioActor Actor);

/// <summary>US-17: every charge, refund, deposit and receipt of a hotel between two dates, newest first.</summary>
public record GetPaymentHistoryQuery(int HotelId, DateTimeOffset From, DateTimeOffset To, int Limit = 500);
