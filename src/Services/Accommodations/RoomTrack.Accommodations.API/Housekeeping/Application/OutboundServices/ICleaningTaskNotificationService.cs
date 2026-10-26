using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.IAM.Interfaces.ACL;

namespace RoomTrack.Housekeeping.Application.OutboundServices;

/// <summary>The cleaning task (and its room) an e-mail is about.</summary>
public sealed record CleaningTaskNotice(int CleaningTaskId, CleaningTaskPriority Priority, string? Notes, string RoomNumber,
    string HotelName);

/// <summary>E-mails to the hotel staff about cleaning tasks. Enlisted in the outbox inside the transaction of the change.</summary>
public interface ICleaningTaskNotificationService
{
    /// <summary>The task was assigned to the recipient: it is in their list now (US-05).</summary>
    Task SendAssignedAsync(UserContact assignee, CleaningTaskNotice task, string? assignedBy);

    /// <summary>The task was reassigned: it left the recipient's list (US-05).</summary>
    Task SendUnassignedAsync(UserContact previousAssignee, CleaningTaskNotice task, string? reassignedBy);

    /// <summary>The room was cleaned and is Available: reception can assign it to a new guest (US-06).</summary>
    Task SendRoomReadyAsync(IReadOnlyList<UserContact> recipients, CleaningTaskNotice task, string? cleanedBy);
}
