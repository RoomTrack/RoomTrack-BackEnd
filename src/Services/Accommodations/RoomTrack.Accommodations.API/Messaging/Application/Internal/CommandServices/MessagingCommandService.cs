using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.GuestRequests.Domain.Repositories;
using RoomTrack.Housekeeping.Domain.Repositories;
using RoomTrack.Incidents.Domain.Repositories;
using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Messaging.Domain.Model.Commands;
using RoomTrack.Messaging.Domain.Model.Entities;
using RoomTrack.Messaging.Domain.Model.Exceptions;
using RoomTrack.Messaging.Domain.Model.ValueObjects;
using RoomTrack.Messaging.Domain.Repositories;
using RoomTrack.Messaging.Domain.Services;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.Messaging.Application.Internal.CommandServices;

/// <summary>
///     Writes the messages of the conversations (US-12, US-20). The conversation of a guest, an area or a task is created
///     with its first message; the other side is notified by the push handler, in the same transaction.
/// </summary>
public class MessagingCommandService(
    IConversationRepository conversationRepository,
    IConversationMessageRepository messageRepository,
    IRoomReservationsFacade roomReservationsFacade,
    ICleaningTaskRepository cleaningTaskRepository,
    IIncidentRepository incidentRepository,
    IGuestRequestRepository guestRequestRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IMessagingCommandService
{
    public async Task<ConversationMessage> Handle(WriteToFrontDeskCommand command)
    {
        // The front desk of the hotel of the guest's stay today.
        var stay = await roomReservationsFacade.FetchCurrentStayAsync(command.Guest.UserId)
                   ?? throw new NoStayForConversationException();
        var conversation = await conversationRepository.FindByKeyAsync(stay.HotelId, ConversationKind.GuestDesk,
                               Conversation.KeyOfGuest(command.Guest.UserId))
                           ?? Conversation.WithFrontDesk(stay.HotelId, command.Guest.UserId, timeProvider.GetUtcNow());
        return await PostAsync(conversation, command.Guest, command.Body);
    }

    public async Task<ConversationMessage?> Handle(PostMessageCommand command)
    {
        var conversation = await conversationRepository.FindByIdAsync(command.ConversationId);
        return conversation is null ? null : await PostAsync(conversation, command.Sender, command.Body);
    }

    public async Task<ConversationMessage> Handle(WriteToAreaCommand command)
    {
        var conversation = await conversationRepository.FindByKeyAsync(command.HotelId, ConversationKind.AreaChannel,
                               Conversation.KeyOfArea(command.Area))
                           ?? Conversation.OfArea(command.HotelId, command.Area, timeProvider.GetUtcNow());
        return await PostAsync(conversation, command.Sender, command.Body);
    }

    public async Task<ConversationMessage?> Handle(CommentOnTaskCommand command)
    {
        if (await HotelOfTaskAsync(command.Subject, command.SubjectId) is not { } hotelId) return null;
        var conversation = await conversationRepository.FindByKeyAsync(hotelId, ConversationKind.TaskThread,
                               Conversation.KeyOfTask(command.Subject, command.SubjectId))
                           ?? Conversation.AboutTask(hotelId, command.Subject, command.SubjectId, timeProvider.GetUtcNow());
        return await PostAsync(conversation, command.Sender, command.Body);
    }

    private async Task<ConversationMessage> PostAsync(Conversation conversation, ConversationActor sender, string body)
    {
        var message = conversation.Post(sender, body, timeProvider.GetUtcNow());
        if (conversation.Id == 0) await conversationRepository.AddAsync(conversation);
        await messageRepository.AddAsync(message);
        await unitOfWork.CompleteAsync();
        return message;
    }

    private async Task<int?> HotelOfTaskAsync(ThreadSubject subject, int subjectId) => subject switch
    {
        ThreadSubject.CleaningTask => (await cleaningTaskRepository.FindByIdAsync(subjectId))?.HotelId,
        ThreadSubject.Incident => (await incidentRepository.FindByIdAsync(subjectId))?.HotelId,
        _ => (await guestRequestRepository.FindByIdAsync(subjectId))?.HotelId
    };
}
