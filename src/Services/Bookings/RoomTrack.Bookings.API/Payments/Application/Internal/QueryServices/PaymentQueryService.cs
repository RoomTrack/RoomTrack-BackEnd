using BackendAwRoomTrack.API.Bookings.Interfaces.ACL;
using BackendAwRoomTrack.API.Payments.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Payments.Domain.Model.Queries;
using BackendAwRoomTrack.API.Payments.Domain.Repositories;
using BackendAwRoomTrack.API.Payments.Domain.Services;

namespace BackendAwRoomTrack.API.Payments.Application.Internal.QueryServices;

/// <summary>
/// Service implementation for handling payment queries.
/// Retrieves payment data from the repository.
/// </summary>
public class PaymentQueryService(
    IPaymentRepository paymentRepository,
    IBookingsContextFacade bookingsContextFacade) : IPaymentQueryService
{
    /// <summary>
    /// Handles the query to retrieve a payment by booking identifier.
    /// Guests only get payments of bookings they own.
    /// </summary>
    /// <param name="query">The query containing the booking ID.</param>
    /// <returns>The payment associated with the booking or null if not found (or not visible to the guest).</returns>
    public async Task<Payment?> Handle(GetPaymentByBookingIdQuery query)
    {
        if (query.GuestUserId.HasValue || query.StaffHotelId.HasValue)
        {
            var booking = await bookingsContextFacade.FetchBookingAsync(query.BookingId, query.GuestUserId);
            if (booking is null || (query.StaffHotelId.HasValue && booking.HotelId != query.StaffHotelId)) return null;
        }

        return await paymentRepository.FindByBookingIdAsync(query.BookingId);
    }
}
