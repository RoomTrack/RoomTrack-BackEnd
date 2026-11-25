using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Queries;
using RoomTrack.Incidents.Domain.Repositories;
using RoomTrack.Incidents.Domain.Services;

namespace RoomTrack.Incidents.Application.Internal.QueryServices;

public class IncidentQueryService(IIncidentRepository incidentRepository) : IIncidentQueryService
{
    public Task<Incident?> Handle(GetIncidentByIdQuery query) => incidentRepository.FindByIdAsync(query.IncidentId);

    public Task<IReadOnlyList<Incident>> Handle(GetIncidentsQuery query) =>
        incidentRepository.ListAsync(query with { Limit = Math.Clamp(query.Limit, 1, 500) });

    public async Task<IReadOnlyList<IncidentAlert>> Handle(GetIncidentAlertsQuery query)
    {
        var incidents = await incidentRepository.ListAlertedAsync(query.HotelId, query.From, query.To, Math.Clamp(query.Limit, 1, 500));
        var alerts = new List<IncidentAlert>();
        foreach (var incident in incidents)
        {
            if (incident.EscalatedAt is { } escalatedAt && escalatedAt >= query.From && escalatedAt <= query.To)
                alerts.Add(new IncidentAlert("Escalation", escalatedAt, incident));
            if (incident.IsRecurring && incident.ReportedAt >= query.From && incident.ReportedAt <= query.To)
                alerts.Add(new IncidentAlert("Recurrence", incident.ReportedAt, incident));
        }
        return alerts.OrderByDescending(alert => alert.RaisedAt).ToList();
    }
}
