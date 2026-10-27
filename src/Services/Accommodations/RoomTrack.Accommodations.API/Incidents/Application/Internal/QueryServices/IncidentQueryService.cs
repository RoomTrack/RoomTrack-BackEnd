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
}
