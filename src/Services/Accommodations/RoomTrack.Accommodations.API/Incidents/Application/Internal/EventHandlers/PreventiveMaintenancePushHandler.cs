using System.Globalization;
using RoomTrack.Contracts.Messaging;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Incidents.Domain.Model.Events;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Shared.Application.OutboundServices;

namespace RoomTrack.Incidents.Application.Internal.EventHandlers;

/// <summary>
///     Push notifications of the maintenance alerts: the reminder of a coming preventive task to the maintenance staff
///     (US-30) and a recurring incident pattern of a room to the administrators (US-10).
/// </summary>
public class PreventiveMaintenancePushHandler(IIamContextFacade iamContextFacade, IPushSender pushSender) :
    IDomainEventHandler<PreventiveMaintenanceReminderDueEvent>,
    IDomainEventHandler<IncidentRecurrenceDetectedEvent>
{
    public async Task HandleAsync(PreventiveMaintenanceReminderDueEvent e, CancellationToken cancellationToken)
    {
        var recipients = await iamContextFacade.ListHotelStaffAsync(e.HotelId, [UserRoles.Maintenance]);
        await pushSender.SendAsync(new PushMessage(recipients.Select(contact => contact.UserId).ToList(),
            PushCategories.IncidentAssigned, "Mantenimiento preventivo próximo",
            $"{e.Title}: vence el {e.DueOn.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}.",
            new Dictionary<string, string> { ["screen"] = "preventive-maintenance", ["id"] = e.PlanId.ToString(CultureInfo.InvariantCulture) }),
            cancellationToken);
    }

    public async Task HandleAsync(IncidentRecurrenceDetectedEvent e, CancellationToken cancellationToken)
    {
        var recipients = await iamContextFacade.ListHotelStaffAsync(e.HotelId, [UserRoles.Admin]);
        await pushSender.SendAsync(new PushMessage(recipients.Select(contact => contact.UserId).ToList(),
            PushCategories.IncidentEscalated, "Incidencia recurrente",
            $"{e.Title}: {e.SimilarCount} incidencias similares en la misma habitación este mes.",
            new Dictionary<string, string> { ["screen"] = "incident", ["id"] = e.IncidentId.ToString(CultureInfo.InvariantCulture) },
            Urgent: true), cancellationToken);
    }
}
