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
        // US-33: the service of the catalog is a reference; the request keeps its price even if the service is removed.
        builder.Entity<GuestRequest>().Property(r => r.ServicePrice).HasColumnType("decimal(10,2)");

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

        // Service catalog of each hotel (US-33)
        builder.Entity<ServiceCatalogItem>().ToTable("service_catalog_items");
        builder.Entity<ServiceCatalogItem>().HasKey(i => i.Id);
        builder.Entity<ServiceCatalogItem>().Property(i => i.Id).ValueGeneratedOnAdd();
        builder.Entity<ServiceCatalogItem>().Property(i => i.Name).HasMaxLength(ServiceCatalogItem.NameMaxLength).IsRequired();
        builder.Entity<ServiceCatalogItem>().Property(i => i.Description).HasMaxLength(ServiceCatalogItem.DescriptionMaxLength);
        builder.Entity<ServiceCatalogItem>().Property(i => i.Area).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<ServiceCatalogItem>().Property(i => i.Price).HasColumnType("decimal(10,2)").IsRequired();
        builder.Entity<ServiceCatalogItem>().Ignore(i => i.IsIncluded);
        builder.Entity<ServiceCatalogItem>().HasIndex(i => new { i.HotelId, i.IsAvailable });
    }
}
