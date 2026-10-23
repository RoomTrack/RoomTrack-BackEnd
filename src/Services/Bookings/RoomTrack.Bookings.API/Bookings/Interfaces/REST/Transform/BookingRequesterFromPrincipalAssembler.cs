using System.Security.Claims;
using BackendAwRoomTrack.API.Bookings.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.IAM.Interfaces.Authorization;

namespace BackendAwRoomTrack.API.Bookings.Interfaces.REST.Transform;

/// <summary>Translates the authenticated user into the Bookings notion of a requester.</summary>
public static class BookingRequesterFromPrincipalAssembler
{
    public static BookingRequester ToBookingRequester(this ClaimsPrincipal user) =>
        user.IsGuest()
            ? BookingRequester.Guest(user.GetUserId(), user.GetUsername())
            : BookingRequester.HotelStaff(user.GetUserId(), user.GetHotelId(), user.IsChainAdmin());
}
