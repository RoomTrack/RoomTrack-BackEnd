using System;
using FluentAssertions;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Xunit;
using static RoomTrack.Tests.Bookings.BookingTestData;

namespace RoomTrack.Tests.Bookings.Domain;

/// <summary>The digital check-in done before the arrival (US-13): its window and the identity confirmed at the hotel.</summary>
public class PreArrivalCheckInTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly GuestIdentityDocument Dni = new(IdentityDocumentType.Dni, "45678912", "PE");

    /// <summary>A paid booking whose check-in day is <see cref="BookingTestData.Today"/> + 2.</summary>
    private static Booking ConfirmedBooking()
    {
        var booking = PlaceForGuest();
        booking.Confirm(Now);
        return booking;
    }

    private static DigitalCheckIn PreRegistered(Booking booking) =>
        DigitalCheckIn.PreRegister(booking, Dni, IdentityDocumentFile.Inspect(Jpeg), "documents/1", Now);

    [Fact]
    public void Starts_within_the_window_and_marks_the_booking_in_progress()
    {
        var booking = ConfirmedBooking();

        booking.StartPreArrivalCheckIn(Today, windowDays: 3, Now);

        booking.PreArrivalCheckInAt.Should().Be(Now);
        booking.CheckInStage.Should().Be("InProgress");
        booking.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public void Is_refused_too_early()
    {
        var booking = ConfirmedBooking();

        var early = () => booking.StartPreArrivalCheckIn(Today, windowDays: 1, Now);

        early.Should().Throw<InvalidBookingTransitionException>().Which.Code.Should().Be(BookingErrorCodes.CheckInNotOpenYet);
    }

    [Fact]
    public void Is_refused_twice()
    {
        var booking = ConfirmedBooking();
        booking.StartPreArrivalCheckIn(Today, windowDays: 3, Now);

        var again = () => booking.StartPreArrivalCheckIn(Today, windowDays: 3, Now);

        again.Should().Throw<InvalidBookingTransitionException>().Which.Code.Should().Be(BookingErrorCodes.CheckInAwaitingArrival);
    }

    [Fact]
    public void Is_refused_for_an_unpaid_booking()
    {
        var booking = PlaceForGuest();

        var unpaid = () => booking.StartPreArrivalCheckIn(Today, windowDays: 3, Now);

        unpaid.Should().Throw<InvalidBookingTransitionException>().Which.Code.Should().Be(BookingErrorCodes.CheckInBookingNotConfirmed);
    }

    [Fact]
    public void Waits_for_the_arrival_without_an_access_code()
    {
        var checkIn = PreRegistered(ConfirmedBooking());

        checkIn.Status.Should().Be(CheckInStatus.PendingArrival);
        checkIn.AccessCodeProtected.Should().BeEmpty();
        checkIn.AccessCodeValidUntil.Should().BeNull();
        checkIn.CompletedAt.Should().BeNull();
        checkIn.SubmittedAt.Should().Be(Now);
    }

    [Fact]
    public void Arrival_with_the_same_document_number_grants_the_access_code()
    {
        var checkIn = PreRegistered(ConfirmedBooking());
        var arrival = Now.AddDays(2);

        checkIn.ConfirmArrival(" 45-678-912 ", "protected-code", arrival.AddDays(2), arrival);

        checkIn.Status.Should().Be(CheckInStatus.Approved);
        checkIn.AccessCodeProtected.Should().Be("protected-code");
        checkIn.AccessCodeValidUntil.Should().Be(arrival.AddDays(2));
        checkIn.CompletedAt.Should().Be(arrival);
    }

    [Fact]
    public void Arrival_with_another_document_number_is_refused()
    {
        var checkIn = PreRegistered(ConfirmedBooking());

        var impostor = () => checkIn.ConfirmArrival("87654321", "protected-code", Now.AddDays(4), Now.AddDays(2));

        impostor.Should().Throw<InvalidFieldException>().Which.Violation.Code.Should().Be(BookingErrorCodes.CheckInIdentityMismatch);
        checkIn.Status.Should().Be(CheckInStatus.PendingArrival);
    }

    [Fact]
    public void A_completed_check_in_is_not_confirmed_again()
    {
        var checkIn = PreRegistered(ConfirmedBooking());
        checkIn.ConfirmArrival("45678912", "protected-code", Now.AddDays(4), Now.AddDays(2));

        var again = () => checkIn.ConfirmArrival("45678912", "other-code", Now.AddDays(4), Now.AddDays(2));

        again.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(BookingErrorCodes.CheckInNotAwaitingArrival);
    }

    [Fact]
    public void The_estimated_wait_is_rounded_up_to_whole_minutes()
    {
        var now = Now;

        new RoomTrack.Accommodations.Interfaces.ACL.RoomReadiness(101, "Cleaning", now.AddMinutes(24).AddSeconds(10))
            .EstimatedWaitMinutes(now).Should().Be(25);
        new RoomTrack.Accommodations.Interfaces.ACL.RoomReadiness(101, "Occupied", null).EstimatedWaitMinutes(now).Should().BeNull();
        new RoomTrack.Accommodations.Interfaces.ACL.RoomReadiness(101, "Available", now).EstimatedWaitMinutes(now).Should().Be(0);
    }
}
