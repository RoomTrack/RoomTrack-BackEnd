using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Messaging.Domain.Model.Entities;
using RoomTrack.Messaging.Domain.Model.Queries;
using RoomTrack.Messaging.Domain.Model.ValueObjects;
using RoomTrack.Messaging.Domain.Repositories;
using RoomTrack.Messaging.Domain.Services;

namespace RoomTrack.Messaging.Application.Internal.QueryServices;

public class MessagingQueryService(
    IConversationRepository conversationRepository,
    IConversationMessageRepository messageRepository) : IMessagingQueryService
{
    private const int MaxConversations = 200;

    public async Task<IReadOnlyList<Conversation>> Handle(GetConversationsQuery query)
    {
        var actor = query.Actor;
        if (actor.IsGuest) return await conversationRepository.ListOfGuestAsync(actor.UserId, MaxConversations);

        // The front desk answers the guests; everyone reads the channels of the areas they can access. The task threads
        // are read from their task.
        ConversationKind[] kinds = actor.IsFrontDesk
            ? [ConversationKind.GuestDesk, ConversationKind.AreaChannel]
            : [ConversationKind.AreaChannel];
        var hotelId = actor.IsChainAdmin ? query.HotelId : actor.HotelId;
        var conversations = await conversationRepository.ListAsync(hotelId, kinds, MaxConversations);
        return conversations.Where(conversation => conversation.CanAccess(actor)).ToList();
    }

    public Task<Conversation?> Handle(GetConversationByIdQuery query) => conversationRepository.FindByIdAsync(query.ConversationId);

    public Task<Conversation?> Handle(GetTaskThreadQuery query) =>
        conversationRepository.FindTaskThreadAsync(Conversation.KeyOfTask(query.Subject, query.SubjectId));

    public Task<IReadOnlyList<ConversationMessage>> Handle(GetMessagesQuery query) =>
        messageRepository.ListAsync(query.ConversationId, query.BeforeMessageId, Math.Clamp(query.Limit, 1, 200));
}
