using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Shared.Domain.Model.Events;

namespace RoomTrack.Messaging.Domain.Model.Events;

/// <summary>
///     A message was written in a conversation: the other side is notified. Carries the conversation (not its id): the
///     first message of a conversation is published before the handlers could load it by id.
/// </summary>
public sealed record MessagePostedEvent(Conversation Conversation, string Preview, int SenderUserId, bool SenderIsGuest,
    string SenderName, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);
