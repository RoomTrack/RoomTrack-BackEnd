using RoomTrack.Notifications.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Notifications.Infrastructure.Persistence.EFC.Configuration.Extensions;

public static class ModelBuilderExtensions
{
    /// <summary>Tables of the push notifications context. Users are logical references (ids).</summary>
    public static void ApplyNotificationsConfiguration(this ModelBuilder builder)
    {
        builder.Entity<PushDevice>().ToTable("push_devices");
        builder.Entity<PushDevice>().HasKey(d => d.Id);
        builder.Entity<PushDevice>().Property(d => d.Id).ValueGeneratedOnAdd();
        builder.Entity<PushDevice>().Property(d => d.Token).HasMaxLength(PushDevice.MaxTokenLength).IsRequired();
        builder.Entity<PushDevice>().Property(d => d.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Entity<PushDevice>().Property(d => d.Platform).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<PushDevice>().HasIndex(d => d.TokenHash).IsUnique();
        builder.Entity<PushDevice>().HasIndex(d => d.UserId);

        builder.Entity<NotificationPreferences>().ToTable("notification_preferences");
        builder.Entity<NotificationPreferences>().HasKey(p => p.UserId);
        builder.Entity<NotificationPreferences>().Property(p => p.UserId).ValueGeneratedNever();
        // The kinds turned off, comma-separated (a handful of short codes).
        builder.Entity<NotificationPreferences>().Property(p => p.DisabledCategoryCodes).HasMaxLength(500).IsRequired();
        builder.Entity<NotificationPreferences>().Ignore(p => p.DisabledCategories);
    }
}
