using BackendAwRoomTrack.API.Accommodations.Domain.Model.Events;
using BackendAwRoomTrack.API.Housekeeping.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Housekeeping.Application.Internal.EventHandlers;
using BackendAwRoomTrack.API.Housekeeping.Application.Internal.QueryServices;
using BackendAwRoomTrack.API.Housekeeping.Application.OutboundServices;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.Events;
using BackendAwRoomTrack.API.Housekeeping.Domain.Repositories;
using BackendAwRoomTrack.API.Housekeeping.Domain.Services;
using BackendAwRoomTrack.API.Housekeeping.Infrastructure.Notifications;
using BackendAwRoomTrack.API.Housekeeping.Infrastructure.Persistence.EFC.Repositories;
using BackendAwRoomTrack.API.Housekeeping.Interfaces.REST.Authorization;
using BackendAwRoomTrack.API.Shared.Application.Internal.EventHandlers;
using Microsoft.AspNetCore.Authorization;

namespace BackendAwRoomTrack.API.Housekeeping.Infrastructure.Interfaces.ASP.Configuration.Extensions;

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

        // Resource-based authorization (hotel scope)
        builder.Services.AddSingleton<IAuthorizationHandler, CleaningTaskScopeAuthorizationHandler>();
    }
}
