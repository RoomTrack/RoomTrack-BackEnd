using RoomTrack.Incidents.Domain.Model.ValueObjects;

namespace RoomTrack.Incidents.Domain.Model.Queries;

/// <summary>An incident with its history.</summary>
public record GetIncidentByIdQuery(int IncidentId);

/// <summary>
///     The incidents of a hotel, most urgent first (priority, then oldest report). Every filter is optional.
/// </summary>
public record GetIncidentsQuery(
    int HotelId,
    IncidentStatus? Status = null,
    IncidentPriority? Priority = null,
    int? RoomId = null,
    int? AssigneeUserId = null,
    bool OnlyOpen = false,
    int Limit = 100);
