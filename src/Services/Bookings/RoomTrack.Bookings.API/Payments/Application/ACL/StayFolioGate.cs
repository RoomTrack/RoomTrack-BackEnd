using RoomTrack.Bookings.Application.OutboundServices;
using RoomTrack.Payments.Domain.Repositories;

namespace RoomTrack.Payments.Application.ACL;

/// <summary>The Payments side of the check-out port: the folio of the stay must be settled.</summary>
public class StayFolioGate(IGuestFolioRepository folioRepository) : IStayFolioGate
{
    public async Task EnsureSettledForCheckOutAsync(int bookingId) =>
        (await folioRepository.FindByBookingIdAsync(bookingId))?.EnsureSettledForCheckOut();
}
