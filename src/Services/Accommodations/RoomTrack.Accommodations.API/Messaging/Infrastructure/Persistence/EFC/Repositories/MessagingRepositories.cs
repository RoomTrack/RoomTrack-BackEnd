using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Messaging.Domain.Model.Entities;
using RoomTrack.Messaging.Domain.Model.ValueObjects;
using RoomTrack.Messaging.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Messaging.Infrastructure.Persistence.EFC.Repositories;

public class ConversationRepository(AppDbContext context) : IConversationRepository
{
    public async Task AddAsync(Conversation conversation) => await context.Set<Conversation>().AddAsync(conversation);

    public Task<Conversation?> FindByIdAsync(int id) => context.Set<Conversation>().FirstOrDefaultAsync(c => c.Id == id);

    public Task<Conversation?> FindByKeyAsync(int hotelId, ConversationKind kind, string key) =>
        context.Set<Conversation>().FirstOrDefaultAsync(c => c.HotelId == hotelId && c.Kind == kind && c.Key == key);

    public Task<Conversation?> FindTaskThreadAsync(string key) =>
        context.Set<Conversation>().FirstOrDefaultAsync(c => c.Kind == ConversationKind.TaskThread && c.Key == key);

    public async Task<IReadOnlyList<Conversation>> ListAsync(int? hotelId, IReadOnlyCollection<ConversationKind> kinds, int limit) =>
        await context.Set<Conversation>()
            .Where(c => (hotelId == null || c.HotelId == hotelId) && kinds.Contains(c.Kind))
            .OrderByDescending(c => c.LastMessageAt).ThenByDescending(c => c.Id)
            .Take(limit)
            .ToListAsync();

    public async Task<IReadOnlyList<Conversation>> ListOfGuestAsync(int guestUserId, int limit) =>
        await context.Set<Conversation>()
            .Where(c => c.Kind == ConversationKind.GuestDesk && c.GuestUserId == guestUserId)
            .OrderByDescending(c => c.LastMessageAt).ThenByDescending(c => c.Id)
            .Take(limit)
            .ToListAsync();
}

public class ConversationMessageRepository(AppDbContext context) : IConversationMessageRepository
{
    public async Task AddAsync(ConversationMessage message) => await context.Set<ConversationMessage>().AddAsync(message);

    public async Task<IReadOnlyList<ConversationMessage>> ListAsync(int conversationId, long? beforeMessageId, int limit) =>
        await context.Set<ConversationMessage>()
            .Where(m => m.ConversationId == conversationId && (beforeMessageId == null || m.Id < beforeMessageId))
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync();

    public async Task<IReadOnlyList<int>> ListSendersAsync(int conversationId) =>
        await context.Set<ConversationMessage>()
            .Where(m => m.ConversationId == conversationId)
            .Select(m => m.SenderUserId)
            .Distinct()
            .ToListAsync();
}
