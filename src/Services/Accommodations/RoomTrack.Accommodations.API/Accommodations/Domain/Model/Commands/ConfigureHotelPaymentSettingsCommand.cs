namespace BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;

/// <summary>
///     Sets how the guests of <paramref name="HotelId"/> pay their bookings (replaces the previous settings).
///     Blank values mean "not offered".
/// </summary>
public record ConfigureHotelPaymentSettingsCommand(
    int HotelId,
    string? AccountHolder,
    string? YapeNumber,
    string? PlinNumber,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountCci);
