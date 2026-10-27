using System;
using System.Linq;
using System.Threading.Tasks;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Shared.Domain.Model.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RoomTrack.Bookings.API.Infrastructure.Persistence;
using Xunit;
using static RoomTrack.Tests.Bookings.BookingTestData;

namespace RoomTrack.Tests.Bookings.Infrastructure;

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

    [Fact]
    public async Task ListPage_ShouldReturnThatPageNewestFirst_AndTheTotalOfTheList()
    {
        var options = CreateNewContextOptions();
        using (var context = new BookingsDbContext(options))
        {
            for (var roomId = 101; roomId <= 105; roomId++)
                await context.AddAsync(PlaceByStaff(guestProfileId: null, roomId: roomId));
            await context.SaveChangesAsync();
        }

        using (var context = new BookingsDbContext(options))
        {
            var repo = new BookingRepository(context);

            var second = await repo.ListPageNewestFirstAsync(HotelId, new PageRequest(2, 2));
            var otherHotel = await repo.ListPageNewestFirstAsync(HotelId + 1, new PageRequest(1, 2));

            second.TotalCount.Should().Be(5);
            second.Items.Select(b => b.RoomId).Should().Equal(103, 102);
            otherHotel.TotalCount.Should().Be(0);
            otherHotel.Items.Should().BeEmpty();
        }
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, PageRequest.MaxPageSize + 1)]
    public void PageRequest_OutOfRange_ShouldBeRejected(int page, int pageSize)
    {
        var act = () => new PageRequest(page, pageSize);

        act.Should().Throw<InvalidFieldException>();
    }
}

