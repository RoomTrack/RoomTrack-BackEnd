using System;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Bookings.Domain.Model.ValueObjects;
using FluentAssertions;
using Xunit;
using static BackendAwRoomTrack.API.Tests.Bookings.BookingTestData;

namespace BackendAwRoomTrack.API.Tests.Bookings.Domain;

public class BookingTests
{
    [Fact]
    public void Place_ByStaffWithoutGuestProfileId_ShouldInitializeWithNullGuestProfileId_AndPendingStatus()
    {
        // Arrange
        var room = Room(roomId: 101, pricePerNight: 150m);
        var dates = Stay(Today);

        // Act
        var booking = Booking.Place(BookingRequester.HotelStaff(userId: 7, HotelId), room, dates,
            Contact("Alice Wonderland", "alice@example.com"), guestUserId: null, guestProfileId: null, Today, Now, PaymentHold);

        // Assert
        booking.RoomId.Should().Be(101);
        booking.HotelId.Should().Be(HotelId);
        booking.GuestName.Should().Be("Alice Wonderland");
        booking.GuestEmail.Should().Be("alice@example.com");
        booking.CheckInDate.Should().Be(dates.CheckIn);
        booking.CheckOutDate.Should().Be(dates.CheckOut);
        booking.PricePerNight.Should().Be(150m);
        booking.GuestProfileId.Should().BeNull();
        booking.GuestId.Should().BeNull();
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.PaymentDueAt.Should().Be(Now + PaymentHold);
    }

    [Fact]
    public void Place_ByStaffWithGuestProfileId_ShouldRetainGuestProfileId()
    {
        // Arrange
        var expectedProfileId = Guid.NewGuid();

        // Act
        var booking = PlaceByStaff(expectedProfileId);

        // Assert
        booking.GuestProfileId.Should().Be(expectedProfileId);
        booking.GuestName.Should().Be("Alice Wonderland");
        booking.GuestEmail.Should().Be("alice@example.com");
    }

    [Fact]
    public void Place_ByGuest_ShouldReferenceTheirAccountAndGuestProfile()
    {
        // Arrange
        var profileId = Guid.NewGuid();

        // Act
        var booking = PlaceForGuest(profileId);

        // Assert
        booking.GuestId.Should().NotBeNull();
        booking.GuestId!.Value.Should().Be(GuestUserId);
        booking.GuestProfileId.Should().Be(profileId);
        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public void Place_ByStaffOfAnotherHotel_ShouldThrowBookingOutsideHotelScopeException()
    {
        // Act
        var act = () => Booking.Place(BookingRequester.HotelStaff(userId: 7, hotelId: 99), Room(), Stay(Today),
            Contact(), guestUserId: null, guestProfileId: null, Today, Now, PaymentHold);

        // Assert
        act.Should().Throw<BookingOutsideHotelScopeException>();
    }

    [Fact]
    public void Confirm_ShouldTransitionStatusToConfirmed()
    {
        // Arrange
        var booking = PlaceByStaff();

        // Act
        booking.Confirm(Now);

        // Assert
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ConfirmedAt.Should().Be(Now);
        booking.PaymentDueAt.Should().BeNull();
    }

    [Fact]
    public void Cancel_ByOwnerGuestBeforeCheckIn_ShouldTransitionStatusToCancelled()
    {
        // Arrange
        var booking = PlaceForGuest();

        // Act
        booking.Cancel(BookingRequester.Guest(GuestUserId, "Alice"), Today, Now);

        // Assert
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancelledAt.Should().Be(Now);
    }

    [Fact]
    public void BookingAggregate_ShouldNotDependOnProfilesClasses()
    {
        // Reflection check: Verify Booking type has no fields or properties of types in Profiles namespace
        var properties = typeof(Booking).GetProperties();
        foreach (var prop in properties)
        {
            prop.PropertyType.FullName.Should().NotContain("Profiles", "Booking domain model must not leak Profiles types");
            prop.PropertyType.FullName.Should().NotContain("GuestProfile", "Booking must only reference identity Guid?");
        }
    }
}
