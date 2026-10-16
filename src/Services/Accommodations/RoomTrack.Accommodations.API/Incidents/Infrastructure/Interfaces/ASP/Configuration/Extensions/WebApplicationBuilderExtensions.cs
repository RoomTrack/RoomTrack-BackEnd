using BackendAwRoomTrack.API.Incidents.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Incidents.Application.Internal.Configuration;
using BackendAwRoomTrack.API.Incidents.Application.Internal.EventHandlers;
using BackendAwRoomTrack.API.Incidents.Application.Internal.QueryServices;
using BackendAwRoomTrack.API.Incidents.Application.OutboundServices;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Events;
using BackendAwRoomTrack.API.Incidents.Domain.Repositories;
using BackendAwRoomTrack.API.Incidents.Domain.Services;
using BackendAwRoomTrack.API.Incidents.Infrastructure.Notifications;
using BackendAwRoomTrack.API.Incidents.Infrastructure.Persistence.EFC.Repositories;
using BackendAwRoomTrack.API.Incidents.Interfaces.REST.Authorization;
using BackendAwRoomTrack.API.Shared.Application.Internal.EventHandlers;
using Microsoft.AspNetCore.Authorization;

namespace BackendAwRoomTrack.API.Incidents.Infrastructure.Interfaces.ASP.Configuration.Extensions;

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

        // Resource-based authorization (hotel scope)
        builder.Services.AddSingleton<IAuthorizationHandler, IncidentScopeAuthorizationHandler>();
    }
}
