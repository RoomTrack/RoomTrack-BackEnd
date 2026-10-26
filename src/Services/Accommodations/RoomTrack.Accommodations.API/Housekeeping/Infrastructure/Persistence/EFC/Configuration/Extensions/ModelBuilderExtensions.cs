using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Housekeeping.Infrastructure.Persistence.EFC.Configuration.Extensions;

public static class ModelBuilderExtensions
{
    /// <summary>
    ///     Tables of the Housekeeping context. The room, the hotel, the users and the incidents are logical references
    ///     (ids), not foreign keys: the task keeps its history even if the room is removed.
    /// </summary>
    public static void ApplyHousekeepingConfiguration(this ModelBuilder builder)
    {
        builder.Entity<CleaningTask>().ToTable("cleaning_tasks");
        builder.Entity<CleaningTask>().HasKey(t => t.Id);
        builder.Entity<CleaningTask>().Property(t => t.Id).ValueGeneratedOnAdd();
        builder.Entity<CleaningTask>().Property(t => t.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<CleaningTask>().Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<CleaningTask>().Property(t => t.Notes).HasMaxLength(CleaningTask.NotesMaxLength);
        builder.Entity<CleaningTask>().Property(t => t.CancellationReason).HasMaxLength(CleaningTask.CancellationReasonMaxLength);
        builder.Entity<CleaningTask>().Property(t => t.Version).IsConcurrencyToken();
        builder.Entity<CleaningTask>().Ignore(t => t.IsOpen);
        builder.Entity<CleaningTask>().Ignore(t => t.ImpedimentIncidentIds);
        builder.Entity<CleaningTask>().Ignore(t => t.DomainEvents);
        builder.Entity<CleaningTask>().HasIndex(t => new { t.HotelId, t.Status });
        builder.Entity<CleaningTask>().HasIndex(t => new { t.RoomId, t.Status });
        builder.Entity<CleaningTask>().HasIndex(t => new { t.AssigneeUserId, t.Status });

        // History, append-only, owned by the task
        builder.Entity<CleaningTask>().HasMany(t => t.History).WithOne().HasForeignKey(h => h.CleaningTaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<CleaningTask>().Navigation(t => t.History).UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_history");

        builder.Entity<CleaningTaskHistoryEntry>().ToTable("cleaning_task_history");
        builder.Entity<CleaningTaskHistoryEntry>().HasKey(h => h.Id);
        builder.Entity<CleaningTaskHistoryEntry>().Property(h => h.Id).ValueGeneratedOnAdd();
        builder.Entity<CleaningTaskHistoryEntry>().Property(h => h.Action).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Entity<CleaningTaskHistoryEntry>().Property(h => h.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<CleaningTaskHistoryEntry>().Property(h => h.Detail).HasMaxLength(CleaningTaskHistoryEntry.MaxDetailLength);
        builder.Entity<CleaningTaskHistoryEntry>().Property(h => h.ActorEmail).HasMaxLength(CleaningTaskHistoryEntry.MaxEmailLength);
        builder.Entity<CleaningTaskHistoryEntry>().HasIndex(h => new { h.CleaningTaskId, h.OccurredAt });

        builder.Entity<HotelHousekeepingSettings>().ToTable("hotel_housekeeping_settings");
        builder.Entity<HotelHousekeepingSettings>().HasKey(s => s.HotelId);
        builder.Entity<HotelHousekeepingSettings>().Property(s => s.HotelId).ValueGeneratedNever();
    }
}
