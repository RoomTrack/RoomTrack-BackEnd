using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Messaging.Domain.Model.Exceptions;

/// <summary>Stable error codes of the messaging context.</summary>
public static class MessagingErrorCodes
{
    public const string BodyLength = "message.body_length";
    public const string AccessDenied = "conversation.access_denied";
    public const string NoCurrentStay = "conversation.no_current_stay";
    public const string NotAnArea = "conversation.not_an_area";
}

/// <summary>The requester is not part of the conversation.</summary>
public class ConversationAccessDeniedException()
    : OperationNotAllowedException(MessagingErrorCodes.AccessDenied, "You are not part of this conversation.");

/// <summary>A guest writes to the front desk of the hotel they are staying at.</summary>
public class NoStayForConversationException()
    : BusinessRuleViolationException(MessagingErrorCodes.NoCurrentStay,
        "You can write to the front desk during your stay (after your booking is confirmed for today).");
