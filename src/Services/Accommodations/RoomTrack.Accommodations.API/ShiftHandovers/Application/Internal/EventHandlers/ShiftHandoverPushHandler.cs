using System.Globalization;
using RoomTrack.Contracts.Messaging;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.ShiftHandovers.Domain.Model.Events;
using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;

namespace RoomTrack.ShiftHandovers.Application.Internal.EventHandlers;

/// <summary>A new handover reaches the devices of the staff of its area (except its author).</summary>
public class ShiftHandoverPushHandler(IIamContextFacade iamContextFacade, IPushSender pushSender)
    : IDomainEventHandler<ShiftHandoverCreatedEvent>
{
    public async Task HandleAsync(ShiftHandoverCreatedEvent e, CancellationToken cancellationToken)
    {
        var role = e.Area switch
        {
            ShiftArea.Housekeeping => UserRoles.Housekeeping,
            ShiftArea.Maintenance => UserRoles.Maintenance,
            _ => UserRoles.Reception
        };
        var recipients = (await iamContextFacade.ListHotelStaffAsync(e.HotelId, [role]))
            .Select(contact => contact.UserId).Where(userId => userId != e.AuthorUserId).ToList();
        await pushSender.SendAsync(new PushMessage(recipients, PushCategories.Message, "Entrega de turno",
            $"El turno anterior dejó {e.OpenItems} pendiente(s). Revisa el resumen y confírmalo.",
            new Dictionary<string, string> { ["screen"] = "shift-handover", ["id"] = e.ShiftHandoverId.ToString(CultureInfo.InvariantCulture) }),
            cancellationToken);
    }
}
