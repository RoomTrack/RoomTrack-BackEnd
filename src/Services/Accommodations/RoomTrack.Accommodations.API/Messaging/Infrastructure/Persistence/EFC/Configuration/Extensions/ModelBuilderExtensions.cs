using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Messaging.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Messaging.Infrastructure.Persistence.EFC.Configuration.Extensions;

public static class ModelBuilderExtensions
{
    /// <summary>
    ///     Tables of the messaging context. One conversation per hotel, kind and key (the guest, the area or the task);
    ///     users and tasks are logical references (ids).
    /// </summary>
    public static void ApplyMessagingConfiguration(this ModelBuilder builder)
    {
        builder.Entity<Conversation>().ToTable("conversations");
        builder.Entity<Conversation>().HasKey(c => c.Id);
        builder.Entity<Conversation>().Property(c => c.Id).ValueGeneratedOnAdd();
        builder.Entity<Conversation>().Property(c => c.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<Conversation>().Property(c => c.Key).HasMaxLength(Conversation.MaxKeyLength).IsRequired();
        builder.Entity<Conversation>().Property(c => c.Area).HasConversion<string>().HasMaxLength(20);
        builder.Entity<Conversation>().Property(c => c.Subject).HasConversion<string>().HasMaxLength(20);
        builder.Entity<Conversation>().Ignore(c => c.DomainEvents);
        builder.Entity<Conversation>().HasIndex(c => new { c.HotelId, c.Kind, c.Key }).IsUnique();
        builder.Entity<Conversation>().HasIndex(c => new { c.Kind, c.Key });
        builder.Entity<Conversation>().HasIndex(c => c.GuestUserId);

        builder.Entity<ConversationMessage>().ToTable("conversation_messages");
        builder.Entity<ConversationMessage>().HasKey(m => m.Id);
        builder.Entity<ConversationMessage>().Property(m => m.Id).ValueGeneratedOnAdd();
        builder.Entity<ConversationMessage>().Property(m => m.Body).HasMaxLength(ConversationMessage.MaxBodyLength).IsRequired();
        builder.Entity<ConversationMessage>().Property(m => m.SenderName).HasMaxLength(ConversationMessage.MaxNameLength).IsRequired();
        builder.Entity<ConversationMessage>().Ignore(m => m.Preview);
        builder.Entity<ConversationMessage>().HasOne(m => m.Conversation).WithMany().HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ConversationMessage>().HasIndex(m => new { m.ConversationId, m.Id });
    }
}
