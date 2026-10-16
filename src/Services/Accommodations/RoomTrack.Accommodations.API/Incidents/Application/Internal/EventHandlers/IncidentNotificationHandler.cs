using BackendAwRoomTrack.API.Accommodations.Domain.Model.Queries;
using BackendAwRoomTrack.API.Accommodations.Domain.Services;
using BackendAwRoomTrack.API.IAM.Domain.Model.Constants;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Incidents.Application.OutboundServices;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Events;
using BackendAwRoomTrack.API.Incidents.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Incidents.Domain.Repositories;
using BackendAwRoomTrack.API.Shared.Application.Internal.EventHandlers;

namespace BackendAwRoomTrack.API.Incidents.Application.Internal.EventHandlers;

/// <summary>
///     Notifies the hotel staff of what happens to an incident. Recipients come from IAM through its ACL facade; the
///     staff member who made the change is never notified of it.
/// </summary>
/// <remarks>
///     Reported → the maintenance staff of the hotel (and its administrators when the incident is critical);
///     assigned → the assignee; started or resolved → the reporter; escalated → the hotel administrators (or the
///     chain administrators when the hotel has none).
/// </remarks>
public class IncidentNotificationHandler(
    IIncidentRepository incidentRepository,
    IRoomQueryService roomQueryService,
    IIamContextFacade iamContextFacade,
    IIncidentNotificationService notifications) :
    IDomainEventHandler<IncidentReportedEvent>,
    IDomainEventHandler<IncidentAssignedEvent>,
    IDomainEventHandler<IncidentStartedEvent>,
    IDomainEventHandler<IncidentResolvedEvent>,
    IDomainEventHandler<IncidentEscalatedEvent>
{
    public async Task HandleAsync(IncidentReportedEvent e, CancellationToken cancellationToken)
    {
        List<string> roles = e.Priority == IncidentPriority.Critical
            ? [UserRoles.Maintenance, UserRoles.Admin]
            : [UserRoles.Maintenance];
        var recipients = (await iamContextFacade.ListHotelStaffAsync(e.HotelId, roles))
            .Where(contact => contact.UserId != e.ReportedByUserId).ToList();
        if (recipients.Count == 0) return;
        await notifications.SendReportedAsync(recipients, await NoticeAsync(e.IncidentId), await NameOfAsync(e.ReportedByUserId));
    }

    public async Task HandleAsync(IncidentAssignedEvent e, CancellationToken cancellationToken)
    {
        if (e.AssigneeUserId == e.AssignedByUserId) return;
        if (await iamContextFacade.FetchUserContactAsync(e.AssigneeUserId) is not { } assignee) return;
        await notifications.SendAssignedAsync(assignee, await NoticeAsync(e.IncidentId), await NameOfAsync(e.AssignedByUserId));
    }

    public Task HandleAsync(IncidentStartedEvent e, CancellationToken cancellationToken) =>
        NotifyReporterAsync(e.IncidentId, e.ReportedByUserId, e.StartedByUserId);

    public Task HandleAsync(IncidentResolvedEvent e, CancellationToken cancellationToken) =>
        NotifyReporterAsync(e.IncidentId, e.ReportedByUserId, e.ResolvedByUserId);

    public async Task HandleAsync(IncidentEscalatedEvent e, CancellationToken cancellationToken)
    {
        var recipients = await iamContextFacade.ListHotelStaffAsync(e.HotelId, [UserRoles.Admin]);
        // A hotel without an administrator: the chain administrators are alerted instead.
        if (recipients.Count == 0)
            recipients = await iamContextFacade.ListHotelStaffAsync(e.HotelId, [UserRoles.ChainAdmin]);
        if (recipients.Count == 0) return;
        await notifications.SendEscalatedAsync(recipients, await NoticeAsync(e.IncidentId), e.ReportedAt);
    }

    private async Task NotifyReporterAsync(int incidentId, int reporterUserId, int actorUserId)
    {
        if (reporterUserId == actorUserId) return;
        if (await iamContextFacade.FetchUserContactAsync(reporterUserId) is not { } reporter) return;
        await notifications.SendProgressAsync(reporter, await NoticeAsync(incidentId), await NameOfAsync(actorUserId));
    }

    private async Task<string?> NameOfAsync(int userId) =>
        await iamContextFacade.FetchUserContactAsync(userId) is { } contact ? contact.FullName ?? contact.Email : null;

    private async Task<IncidentNotice> NoticeAsync(int incidentId)
    {
        var incident = await incidentRepository.FindByIdAsync(incidentId)
                       ?? throw new InvalidOperationException($"Incident {incidentId} of a domain event was not found.");
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(incident.RoomId));
        return new IncidentNotice(incident.Id, incident.Title, incident.Priority, incident.Status, incident.BlocksRoom,
            room?.Number ?? incident.RoomId.ToString(), room?.Hotel?.Name ?? $"hotel {incident.HotelId}", incident.ResolutionNotes);
    }
}
