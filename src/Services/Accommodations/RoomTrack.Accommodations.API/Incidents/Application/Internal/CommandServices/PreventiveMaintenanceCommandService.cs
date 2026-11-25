using RoomTrack.Accommodations.Domain.Model.Queries;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.Incidents.Application.Internal.Configuration;
using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Commands;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Incidents.Domain.Repositories;
using RoomTrack.Incidents.Domain.Services;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace RoomTrack.Incidents.Application.Internal.CommandServices;

/// <summary>
///     Use cases of the preventive maintenance plans (US-30). On each due date the plan becomes an incident of kind
///     Preventive, so maintenance follows it in the same list as the corrective ones and the room history shows both.
/// </summary>
public class PreventiveMaintenanceCommandService(
    IPreventiveMaintenancePlanRepository planRepository,
    IIncidentRepository incidentRepository,
    IRoomQueryService roomQueryService,
    IUnitOfWork unitOfWork,
    IOptions<IncidentSettings> settings,
    TimeProvider timeProvider,
    ILogger<PreventiveMaintenanceCommandService> logger) : IPreventiveMaintenanceCommandService
{
    public async Task<PreventiveMaintenancePlan> Handle(SchedulePreventiveMaintenanceCommand command)
    {
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(command.RoomId))
                   ?? throw new EntityNotFoundException("Room", command.RoomId);
        var plan = PreventiveMaintenancePlan.Schedule(room.HotelId, room.Id, command.Title, command.Description,
            command.FrequencyDays, command.FirstDueOn, command.ReminderDaysBefore, command.ScheduledBy.UserId,
            settings.Value.Today(timeProvider), timeProvider.GetUtcNow());
        await planRepository.AddAsync(plan);
        await unitOfWork.CompleteAsync();
        return plan;
    }

    public async Task<PreventiveMaintenancePlan?> Handle(ReschedulePreventiveMaintenanceCommand command)
    {
        var plan = await planRepository.FindByIdAsync(command.PlanId);
        if (plan is null) return null;
        plan.Reschedule(command.FrequencyDays, command.NextDueOn, command.ReminderDaysBefore, settings.Value.Today(timeProvider));
        await unitOfWork.CompleteAsync();
        return plan;
    }

    public async Task<PreventiveMaintenancePlan?> Handle(DeactivatePreventiveMaintenanceCommand command)
    {
        var plan = await planRepository.FindByIdAsync(command.PlanId);
        if (plan is null) return null;
        plan.Deactivate(timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return plan;
    }

    public async Task<PreventiveMaintenanceRunResult> Handle(RunPreventiveMaintenanceCommand command)
    {
        var today = settings.Value.Today(timeProvider);
        var now = timeProvider.GetUtcNow();
        int generated = 0, reminded = 0;

        foreach (var plan in await planRepository.ListActionableAsync(today, PreventiveMaintenancePlan.MaxReminderDaysBefore))
        {
            if (plan.IsDue(today))
            {
                // Each plan in its own transaction: the incident gets its id and the plan moves to its next cycle together.
                await unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var incident = Incident.ReportPreventive(plan.HotelId, plan.RoomId, plan.Id, plan.Title, plan.Description,
                        new IncidentActor(plan.CreatedByUserId, null, IsAdministrator: false), now);
                    await incidentRepository.AddAsync(incident);
                    await unitOfWork.CompleteAsync();
                    plan.RecordGenerated(incident.Id, today, now);
                    await unitOfWork.CompleteAsync();
                });
                generated++;
                logger.LogInformation("Preventive maintenance plan {PlanId} generated its task; next due on {NextDueOn}.",
                    plan.Id, plan.NextDueOn);
            }
            if (plan.Remind(today, now)) reminded++;
        }

        await unitOfWork.CompleteAsync();
        return new PreventiveMaintenanceRunResult(generated, reminded);
    }
}
