using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Incidents.Infrastructure.Persistence.EFC.Configuration.Extensions;

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
        builder.Entity<Incident>().Property(i => i.Version).IsConcurrencyToken();
        builder.Entity<Incident>().Ignore(i => i.IsOpen);
        builder.Entity<Incident>().Ignore(i => i.DomainEvents);
        builder.Entity<Incident>().HasIndex(i => new { i.HotelId, i.Status });
        builder.Entity<Incident>().HasIndex(i => new { i.RoomId, i.Status });
        builder.Entity<Incident>().HasIndex(i => new { i.AssigneeUserId, i.Status });
        builder.Entity<Incident>().Property(i => i.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<Incident>().HasIndex(i => new { i.RoomId, i.ReportedAt });
        builder.Entity<Incident>().HasIndex(i => i.PreventivePlanId);

        // History, append-only, owned by the incident
        builder.Entity<Incident>().HasMany(i => i.History).WithOne().HasForeignKey(h => h.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Incident>().Navigation(i => i.History).UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_history");

        builder.Entity<IncidentHistoryEntry>().ToTable("incident_history");
        builder.Entity<IncidentHistoryEntry>().HasKey(h => h.Id);
        builder.Entity<IncidentHistoryEntry>().Property(h => h.Id).ValueGeneratedOnAdd();
        builder.Entity<IncidentHistoryEntry>().Property(h => h.Action).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Entity<IncidentHistoryEntry>().Property(h => h.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<IncidentHistoryEntry>().Property(h => h.Detail).HasMaxLength(IncidentHistoryEntry.MaxDetailLength);
        builder.Entity<IncidentHistoryEntry>().Property(h => h.ActorEmail).HasMaxLength(IncidentHistoryEntry.MaxEmailLength);
        builder.Entity<IncidentHistoryEntry>().HasIndex(h => new { h.IncidentId, h.OccurredAt });
    }
}

public static class PreventiveMaintenanceModelBuilderExtensions
{
    /// <summary>Preventive maintenance plans of the rooms (US-30). The room is a logical reference (id).</summary>
    public static void ApplyPreventiveMaintenanceConfiguration(this ModelBuilder builder)
    {
        builder.Entity<PreventiveMaintenancePlan>().ToTable("preventive_maintenance_plans");
        builder.Entity<PreventiveMaintenancePlan>().HasKey(p => p.Id);
        builder.Entity<PreventiveMaintenancePlan>().Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Entity<PreventiveMaintenancePlan>().Property(p => p.Title).IsRequired().HasMaxLength(PreventiveMaintenancePlan.TitleMaxLength);
        builder.Entity<PreventiveMaintenancePlan>().Property(p => p.Description).HasMaxLength(PreventiveMaintenancePlan.DescriptionMaxLength);
        builder.Entity<PreventiveMaintenancePlan>().Ignore(p => p.DomainEvents);
        builder.Entity<PreventiveMaintenancePlan>().HasIndex(p => new { p.HotelId, p.IsActive });
        builder.Entity<PreventiveMaintenancePlan>().HasIndex(p => new { p.IsActive, p.NextDueOn });
        builder.Entity<PreventiveMaintenancePlan>().HasIndex(p => p.RoomId);
    }
}
