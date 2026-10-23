using System;
using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Bookings.Domain.Model.ValueObjects;

namespace BackendAwRoomTrack.API.Tests.Bookings;

/// <summary>Rooms, stays and bookings shared by the Bookings tests, on a fixed hotel day.</summary>
internal static class BookingTestData
{
    public const int HotelId = 1;
    public const int GuestUserId = 42;

    public static readonly DateTime Today = new(2030, 1, 10);
    public static readonly DateTimeOffset Now = new(2030, 1, 10, 15, 0, 0, TimeSpan.Zero);
    public static readonly TimeSpan PaymentHold = TimeSpan.FromHours(24);

    public static RoomOffer Room(int roomId = 101, int hotelId = HotelId, decimal pricePerNight = 150m) =>
        new(roomId, hotelId, RoomTypeId: 1, RoomTypeName: "Double", pricePerNight, Description: "Double room",
            Amenities: [], Status: "Available", Number: roomId.ToString(), HotelAcceptsBookings: true);

    /// <summary>A stay starting <paramref name="daysFromToday"/> days after <paramref name="today"/>.</summary>
    public static DateRange Stay(DateTime today, int daysFromToday = 2, int nights = 2) =>
        new(today.AddDays(daysFromToday), today.AddDays(daysFromToday + nights));

    public static GuestContact Contact(string name = "Alice Wonderland", string email = "alice@example.com") =>
        new(name, email, phone: null);

    /// <summary>A Pending booking placed by a guest for themselves.</summary>
    public static Booking PlaceForGuest(Guid? guestProfileId = null, int roomId = 101) =>
        Booking.Place(BookingRequester.Guest(GuestUserId, "Alice").WithGuestProfile(guestProfileId), Room(roomId),
            Stay(Today), Contact(), GuestUserId, guestProfileId: null, Today, Now, PaymentHold);

    /// <summary>A Pending booking taken by the staff of <see cref="HotelId"/> for a guest without an account.</summary>
    public static Booking PlaceByStaff(Guid? guestProfileId = null, int roomId = 101) =>
        Booking.Place(BookingRequester.HotelStaff(userId: 7, HotelId), Room(roomId), Stay(Today), Contact(),
            guestUserId: null, guestProfileId, Today, Now, PaymentHold);
}
