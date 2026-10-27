using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Domain.Model.Queries;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Incidents.Application.Internal.Configuration;
using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Commands;
using RoomTrack.Incidents.Domain.Model.Exceptions;
using RoomTrack.Incidents.Domain.Repositories;
using RoomTrack.Incidents.Domain.Services;
using RoomTrack.Shared.Domain.Repositories;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Microsoft.Extensions.Options;

namespace RoomTrack.Incidents.Application.Internal.CommandServices;

/// <summary>
///     Use cases of the incidents. The room of a blocking incident is held and released through the Rooms context
///     (<see cref="IRoomCommandService"/>) inside the same transaction as the incident change, so an incident and
///     the status of its room never disagree.
/// </summary>
public class IncidentCommandService(
    IIncidentRepository incidentRepository,
    IRoomQueryService roomQueryService,
    IRoomCommandService roomCommandService,
    IIamContextFacade iamContextFacade,
    IUnitOfWork unitOfWork,
    IOptions<IncidentSettings> settings,
    TimeProvider timeProvider) : IIncidentCommandService
{
    public async Task<Incident> Handle(ReportIncidentCommand command)
    {
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(command.RoomId))
                   ?? throw new EntityNotFoundException("Room", command.RoomId);
        var incident = Incident.Report(room.HotelId, room.Id, command.Title, command.Description, command.Priority,
            command.BlocksRoom, command.Reporter, timeProvider.GetUtcNow());

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await incidentRepository.AddAsync(incident);
            await unitOfWork.CompleteAsync();
            if (incident.BlocksRoom)
                await roomCommandService.Handle(new HoldRoomForIncidentCommand(room.Id, command.Reporter.UserId, command.Reporter.Email));
        });
        return incident;
    }

    public async Task<Incident?> Handle(AssignIncidentCommand command)
    {
        var incident = await incidentRepository.FindByIdAsync(command.IncidentId);
        if (incident is null) return null;

        // A maintenance staff member taking it for themselves is already known to be one (endpoint policy and hotel
        // scope); anyone else must be maintenance staff of the incident's hotel.
        var takesItThemselves = command.AssigneeUserId == command.Actor.UserId && !command.Actor.IsAdministrator;
        if (!takesItThemselves)
        {
            var maintenance = await iamContextFacade.ListHotelStaffAsync(incident.HotelId, [UserRoles.Maintenance]);
            if (maintenance.All(contact => contact.UserId != command.AssigneeUserId))
                throw new InvalidFieldException("assigneeUserId", IncidentErrorCodes.AssigneeNotMaintenance,
                    $"User {command.AssigneeUserId} is not an active maintenance staff member of hotel {incident.HotelId}.");
        }

        incident.AssignTo(command.AssigneeUserId, command.Actor, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return incident;
    }

    public async Task<Incident?> Handle(StartIncidentCommand command)
    {
        var incident = await incidentRepository.FindByIdAsync(command.IncidentId);
        if (incident is null) return null;

        var now = timeProvider.GetUtcNow();
        if (incident.AssigneeUserId is null && !command.Actor.IsAdministrator)
            incident.AssignTo(command.Actor.UserId, command.Actor, now);
        incident.Start(command.Actor, now);
        await unitOfWork.CompleteAsync();
        return incident;
    }

    public async Task<Incident?> Handle(ResolveIncidentCommand command)
    {
        var incident = await incidentRepository.FindByIdAsync(command.IncidentId);
        if (incident is null) return null;

        incident.Resolve(command.ResolutionNotes, command.Actor, timeProvider.GetUtcNow());
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await unitOfWork.CompleteAsync();
            // The room stays out of service while another blocking incident of it is open.
            if (incident.BlocksRoom && !await incidentRepository.AnyOpenBlockingAsync(incident.RoomId, incident.Id))
                await roomCommandService.Handle(new ReleaseRoomFromIncidentCommand(incident.RoomId, command.Actor.UserId, command.Actor.Email));
        });
        return incident;
    }

    public async Task<Incident?> Handle(ChangeIncidentPriorityCommand command)
    {
        var incident = await incidentRepository.FindByIdAsync(command.IncidentId);
        if (incident is null) return null;

        incident.ChangePriority(command.Priority, command.Actor, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return incident;
    }

    public async Task<int> Handle(EscalateLateIncidentsCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var escalated = 0;
        foreach (var incident in await incidentRepository.ListEscalationCandidatesAsync())
            if (incident.Escalate(now, settings.Value.CriticalEscalationAfter)) escalated++;
        await unitOfWork.CompleteAsync();
        return escalated;
    }
}
