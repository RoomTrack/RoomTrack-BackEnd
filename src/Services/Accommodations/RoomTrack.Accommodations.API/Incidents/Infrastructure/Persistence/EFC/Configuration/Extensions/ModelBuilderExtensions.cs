using BackendAwRoomTrack.API.Incidents.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace BackendAwRoomTrack.API.Incidents.Infrastructure.Persistence.EFC.Configuration.Extensions;

public static class ModelBuilderExtensions
{
    /// <summary>
    ///     Tables of the Incidents context. The room and the users are logical references (ids), not foreign keys:
    ///     the incident keeps its history even if the room is removed.
    /// </summary>
    public static void ApplyIncidentsConfiguration(this ModelBuilder builder)
    {
        builder.Entity<Incident>().ToTable("incidents");
        builder.Entity<Incident>().HasKey(i => i.Id);
        builder.Entity<Incident>().Property(i => i.Id).ValueGeneratedOnAdd();
        builder.Entity<Incident>().Property(i => i.Title).IsRequired().HasMaxLength(Incident.TitleMaxLength);
        builder.Entity<Incident>().Property(i => i.Description).HasMaxLength(Incident.DescriptionMaxLength);
        builder.Entity<Incident>().Property(i => i.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<Incident>().Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<Incident>().Property(i => i.ReportedByEmail).HasMaxLength(Incident.MaxEmailLength);
        builder.Entity<Incident>().Property(i => i.ResolutionNotes).HasMaxLength(Incident.ResolutionNotesMaxLength);
        builder.Entity<Incident>().Ignore(i => i.IsOpen);
        builder.Entity<Incident>().Ignore(i => i.DomainEvents);
        builder.Entity<Incident>().HasIndex(i => new { i.HotelId, i.Status });
        builder.Entity<Incident>().HasIndex(i => new { i.RoomId, i.Status });
        builder.Entity<Incident>().HasIndex(i => new { i.AssigneeUserId, i.Status });

        // History, append-only, owned by the incident
        builder.Entity<Incident>().HasMany(i => i.History).WithOne().HasForeignKey(h => h.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Incident>().Navigation(i => i.History).UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_history");

        builder.Entity<IncidentHistoryEntry>().ToTable("incident_history");
        builder.Entity<IncidentHistoryEntry>().HasKey(h => h.Id);
        builder.Entity<IncidentHistoryEntry>().Property(h => h.Id).ValueGeneratedOnAdd();
        builder.Entity<IncidentHistoryEntry>().Property(h => h.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<IncidentHistoryEntry>().Property(h => h.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<IncidentHistoryEntry>().Property(h => h.Detail).HasMaxLength(IncidentHistoryEntry.MaxDetailLength);
        builder.Entity<IncidentHistoryEntry>().Property(h => h.ActorEmail).HasMaxLength(IncidentHistoryEntry.MaxEmailLength);
        builder.Entity<IncidentHistoryEntry>().HasIndex(h => new { h.IncidentId, h.OccurredAt });
    }
}
