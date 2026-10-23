using BackendAwRoomTrack.API.Incidents.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Queries;

namespace BackendAwRoomTrack.API.Incidents.Domain.Repositories;

public interface IIncidentRepository
{
    Task AddAsync(Incident incident);

    /// <summary>The incident with its history, or null.</summary>
    Task<Incident?> FindByIdAsync(int id);

    /// <summary>The incidents matching the query, most urgent first (without their history).</summary>
    Task<IReadOnlyList<Incident>> ListAsync(GetIncidentsQuery query);

    /// <summary>Another open incident (not <paramref name="excludingIncidentId"/>) still blocks the room.</summary>
    Task<bool> AnyOpenBlockingAsync(int roomId, int excludingIncidentId);

    /// <summary>Open critical incidents not started nor escalated yet (with their history).</summary>
    Task<IReadOnlyList<Incident>> ListEscalationCandidatesAsync();
}
