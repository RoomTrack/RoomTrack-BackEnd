using BackendAwRoomTrack.API.Incidents.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Entities;
using BackendAwRoomTrack.API.Incidents.Interfaces.REST.Resources;

namespace BackendAwRoomTrack.API.Incidents.Interfaces.REST.Transform;

public static class IncidentResourceAssembler
{
    /// <param name="incident">The incident.</param>
    /// <param name="withHistory">Include its history (loaded when a single incident is read).</param>
    public static IncidentResource ToResource(Incident incident, bool withHistory = true) =>
        new(incident.Id, incident.HotelId, incident.RoomId, incident.Title, incident.Description, incident.Priority.ToString(),
            incident.Status.ToString(), incident.BlocksRoom, incident.ReportedByUserId, incident.ReportedByEmail,
            incident.ReportedAt, incident.AssigneeUserId, incident.AssignedAt, incident.StartedAt, incident.ResolvedAt,
            incident.ResolvedByUserId, incident.ResolutionNotes, incident.EscalatedAt,
            withHistory ? incident.History.OrderBy(entry => entry.OccurredAt).ThenBy(entry => entry.Id).Select(ToResource).ToList() : null);

    private static IncidentHistoryEntryResource ToResource(IncidentHistoryEntry entry) =>
        new(entry.Action.ToString(), entry.Status.ToString(), entry.Detail, entry.ActorUserId, entry.ActorEmail, entry.OccurredAt);
}
