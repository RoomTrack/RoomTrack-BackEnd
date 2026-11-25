namespace RoomTrack.Bookings.Application.OutboundServices;

/// <summary>
///     Port of the check-out to the folio of the stay (Payments context, US-14/16): the guest cannot check out while
///     additional charges are unpaid.
/// </summary>
public interface IStayFolioGate
{
    /// <exception cref="RoomTrack.Shared.Domain.Model.Exceptions.BusinessRuleViolationException">
    ///     Code <c>folio.balance_pending</c>, with the balance and the charges, when something is unpaid.
    /// </exception>
    Task EnsureSettledForCheckOutAsync(int bookingId);
}
