using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Messaging.Domain.Model.Entities;
using RoomTrack.Messaging.Domain.Model.ValueObjects;

namespace RoomTrack.Messaging.Domain.Repositories;

public interface IConversationRepository
{
    Task AddAsync(Conversation conversation);
    Task<Conversation?> FindByIdAsync(int id);
    Task<Conversation?> FindByKeyAsync(int hotelId, ConversationKind kind, string key);

    /// <summary>The thread of a task (its key is unique: a task belongs to one hotel).</summary>
    Task<Conversation?> FindTaskThreadAsync(string key);

    /// <summary>The conversations of these kinds of a hotel (or of every hotel when null), latest activity first.</summary>
    Task<IReadOnlyList<Conversation>> ListAsync(int? hotelId, IReadOnlyCollection<ConversationKind> kinds, int limit);

    /// <summary>The conversations of a guest with the front desk, latest activity first.</summary>
    Task<IReadOnlyList<Conversation>> ListOfGuestAsync(int guestUserId, int limit);
}

public interface IConversationMessageRepository
{
    Task AddAsync(ConversationMessage message);

    /// <summary>Newest first, before <paramref name="beforeMessageId"/> when given.</summary>
    Task<IReadOnlyList<ConversationMessage>> ListAsync(int conversationId, long? beforeMessageId, int limit);

    /// <summary>Who wrote in the conversation (to notify the participants of a task thread).</summary>
    Task<IReadOnlyList<int>> ListSendersAsync(int conversationId);
}
