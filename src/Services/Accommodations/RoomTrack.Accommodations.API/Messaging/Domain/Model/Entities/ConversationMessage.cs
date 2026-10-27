using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Messaging.Domain.Model.Exceptions;
using RoomTrack.Messaging.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Messaging.Domain.Model.Entities;

/// <summary>A message of a conversation. Never modified (the history of a closed task keeps every comment).</summary>
public class ConversationMessage
{
    public const int MaxBodyLength = 2000;
    public const int MaxNameLength = 120;
    public const int PreviewLength = 140;

    /// <summary>EF Core constructor.</summary>
    protected ConversationMessage()
    {
        Body = string.Empty;
        SenderName = string.Empty;
    }

    internal ConversationMessage(Conversation conversation, ConversationActor sender, string body, DateTimeOffset now)
    {
        var trimmed = body?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxBodyLength)
            throw new InvalidFieldException("body", MessagingErrorCodes.BodyLength,
                $"Write a message of 1 to {MaxBodyLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = MaxBodyLength });

        Conversation = conversation;
        SenderUserId = sender.UserId;
        SenderName = sender.DisplayName.Length > MaxNameLength ? sender.DisplayName[..MaxNameLength] : sender.DisplayName;
        SenderIsGuest = sender.IsGuest;
        Body = trimmed;
        SentAt = now;
    }

    public long Id { get; private set; }
    public int ConversationId { get; private set; }

    /// <summary>EF Core navigation, so a message of a new conversation is saved with it.</summary>
    public Conversation? Conversation { get; private set; }

    public int SenderUserId { get; private set; }
    public string SenderName { get; private set; }
    public bool SenderIsGuest { get; private set; }
    public string Body { get; private set; }
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>The beginning of the message, for the notifications.</summary>
    public string Preview => Body.Length <= PreviewLength ? Body : Body[..(PreviewLength - 1)] + "…";
}
