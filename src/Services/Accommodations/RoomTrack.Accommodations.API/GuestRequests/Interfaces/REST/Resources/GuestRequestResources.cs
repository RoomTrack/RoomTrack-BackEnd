using System.ComponentModel.DataAnnotations;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Entities;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;

namespace RoomTrack.GuestRequests.Interfaces.REST.Resources;

/// <summary>
///     A request: a guest sends it for the room of their stay (<c>roomId</c> is not sent; <c>area</c> is optional, the
///     front desk by default); the front desk registers it for a room and chooses the <c>area</c>.
/// </summary>
public record CreateGuestRequestResource(
    [Required] string? Title,
    string? Description,
    ServiceArea? Area,
    int? RoomId);

public record RouteGuestRequestResource([Required] ServiceArea? Area);

public record AssignGuestRequestResource([Required] int? AssigneeUserId);

public record CloseGuestRequestResource(string? Note);

public record GuestRequestHistoryResource(GuestRequestHistoryAction Action, GuestRequestStatus Status, string? Detail,
    int? ActorUserId, string? ActorEmail, DateTimeOffset OccurredAt);

public record GuestRequestResource(
    int Id,
    int HotelId,
    int RoomId,
    int? BookingId,
    int? GuestUserId,
    string Title,
    string? Description,
    ServiceArea Area,
    GuestRequestStatus Status,
    int CreatedByUserId,
    bool CreatedByGuest,
    DateTimeOffset CreatedAt,
    DateTimeOffset EstimatedAttentionBy,
    int? AssigneeUserId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? CancelledAt,
    string? ClosingNote,
    IReadOnlyList<GuestRequestHistoryResource>? History);

public static class GuestRequestResourceAssembler
{
    public static GuestRequestResource ToResource(GuestRequest request, bool withHistory = true) => new(
        request.Id, request.HotelId, request.RoomId, request.BookingId, request.GuestUserId, request.Title,
        request.Description, request.Area, request.Status, request.CreatedByUserId, request.CreatedByGuest,
        request.CreatedAt, request.EstimatedAttentionBy, request.AssigneeUserId, request.StartedAt, request.ResolvedAt,
        request.CancelledAt, request.ClosingNote,
        withHistory ? request.History.Select(ToResource).ToList() : null);

    private static GuestRequestHistoryResource ToResource(GuestRequestHistoryEntry entry) =>
        new(entry.Action, entry.Status, entry.Detail, entry.ActorUserId, entry.ActorEmail, entry.OccurredAt);
}
