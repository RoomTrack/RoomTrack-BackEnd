using RoomTrack.Messaging.Domain.Model.ValueObjects;

namespace RoomTrack.Messaging.Domain.Model.Queries;

/// <summary>
///     The conversations the requester takes part in, latest activity first: a guest their conversations with the front
///     desk; staff the area channels and, for the front desk, the conversations of the guests of the hotel.
/// </summary>
public record GetConversationsQuery(ConversationActor Actor, int? HotelId = null);

public record GetConversationByIdQuery(int ConversationId);

/// <summary>The comments on a task (null when nobody commented yet).</summary>
public record GetTaskThreadQuery(ThreadSubject Subject, int SubjectId);

/// <summary>A page of the messages of a conversation, newest first, before <paramref name="BeforeMessageId"/> when given.</summary>
public record GetMessagesQuery(int ConversationId, long? BeforeMessageId = null, int Limit = 50);
