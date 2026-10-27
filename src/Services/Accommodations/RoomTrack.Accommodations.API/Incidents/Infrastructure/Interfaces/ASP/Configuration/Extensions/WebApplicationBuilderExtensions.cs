using RoomTrack.Incidents.Application.Internal.CommandServices;
using RoomTrack.Incidents.Application.Internal.Configuration;
using RoomTrack.Incidents.Application.Internal.EventHandlers;
using RoomTrack.Incidents.Application.Internal.QueryServices;
using RoomTrack.Incidents.Application.OutboundServices;
using RoomTrack.Incidents.Domain.Model.Events;
using RoomTrack.Incidents.Domain.Repositories;
using RoomTrack.Incidents.Domain.Services;
using RoomTrack.Incidents.Infrastructure.Notifications;
using RoomTrack.Incidents.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Incidents.Interfaces.REST.Authorization;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using Microsoft.AspNetCore.Authorization;

namespace RoomTrack.Incidents.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>Incidents Bounded Context: reporting and follow-up of the incidents of the rooms.</summary>
    public static void AddIncidentsContextServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<IncidentSettings>()
            .Bind(builder.Configuration.GetSection(IncidentSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();
        builder.Services.AddScoped<IIncidentCommandService, IncidentCommandService>();
        builder.Services.AddScoped<IIncidentQueryService, IncidentQueryService>();

        // Staff e-mails about the incidents, enlisted in the outbox by the incident event handlers
        builder.Services.AddScoped<IIncidentNotificationService, IncidentEmailNotificationService>();
        builder.Services.AddScoped<IncidentNotificationHandler>();
        builder.Services.AddScoped<IDomainEventHandler<IncidentReportedEvent>>(sp => sp.GetRequiredService<IncidentNotificationHandler>());
        builder.Services.AddScoped<IDomainEventHandler<IncidentAssignedEvent>>(sp => sp.GetRequiredService<IncidentNotificationHandler>());
        builder.Services.AddScoped<IDomainEventHandler<IncidentStartedEvent>>(sp => sp.GetRequiredService<IncidentNotificationHandler>());
        builder.Services.AddScoped<IDomainEventHandler<IncidentResolvedEvent>>(sp => sp.GetRequiredService<IncidentNotificationHandler>());
        builder.Services.AddScoped<IDomainEventHandler<IncidentEscalatedEvent>>(sp => sp.GetRequiredService<IncidentNotificationHandler>());

        // Push notifications to the devices of the staff (US-22)
        builder.Services.AddScoped<IncidentPushHandler>();
        builder.Services.AddScoped<IDomainEventHandler<IncidentReportedEvent>>(sp => sp.GetRequiredService<IncidentPushHandler>());
        builder.Services.AddScoped<IDomainEventHandler<IncidentAssignedEvent>>(sp => sp.GetRequiredService<IncidentPushHandler>());
        builder.Services.AddScoped<IDomainEventHandler<IncidentPriorityChangedEvent>>(sp => sp.GetRequiredService<IncidentPushHandler>());
        builder.Services.AddScoped<IDomainEventHandler<IncidentEscalatedEvent>>(sp => sp.GetRequiredService<IncidentPushHandler>());

        // Resource-based authorization (hotel scope)
        builder.Services.AddSingleton<IAuthorizationHandler, IncidentScopeAuthorizationHandler>();
    }
}
