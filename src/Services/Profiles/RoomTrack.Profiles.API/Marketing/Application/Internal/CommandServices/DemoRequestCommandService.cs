using RoomTrack.Marketing.Application.Internal.Configuration;
using RoomTrack.Marketing.Application.OutboundServices;
using RoomTrack.Marketing.Domain.Model.Aggregates;
using RoomTrack.Marketing.Domain.Model.Commands;
using RoomTrack.Marketing.Domain.Model.ValueObjects;
using RoomTrack.Marketing.Domain.Repositories;
using RoomTrack.Marketing.Domain.Services;
using RoomTrack.Shared.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace RoomTrack.Marketing.Application.Internal.CommandServices;

public class DemoRequestCommandService(
    IDemoRequestRepository demoRequestRepository,
    IDemoRequestNotificationService notifications,
    IUnitOfWork unitOfWork,
    IOptions<DemoRequestSettings> settings,
    TimeProvider timeProvider,
    ILogger<DemoRequestCommandService> logger) : IDemoRequestCommandService
{
    public async Task<DemoRequest> Handle(SubmitDemoRequestCommand command)
    {
        var contact = new ContactDetails(command.FirstName, command.LastName, command.Email, command.Phone);
        var request = DemoRequest.Submit(contact, command.HotelName, command.JobTitle, command.AccommodationType,
            command.RoomsRange, command.ReferralSource, command.Profile, command.Message, timeProvider.GetUtcNow());

        // The e-mails show the request number: saved first, then enlisted in the outbox in the same transaction.
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await demoRequestRepository.AddAsync(request);
            await unitOfWork.CompleteAsync();
            await notifications.SendConfirmationAsync(request);
            await notifications.NotifySalesTeamAsync(request);
            await unitOfWork.CompleteAsync();
        });
        return request;
    }

    public async Task<int> Handle(SendDemoFollowUpsCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var waiting = await demoRequestRepository.FindWaitingSinceAsync(now - settings.Value.FollowUpAfter);
        var due = waiting.Where(request => request.IsDueForFollowUp(now, settings.Value.FollowUpAfter)).ToList();
        if (due.Count == 0) return 0;

        // Marked and enlisted in the outbox in one commit: a second run (or a retry of the cron job) never sends the
        // reminder twice, and a marked request always has its reminder.
        var followedUp = due.Where(request => request.MarkFollowedUp(now)).ToList();
        foreach (var request in followedUp)
            await notifications.SendFollowUpAsync(request);
        await unitOfWork.CompleteAsync();

        logger.LogInformation("Demo follow-up sent to {Count} request(s).", followedUp.Count);
        return followedUp.Count;
    }
}
