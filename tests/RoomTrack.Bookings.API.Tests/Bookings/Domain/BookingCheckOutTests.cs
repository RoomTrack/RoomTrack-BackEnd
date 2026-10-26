using System;
using System.Linq;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Events;
using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using FluentAssertions;
using Xunit;
using static RoomTrack.Tests.Bookings.BookingTestData;

namespace RoomTrack.Tests.Bookings.Domain;

/// <summary>Check-out of a stay (US-14 digital, US-27 at the front desk).</summary>
public class BookingCheckOutTests
{
    private static readonly BookingRequester Guest = BookingRequester.Guest(GuestUserId, "Alice");
    private static readonly BookingRequester Reception = BookingRequester.HotelStaff(userId: 7, HotelId);

    /// <summary>The first day of the stay of <see cref="PlaceForGuest"/> (check-in day).</summary>
    private static readonly DateTime FirstDay = Stay(Today).CheckIn;

    private static Booking CheckedInBooking()
    {
        var booking = PlaceForGuest();
        booking.Confirm(Now);
        booking.CheckIn(FirstDay, Now);
        booking.ClearDomainEvents();
        return booking;
    }

    [Fact]
    public void TheGuest_ShouldCheckOutTheirStay_AndEndIt()
    {
        var booking = CheckedInBooking();

        booking.CheckOut(Guest, FirstDay.AddDays(2), Now.AddDays(4));

        booking.Status.Should().Be(BookingStatus.Completed);
        booking.CheckedOutAt.Should().Be(Now.AddDays(4));
        booking.CheckedOutByUserId.Should().Be(GuestUserId);
        booking.Status.IsActive().Should().BeFalse("a completed stay no longer holds the room");
        var e = booking.DomainEvents.OfType<GuestCheckedOutEvent>().Should().ContainSingle().Subject;
        e.Assisted.Should().BeFalse();
        e.Early.Should().BeFalse();
    }

    [Fact]
    public void TheFrontDesk_ShouldCheckOutAGuest_AndAnEarlyCheckOutIsFlagged()
    {
        var booking = CheckedInBooking();

        booking.CheckOut(Reception, FirstDay, Now);

        var e = booking.DomainEvents.OfType<GuestCheckedOutEvent>().Should().ContainSingle().Subject;
        e.Assisted.Should().BeTrue();
        e.Early.Should().BeTrue();
    }

    [Fact]
    public void StaffOfAnotherHotel_OrAnotherGuest_ShouldNotFindTheBooking()
    {
        var booking = CheckedInBooking();

        var otherHotel = () => booking.CheckOut(BookingRequester.HotelStaff(userId: 8, HotelId + 1), FirstDay, Now);
        var otherGuest = () => booking.CheckOut(BookingRequester.Guest(GuestUserId + 1, "Bob"), FirstDay, Now);

        otherHotel.Should().Throw<BookingNotFoundException>();
        otherGuest.Should().Throw<BookingNotFoundException>();
        booking.Status.Should().Be(BookingStatus.CheckedIn);
    }

    [Fact]
    public void ABookingNotCheckedIn_OrAlreadyCheckedOut_ShouldBeRefused()
    {
        var confirmed = PlaceForGuest();
        confirmed.Confirm(Now);
        var checkedOut = CheckedInBooking();
        checkedOut.CheckOut(Guest, FirstDay, Now);

        var notCheckedIn = () => confirmed.CheckOut(Guest, FirstDay, Now);
        var twice = () => checkedOut.CheckOut(Guest, FirstDay, Now);

        notCheckedIn.Should().Throw<InvalidBookingTransitionException>().Which.Code.Should().Be(BookingErrorCodes.CheckOutNotCheckedIn);
        twice.Should().Throw<InvalidBookingTransitionException>().Which.Code.Should().Be(BookingErrorCodes.CheckOutAlreadyCompleted);
    }
}
