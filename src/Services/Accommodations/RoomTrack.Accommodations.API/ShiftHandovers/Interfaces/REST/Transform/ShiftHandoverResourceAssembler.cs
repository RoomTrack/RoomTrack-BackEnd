using RoomTrack.ShiftHandovers.Domain.Model.Aggregates;
using RoomTrack.ShiftHandovers.Interfaces.REST.Resources;

namespace RoomTrack.ShiftHandovers.Interfaces.REST.Transform;

public static class ShiftHandoverResourceAssembler
{
    public static ShiftHandoverResource ToResource(ShiftHandover handover, int requesterUserId) =>
        new(handover.Id, handover.HotelId, handover.Area.ToString(), handover.AuthorUserId, handover.AuthorEmail, handover.Notes,
            handover.CreatedAt,
            handover.Items.Select(item => new ShiftHandoverItemResource(item.Kind.ToString(), item.ReferenceId, item.RoomId,
                item.Summary, item.Status, item.Priority, item.AssigneeUserId)).ToList(),
            handover.Acknowledgements.OrderBy(ack => ack.AcknowledgedAt)
                .Select(ack => new ShiftHandoverAcknowledgementResource(ack.UserId, ack.Email, ack.AcknowledgedAt)).ToList(),
            handover.IsAcknowledgedBy(requesterUserId));
}
