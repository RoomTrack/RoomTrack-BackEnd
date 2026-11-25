using Microsoft.EntityFrameworkCore;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using RoomTrack.ShiftHandovers.Domain.Model.Aggregates;
using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;
using RoomTrack.ShiftHandovers.Domain.Repositories;

namespace RoomTrack.ShiftHandovers.Infrastructure.Persistence.EFC;

public static class ModelBuilderExtensions
{
    /// <summary>Tables of the ShiftHandovers context (US-32). Tasks, incidents and users are logical references (ids).</summary>
    public static void ApplyShiftHandoversConfiguration(this ModelBuilder builder)
    {
        builder.Entity<ShiftHandover>().ToTable("shift_handovers");
        builder.Entity<ShiftHandover>().HasKey(h => h.Id);
        builder.Entity<ShiftHandover>().Property(h => h.Id).ValueGeneratedOnAdd();
        builder.Entity<ShiftHandover>().Property(h => h.Area).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<ShiftHandover>().Property(h => h.Notes).HasMaxLength(ShiftHandover.NotesMaxLength);
        builder.Entity<ShiftHandover>().Property(h => h.AuthorEmail).HasMaxLength(ShiftHandover.MaxEmailLength);
        builder.Entity<ShiftHandover>().Ignore(h => h.DomainEvents);
        builder.Entity<ShiftHandover>().HasIndex(h => new { h.HotelId, h.Area, h.CreatedAt });

        builder.Entity<ShiftHandover>().HasMany(h => h.Items).WithOne().HasForeignKey(i => i.ShiftHandoverId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ShiftHandover>().Navigation(h => h.Items).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_items");
        builder.Entity<ShiftHandoverItem>().ToTable("shift_handover_items");
        builder.Entity<ShiftHandoverItem>().HasKey(i => i.Id);
        builder.Entity<ShiftHandoverItem>().Property(i => i.Id).ValueGeneratedOnAdd();
        builder.Entity<ShiftHandoverItem>().Property(i => i.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<ShiftHandoverItem>().Property(i => i.Summary).HasMaxLength(ShiftHandoverItem.SummaryMaxLength).IsRequired();
        builder.Entity<ShiftHandoverItem>().Property(i => i.Status).HasMaxLength(20).IsRequired();
        builder.Entity<ShiftHandoverItem>().Property(i => i.Priority).HasMaxLength(20).IsRequired();

        builder.Entity<ShiftHandover>().HasMany(h => h.Acknowledgements).WithOne().HasForeignKey(a => a.ShiftHandoverId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ShiftHandover>().Navigation(h => h.Acknowledgements).UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_acknowledgements");
        builder.Entity<ShiftHandoverAcknowledgement>().ToTable("shift_handover_acknowledgements");
        builder.Entity<ShiftHandoverAcknowledgement>().HasKey(a => a.Id);
        builder.Entity<ShiftHandoverAcknowledgement>().Property(a => a.Id).ValueGeneratedOnAdd();
        builder.Entity<ShiftHandoverAcknowledgement>().Property(a => a.Email).HasMaxLength(ShiftHandover.MaxEmailLength);
        builder.Entity<ShiftHandoverAcknowledgement>().HasIndex(a => new { a.ShiftHandoverId, a.UserId }).IsUnique();
    }
}

public class ShiftHandoverRepository(AppDbContext context) : IShiftHandoverRepository
{
    public async Task AddAsync(ShiftHandover handover) => await context.Set<ShiftHandover>().AddAsync(handover);

    public Task<ShiftHandover?> FindByIdAsync(int id) =>
        context.Set<ShiftHandover>().Include(h => h.Items).Include(h => h.Acknowledgements).FirstOrDefaultAsync(h => h.Id == id);

    public async Task<IReadOnlyList<ShiftHandover>> ListAsync(int hotelId, ShiftArea? area, int limit)
    {
        var handovers = context.Set<ShiftHandover>().Where(h => h.HotelId == hotelId);
        if (area is { } value) handovers = handovers.Where(h => h.Area == value);
        return await handovers.Include(h => h.Items).Include(h => h.Acknowledgements)
            .OrderByDescending(h => h.CreatedAt).ThenByDescending(h => h.Id)
            .Take(limit)
            .AsSplitQuery()
            .ToListAsync();
    }
}
