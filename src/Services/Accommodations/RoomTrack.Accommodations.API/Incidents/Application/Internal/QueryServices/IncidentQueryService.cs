using BackendAwRoomTrack.API.Incidents.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Queries;
using BackendAwRoomTrack.API.Incidents.Domain.Repositories;
using BackendAwRoomTrack.API.Incidents.Domain.Services;

namespace BackendAwRoomTrack.API.Incidents.Application.Internal.QueryServices;

public class IncidentQueryService(IIncidentRepository incidentRepository) : IIncidentQueryService
{
    public Task<Incident?> Handle(GetIncidentByIdQuery query) => incidentRepository.FindByIdAsync(query.IncidentId);

    public Task<IReadOnlyList<Incident>> Handle(GetIncidentsQuery query) =>
        incidentRepository.ListAsync(query with { Limit = Math.Clamp(query.Limit, 1, 500) });
}
