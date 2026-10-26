using System.Globalization;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.GuestRequests.Domain.Repositories;
using RoomTrack.Housekeeping.Domain.Repositories;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Incidents.Domain.Repositories;
using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Messaging.Domain.Model.Events;
using RoomTrack.Messaging.Domain.Model.ValueObjects;
using RoomTrack.Messaging.Domain.Repositories;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Contracts.Messaging;

namespace RoomTrack.Messaging.Application.Internal.EventHandlers;

/// <summary>
///     Notifies the other side of a new message (US-20 scenario 2, US-12): the front desk of the hotel when a guest
///     writes and the guest when the front desk answers; everyone of the area in an area channel; the person in charge
///     of the task and whoever already commented in a task thread. The sender is never notified.
/// </summary>
public class MessagePushHandler(
    IIamContextFacade iamContextFacade,
    IConversationMessageRepository messageRepository,
    ICleaningTaskRepository cleaningTaskRepository,
    IIncidentRepository incidentRepository,
    IGuestRequestRepository guestRequestRepository,
    IPushSender pushSender) : IDomainEventHandler<MessagePostedEvent>
{
    public async Task HandleAsync(MessagePostedEvent e, CancellationToken cancellationToken)
    {
        var conversation = e.Conversation;
        var recipients = (await RecipientsAsync(conversation, e))
            .Where(userId => userId != e.SenderUserId).Distinct().ToList();
        await pushSender.SendAsync(new PushMessage(recipients, PushCategories.Message, TitleOf(conversation, e), e.Preview,
            new Dictionary<string, string>
            {
                ["screen"] = "conversation",
                ["id"] = conversation.Id.ToString(CultureInfo.InvariantCulture)
            }), cancellationToken);
    }

    private async Task<IEnumerable<int>> RecipientsAsync(Conversation conversation, MessagePostedEvent e)
    {
        switch (conversation.Kind)
        {
            case ConversationKind.GuestDesk when e.SenderIsGuest:
                var frontDesk = await iamContextFacade.ListHotelStaffAsync(conversation.HotelId, [UserRoles.Reception]);
                // A hotel without reception staff: its administrators answer.
                if (frontDesk.Count == 0)
                    frontDesk = await iamContextFacade.ListHotelStaffAsync(conversation.HotelId, [UserRoles.Admin]);
                return frontDesk.Select(contact => contact.UserId);
            case ConversationKind.GuestDesk:
                return conversation.GuestUserId is { } guest ? [guest] : [];
            case ConversationKind.AreaChannel:
                return (await iamContextFacade.ListHotelStaffAsync(conversation.HotelId,
                    [ServiceAreas.RoleOf(conversation.Area!.Value)])).Select(contact => contact.UserId);
            default:
                var participants = (await messageRepository.ListSendersAsync(conversation.Id)).ToList();
                if (await AssigneeOfTaskAsync(conversation) is { } assignee) participants.Add(assignee);
                return participants;
        }
    }

    private async Task<int?> AssigneeOfTaskAsync(Conversation conversation) => conversation.Subject switch
    {
        ThreadSubject.CleaningTask => (await cleaningTaskRepository.FindByIdAsync(conversation.SubjectId!.Value))?.AssigneeUserId,
        ThreadSubject.Incident => (await incidentRepository.FindByIdAsync(conversation.SubjectId!.Value))?.AssigneeUserId,
        ThreadSubject.GuestRequest => (await guestRequestRepository.FindByIdAsync(conversation.SubjectId!.Value))?.AssigneeUserId,
        _ => null
    };

    private static string TitleOf(Conversation conversation, MessagePostedEvent e) => conversation.Kind switch
    {
        ConversationKind.GuestDesk when e.SenderIsGuest => $"Mensaje de huésped: {e.SenderName}",
        ConversationKind.GuestDesk => "Respuesta de recepción",
        ConversationKind.AreaChannel => $"Canal de {conversation.Area}: {e.SenderName}",
        _ => $"Comentario de {e.SenderName}"
    };
}
