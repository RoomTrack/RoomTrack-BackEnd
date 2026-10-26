using RoomTrack.Accommodations.Domain.Model.Events;
using RoomTrack.Housekeeping.Application.Internal.CommandServices;
using RoomTrack.Housekeeping.Application.Internal.EventHandlers;
using RoomTrack.Housekeeping.Application.Internal.QueryServices;
using RoomTrack.Housekeeping.Application.OutboundServices;
using RoomTrack.Housekeeping.Domain.Model.Events;
using RoomTrack.Housekeeping.Domain.Repositories;
using RoomTrack.Housekeeping.Domain.Services;
using RoomTrack.Housekeeping.Infrastructure.Notifications;
using RoomTrack.Housekeeping.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Housekeeping.Interfaces.REST.Authorization;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using Microsoft.AspNetCore.Authorization;

namespace RoomTrack.Housekeeping.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>Housekeeping Bounded Context: assignment and follow-up of the cleaning tasks of the rooms.</summary>
    public static void AddHousekeepingContextServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ICleaningTaskRepository, CleaningTaskRepository>();
        builder.Services.AddScoped<IHotelHousekeepingSettingsRepository, HotelHousekeepingSettingsRepository>();
        builder.Services.AddScoped<ICleaningTaskAssignmentStrategy, LeastWorkloadAssignmentStrategy>();
        builder.Services.AddScoped<ICleaningTaskCommandService, CleaningTaskCommandService>();
        builder.Services.AddScoped<ICleaningTaskQueryService, CleaningTaskQueryService>();

        // Tasks of the rooms that go to Cleaning outside Housekeeping
        builder.Services.AddScoped<IDomainEventHandler<RoomStatusChangedEvent>, RoomCleaningTaskHandler>();

        // Staff e-mails about the cleaning tasks, enlisted in the outbox by the task event handlers
        builder.Services.AddScoped<ICleaningTaskNotificationService, CleaningTaskEmailNotificationService>();
        builder.Services.AddScoped<CleaningTaskNotificationHandler>();
        builder.Services.AddScoped<IDomainEventHandler<CleaningTaskAssignedEvent>>(sp => sp.GetRequiredService<CleaningTaskNotificationHandler>());
        builder.Services.AddScoped<IDomainEventHandler<CleaningTaskCompletedEvent>>(sp => sp.GetRequiredService<CleaningTaskNotificationHandler>());

        // Push notifications to the devices of the staff (US-22)
        builder.Services.AddScoped<CleaningTaskPushHandler>();
        builder.Services.AddScoped<IDomainEventHandler<CleaningTaskAssignedEvent>>(sp => sp.GetRequiredService<CleaningTaskPushHandler>());
        builder.Services.AddScoped<IDomainEventHandler<CleaningTaskPriorityChangedEvent>>(sp => sp.GetRequiredService<CleaningTaskPushHandler>());

        // Resource-based authorization (hotel scope)
        builder.Services.AddSingleton<IAuthorizationHandler, CleaningTaskScopeAuthorizationHandler>();
    }
}
