using System;
using System.Threading.Tasks;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Bookings.Infrastructure.Persistence.EFC.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RoomTrack.Bookings.API.Infrastructure.Persistence;
using Xunit;
using static BackendAwRoomTrack.API.Tests.Bookings.BookingTestData;

namespace BackendAwRoomTrack.API.Tests.Bookings.Infrastructure;

public class BookingsPersistenceTests
{
    private DbContextOptions<BookingsDbContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<BookingsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task Booking_WithGuestProfileId_ShouldPersist_AndMaterializeCorrectly()
    {
        // Arrange
        var options = CreateNewContextOptions();
        var guestProfileId = Guid.NewGuid();
        int bookingId;

        // 1. Persist
        using (var context = new BookingsDbContext(options))
        {
            var repo = new BookingRepository(context);
            var booking = PlaceByStaff(guestProfileId, roomId: 101);

            await repo.AddAsync(booking);
            await context.SaveChangesAsync();
            bookingId = booking.Id;
        }

        // 2. Query in separate DbContext
        using (var context = new BookingsDbContext(options))
        {
            var repo = new BookingRepository(context);
            var loadedBooking = await repo.FindByIdAsync(bookingId);

            loadedBooking.Should().NotBeNull();
            loadedBooking!.Id.Should().Be(bookingId);
            loadedBooking.RoomId.Should().Be(101);
            loadedBooking.GuestName.Should().Be("Alice Wonderland");
            loadedBooking.GuestEmail.Should().Be("alice@example.com");
            loadedBooking.Status.Should().Be(BookingStatus.Pending);
            loadedBooking.GuestProfileId.Should().Be(guestProfileId);
        }
    }

    [Fact]
    public async Task Booking_WithoutGuestProfileId_ShouldPersist_AndMaterializeWithNullGuestProfileId()
    {
        // Arrange
        var options = CreateNewContextOptions();
        int bookingId;

        // 1. Persist
        using (var context = new BookingsDbContext(options))
        {
            var repo = new BookingRepository(context);
            var booking = PlaceByStaff(guestProfileId: null, roomId: 202);

            await repo.AddAsync(booking);
            await context.SaveChangesAsync();
            bookingId = booking.Id;
        }

        // 2. Query in separate DbContext
        using (var context = new BookingsDbContext(options))
        {
            var repo = new BookingRepository(context);
            var loadedBooking = await repo.FindByIdAsync(bookingId);

            loadedBooking.Should().NotBeNull();
            loadedBooking!.Id.Should().Be(bookingId);
            loadedBooking.RoomId.Should().Be(202);
            loadedBooking.Status.Should().Be(BookingStatus.Pending);
            loadedBooking.GuestProfileId.Should().BeNull();
        }
    }

    [Fact]
    public async Task Booking_StatusTransition_ShouldPersistCorrectly()
    {
        // Arrange
        var options = CreateNewContextOptions();
        int bookingId;

        using (var context = new BookingsDbContext(options))
        {
            var repo = new BookingRepository(context);
            var booking = PlaceByStaff(roomId: 10);

            await repo.AddAsync(booking);
            await context.SaveChangesAsync();
            bookingId = booking.Id;
        }

        // Confirm booking
        using (var context = new BookingsDbContext(options))
        {
            var repo = new BookingRepository(context);
            var booking = await repo.FindByIdAsync(bookingId);
            booking.Should().NotBeNull();
            booking!.Confirm(Now);
            repo.Update(booking);
            await context.SaveChangesAsync();
        }

        // Verify confirmed
        using (var context = new BookingsDbContext(options))
        {
            var repo = new BookingRepository(context);
            var loadedBooking = await repo.FindByIdAsync(bookingId);
            loadedBooking.Should().NotBeNull();
            loadedBooking!.Status.Should().Be(BookingStatus.Confirmed);
        }
    }
}
