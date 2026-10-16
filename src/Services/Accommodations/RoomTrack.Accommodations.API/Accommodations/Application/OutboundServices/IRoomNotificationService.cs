using BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;

namespace BackendAwRoomTrack.API.Accommodations.Application.OutboundServices;

/// <summary>The room and hotel an e-mail is about.</summary>
public sealed record RoomNotice(int RoomId, string RoomNumber, int HotelId, string HotelName);

/// <summary>E-mails to the hotel staff about the rooms. Enlisted in the outbox inside the transaction of the change.</summary>
public interface IRoomNotificationService
{
    /// <summary>The status of a room changed; the staff in charge of the new status is notified.</summary>
    Task SendStatusChangedAsync(IReadOnlyList<UserContact> recipients, RoomNotice room, RoomStatus from, RoomStatus to, string? changedBy);

    /// <summary>The room has been under maintenance for too long.</summary>
    Task SendMaintenanceOverdueAsync(IReadOnlyList<UserContact> recipients, RoomNotice room, DateTimeOffset maintenanceSince);
}
