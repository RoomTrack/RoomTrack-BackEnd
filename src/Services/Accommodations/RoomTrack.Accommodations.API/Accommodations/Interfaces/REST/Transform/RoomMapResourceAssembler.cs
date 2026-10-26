using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Domain.Model.Entities;
using RoomTrack.Accommodations.Domain.Model.ValueObjects;
using RoomTrack.Accommodations.Interfaces.REST.Resources;

namespace RoomTrack.Accommodations.Interfaces.REST.Transform;

public static class RoomMapResourceAssembler
{
    public static RoomMapResource ToResource(Hotel hotel, IReadOnlyList<Room> rooms, DateTimeOffset now, TimeSpan maintenanceThreshold) =>
        new(hotel.Id, hotel.Name, now,
            Enum.GetValues<RoomStatus>().ToDictionary(status => status.ToString(), status => rooms.Count(room => room.Status == status)),
            rooms.Select(room => new RoomMapItemResource(room.Id, room.Number, room.RoomType?.Name ?? string.Empty, room.Description,
                room.Price, room.Status.ToString(), room.StatusChangedAt,
                room.Status == RoomStatus.Maintenance && now - room.StatusChangedAt >= maintenanceThreshold,
                room.AllowedNextStatuses.Select(status => status.ToString()).ToList(), room.IsBlockedByIncident)).ToList());

    public static RoomStatusChangeResource ToResource(RoomStatusChange change) =>
        new(change.Id, change.RoomId, change.FromStatus.ToString(), change.ToStatus.ToString(), change.Origin.ToString(),
            change.ChangedAt, change.ChangedByUserId, change.ChangedByEmail);
}
