using BackendAwRoomTrack.API.Bookings.Infrastructure.Persistence.EFC.Configuration.Extensions;
using BackendAwRoomTrack.API.Payments.Infrastructure.Persistence.EFC.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;
using RoomTrack.Bookings.API.Infrastructure.Concurrency;

namespace RoomTrack.Bookings.API.Infrastructure.Persistence;

/// <summary>Database of the Bookings service: bookings, digital check-ins and their documents (Bookings) and payments.</summary>
public class BookingsDbContext(DbContextOptions<BookingsDbContext> options) : AppDbContext(options)
{
    protected override void ApplyServiceConfiguration(ModelBuilder builder)
    {
        builder.ApplyBookingsConfiguration();
        builder.ApplyPaymentsConfiguration();

        // One row per booked room, locked to serialize the bookings of a room (see RoomBookingLock).
        builder.Entity<RoomBookingLock>(entity =>
        {
            entity.HasKey(roomLock => roomLock.RoomId);
            entity.Property(roomLock => roomLock.RoomId).ValueGeneratedNever();
        });
    }
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public class BookingsDbContextFactory() : DesignTimeDbContextFactory<BookingsDbContext>("roomtrack_bookings");
