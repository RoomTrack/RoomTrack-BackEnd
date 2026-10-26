using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Messaging.Domain.Model.Commands;
using RoomTrack.Messaging.Domain.Model.Entities;
using RoomTrack.Messaging.Domain.Model.Queries;

namespace RoomTrack.Messaging.Domain.Services;

public interface IMessagingCommandService
{
    Task<ConversationMessage> Handle(WriteToFrontDeskCommand command);

    /// <summary>Null when the conversation does not exist.</summary>
    Task<ConversationMessage?> Handle(PostMessageCommand command);

    Task<ConversationMessage> Handle(WriteToAreaCommand command);

    /// <summary>Null when the task does not exist.</summary>
    Task<ConversationMessage?> Handle(CommentOnTaskCommand command);
}

public interface IMessagingQueryService
{
    Task<IReadOnlyList<Conversation>> Handle(GetConversationsQuery query);
    Task<Conversation?> Handle(GetConversationByIdQuery query);
    Task<Conversation?> Handle(GetTaskThreadQuery query);
    Task<IReadOnlyList<ConversationMessage>> Handle(GetMessagesQuery query);
}
