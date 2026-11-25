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

/// <summary>The preventive maintenance plans of a hotel, optionally of one room (US-30).</summary>
public record GetPreventiveMaintenancePlansQuery(int HotelId, int? RoomId = null, bool OnlyActive = false);

/// <summary>A preventive maintenance plan.</summary>
public record GetPreventiveMaintenancePlanByIdQuery(int PlanId);

/// <summary>
///     The maintenance history of a room (US-10, US-30): every corrective and preventive task, newest first, and its
///     preventive plans.
/// </summary>
public record GetRoomMaintenanceHistoryQuery(int RoomId, int HotelId, int Limit = 200);

/// <summary>
///     US-23: the automatic alerts of a hotel (critical incidents not attended in time, recurring incidents of a room)
///     raised in the period, newest first, with how each one was resolved.
/// </summary>
public record GetIncidentAlertsQuery(int HotelId, DateTimeOffset From, DateTimeOffset To, int Limit = 200);
