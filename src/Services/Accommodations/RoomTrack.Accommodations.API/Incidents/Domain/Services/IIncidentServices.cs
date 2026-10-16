using BackendAwRoomTrack.API.Incidents.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Commands;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Queries;

namespace BackendAwRoomTrack.API.Incidents.Domain.Services;

public interface IIncidentCommandService
{
    /// <summary>Reports an incident; a blocking one takes its room out of service in the same transaction.</summary>
    /// <exception cref="BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions.EntityNotFoundException">The room does not exist.</exception>
    Task<Incident> Handle(ReportIncidentCommand command);

    /// <returns>The incident, or null when it does not exist.</returns>
    Task<Incident?> Handle(AssignIncidentCommand command);

    /// <returns>The incident, or null when it does not exist.</returns>
    Task<Incident?> Handle(StartIncidentCommand command);

    /// <summary>Resolves an incident; the room of the last blocking incident is released to cleaning.</summary>
    /// <returns>The incident, or null when it does not exist.</returns>
    Task<Incident?> Handle(ResolveIncidentCommand command);

    /// <returns>The incident, or null when it does not exist.</returns>
    Task<Incident?> Handle(ChangeIncidentPriorityCommand command);

    /// <returns>How many incidents were escalated.</returns>
    Task<int> Handle(EscalateLateIncidentsCommand command);
}

public interface IIncidentQueryService
{
    Task<Incident?> Handle(GetIncidentByIdQuery query);

    Task<IReadOnlyList<Incident>> Handle(GetIncidentsQuery query);
}
