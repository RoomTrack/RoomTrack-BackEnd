using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.Messaging.Domain.Model.ValueObjects;

namespace RoomTrack.Messaging.Domain.Model.Commands;

/// <summary>A guest writes to the front desk of the hotel of their stay (US-20).</summary>
public record WriteToFrontDeskCommand(string Body, ConversationActor Guest);

/// <summary>Someone of a conversation writes in it (e.g. the front desk answers a guest).</summary>
public record PostMessageCommand(int ConversationId, string Body, ConversationActor Sender);

/// <summary>A staff member writes to the channel of an area of their hotel (US-12, scenario 2).</summary>
public record WriteToAreaCommand(int HotelId, ServiceArea Area, string Body, ConversationActor Sender);

/// <summary>A staff member comments on a task (US-12, scenario 1).</summary>
public record CommentOnTaskCommand(ThreadSubject Subject, int SubjectId, string Body, ConversationActor Sender);
