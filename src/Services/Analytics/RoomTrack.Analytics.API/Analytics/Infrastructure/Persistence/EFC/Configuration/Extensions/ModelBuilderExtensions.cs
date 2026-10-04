using BackendAwRoomTrack.API.Analytics.Domain.Model.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace BackendAwRoomTrack.API.Analytics.Infrastructure.Persistence.EFC.Configuration.Extensions;

public static class ModelBuilderExtensions
{
    /// <summary>
    ///     Read model of the Analytics service: the rooms, bookings and payments it needs for the metrics, replicated
    ///     from the other services by their integration events. The ids are those of the owning services.
    /// </summary>
    public static void ApplyAnalyticsConfiguration(this ModelBuilder builder)
    {
        builder.Entity<RoomFact>(entity =>
        {
            entity.HasKey(room => room.RoomId);
            entity.Property(room => room.RoomId).ValueGeneratedNever();
            entity.HasIndex(room => room.HotelId);
        });

        builder.Entity<BookingFact>(entity =>
        {
            entity.HasKey(booking => booking.BookingId);
            entity.Property(booking => booking.BookingId).ValueGeneratedNever();
            entity.Property(booking => booking.Status).IsRequired().HasMaxLength(20);
            entity.HasIndex(booking => new { booking.HotelId, booking.CheckInDate });
        });

        builder.Entity<PaymentFact>(entity =>
        {
            entity.HasKey(payment => payment.PaymentId);
            entity.Property(payment => payment.PaymentId).ValueGeneratedNever();
            entity.Property(payment => payment.Amount).HasPrecision(10, 2);
            entity.Property(payment => payment.Status).IsRequired().HasMaxLength(20);
            entity.HasIndex(payment => new { payment.HotelId, payment.PaymentDate });
        });
    }
}
