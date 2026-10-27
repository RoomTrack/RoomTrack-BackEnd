using System.Globalization;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Incidents.Domain.Model.Events;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Incidents.Domain.Repositories;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Contracts.Messaging;

namespace RoomTrack.Incidents.Application.Internal.EventHandlers;

/// <summary>
///     Push notifications of the incidents (US-08, US-09, US-22), next to their e-mails: a new incident to the
///     maintenance staff, an incident assigned to someone, a change of urgency to its assignee and a late critical
///     incident to the administrators. The staff member who made the change is never notified of it.
/// </summary>
public class IncidentPushHandler(
    IIncidentRepository incidentRepository,
    IIamContextFacade iamContextFacade,
    IPushSender pushSender) :
    IDomainEventHandler<IncidentReportedEvent>,
    IDomainEventHandler<IncidentAssignedEvent>,
    IDomainEventHandler<IncidentPriorityChangedEvent>,
    IDomainEventHandler<IncidentEscalatedEvent>
{
    public async Task HandleAsync(IncidentReportedEvent e, CancellationToken cancellationToken)
    {
        var recipients = (await iamContextFacade.ListHotelStaffAsync(e.HotelId, [UserRoles.Maintenance]))
            .Select(contact => contact.UserId).Where(userId => userId != e.ReportedByUserId).ToList();
        var critical = e.Priority == IncidentPriority.Critical;
        await pushSender.SendAsync(new PushMessage(recipients, PushCategories.IncidentAssigned,
            critical ? "¡Incidencia crítica!" : "Nueva incidencia", await TitleOfAsync(e.IncidentId), Data(e.IncidentId), critical),
            cancellationToken);
    }

    public async Task HandleAsync(IncidentAssignedEvent e, CancellationToken cancellationToken)
    {
        if (e.AssigneeUserId == e.AssignedByUserId) return;
        await pushSender.SendAsync(new PushMessage([e.AssigneeUserId], PushCategories.IncidentAssigned,
            "Incidencia asignada", await TitleOfAsync(e.IncidentId), Data(e.IncidentId)), cancellationToken);
    }

    public async Task HandleAsync(IncidentPriorityChangedEvent e, CancellationToken cancellationToken)
    {
        if (e.AssigneeUserId is not { } assignee || assignee == e.ChangedByUserId) return;
        var critical = e.ToPriority == IncidentPriority.Critical;
        await pushSender.SendAsync(new PushMessage([assignee], PushCategories.PriorityChanged,
            critical ? "¡Incidencia crítica!" : "Cambió la prioridad de una incidencia",
            $"{await TitleOfAsync(e.IncidentId)}: prioridad {e.FromPriority} → {e.ToPriority}.", Data(e.IncidentId), critical),
            cancellationToken);
    }

    public async Task HandleAsync(IncidentEscalatedEvent e, CancellationToken cancellationToken)
    {
        var recipients = await iamContextFacade.ListHotelStaffAsync(e.HotelId, [UserRoles.Admin]);
        if (recipients.Count == 0) recipients = await iamContextFacade.ListHotelStaffAsync(e.HotelId, [UserRoles.ChainAdmin]);
        await pushSender.SendAsync(new PushMessage(recipients.Select(contact => contact.UserId).ToList(),
            PushCategories.IncidentEscalated, "Incidencia crítica sin atender", await TitleOfAsync(e.IncidentId),
            Data(e.IncidentId), Urgent: true), cancellationToken);
    }

    private async Task<string> TitleOfAsync(int incidentId) =>
        (await incidentRepository.FindByIdAsync(incidentId))?.Title ?? $"Incidencia {incidentId}";

    private static Dictionary<string, string> Data(int incidentId) =>
        new() { ["screen"] = "incident", ["id"] = incidentId.ToString(CultureInfo.InvariantCulture) };
}
