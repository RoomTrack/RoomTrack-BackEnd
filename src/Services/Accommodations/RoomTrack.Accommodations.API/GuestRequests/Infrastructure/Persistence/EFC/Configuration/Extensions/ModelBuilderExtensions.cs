using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.GuestRequests.Infrastructure.Persistence.EFC.Configuration.Extensions;

public static class ModelBuilderExtensions
{
    /// <summary>
    ///     Tables of the guest requests context. The room, the booking and the users are logical references (ids): the
    ///     request keeps its history even if the room is removed.
    /// </summary>
    public static void ApplyGuestRequestsConfiguration(this ModelBuilder builder)
    {
        builder.Entity<GuestRequest>().ToTable("guest_requests");
        builder.Entity<GuestRequest>().HasKey(r => r.Id);
        builder.Entity<GuestRequest>().Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Entity<GuestRequest>().Property(r => r.Title).HasMaxLength(GuestRequest.TitleMaxLength).IsRequired();
        builder.Entity<GuestRequest>().Property(r => r.Description).HasMaxLength(GuestRequest.DescriptionMaxLength);
        builder.Entity<GuestRequest>().Property(r => r.ClosingNote).HasMaxLength(GuestRequest.NoteMaxLength);
        builder.Entity<GuestRequest>().Property(r => r.Area).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<GuestRequest>().Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<GuestRequest>().Property(r => r.Version).IsConcurrencyToken();
        builder.Entity<GuestRequest>().Ignore(r => r.IsOpen);
        builder.Entity<GuestRequest>().Ignore(r => r.DomainEvents);
        builder.Entity<GuestRequest>().HasIndex(r => new { r.HotelId, r.Status });
        builder.Entity<GuestRequest>().HasIndex(r => new { r.HotelId, r.Area, r.Status });
        builder.Entity<GuestRequest>().HasIndex(r => r.GuestUserId);

        builder.Entity<GuestRequest>().HasMany(r => r.History).WithOne().HasForeignKey(h => h.GuestRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<GuestRequest>().Navigation(r => r.History).UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_history");

        builder.Entity<GuestRequestHistoryEntry>().ToTable("guest_request_history");
        builder.Entity<GuestRequestHistoryEntry>().HasKey(h => h.Id);
        builder.Entity<GuestRequestHistoryEntry>().Property(h => h.Id).ValueGeneratedOnAdd();
        builder.Entity<GuestRequestHistoryEntry>().Property(h => h.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<GuestRequestHistoryEntry>().Property(h => h.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<GuestRequestHistoryEntry>().Property(h => h.Detail).HasMaxLength(GuestRequestHistoryEntry.MaxDetailLength);
        builder.Entity<GuestRequestHistoryEntry>().Property(h => h.ActorEmail).HasMaxLength(GuestRequestHistoryEntry.MaxEmailLength);
        builder.Entity<GuestRequestHistoryEntry>().HasIndex(h => new { h.GuestRequestId, h.OccurredAt });
    }
}
