using RoomTrack.Accommodations.Application.ACL;
using RoomTrack.Accommodations.Application.Internal.Configuration;
using RoomTrack.Accommodations.Application.Internal.EventHandlers;
using RoomTrack.Accommodations.Application.OutboundServices;
using RoomTrack.Accommodations.Domain.Model.Events;
using RoomTrack.Accommodations.Infrastructure.Notifications;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Accommodations.Application.Internal.CommandServices;
using RoomTrack.Accommodations.Application.Internal.QueryServices;
using RoomTrack.Accommodations.Domain.Repositories;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.Accommodations.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Accommodations.Interfaces.ACL;
using RoomTrack.Accommodations.Interfaces.REST.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace RoomTrack.Accommodations.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static void AddAccommodationsContextServices(this WebApplicationBuilder builder)
    {
        // Accommodations Bounded Context Injection Configuration

        builder.Services.AddOptions<RoomOperationsSettings>()
            .Bind(builder.Configuration.GetSection(RoomOperationsSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Repositories
        builder.Services.AddScoped<IRoomRepository, RoomRepository>();
        builder.Services.AddScoped<IRoomStatusChangeRepository, RoomStatusChangeRepository>();

        // Staff e-mails about the rooms, enlisted in the outbox by the room event handlers
        builder.Services.AddScoped<IRoomNotificationService, RoomEmailNotificationService>();
        builder.Services.AddScoped<RoomStaffNotificationHandler>();
        builder.Services.AddScoped<IDomainEventHandler<RoomStatusChangedEvent>>(sp => sp.GetRequiredService<RoomStaffNotificationHandler>());
        builder.Services.AddScoped<IDomainEventHandler<RoomMaintenanceOverdueEvent>>(sp => sp.GetRequiredService<RoomStaffNotificationHandler>());
        builder.Services.AddScoped<IRoomTypeRepository, RoomTypeRepository>();
        builder.Services.AddScoped<IHotelRepository, HotelRepository>();

        // Command Services
        builder.Services.AddScoped<IRoomCommandService, RoomCommandService>();
        builder.Services.AddScoped<IRoomTypeCommandService, RoomTypeCommandService>();
        builder.Services.AddScoped<IHotelCommandService, HotelCommandService>();

        // Query Services
        builder.Services.AddScoped<IRoomQueryService, RoomQueryService>();
        builder.Services.AddScoped<IRoomTypeQueryService, RoomTypeQueryService>();
        builder.Services.AddScoped<IHotelQueryService, HotelQueryService>();

        // ACL Facade
        builder.Services.AddScoped<IAccommodationsContextFacade, AccommodationsContextFacade>();

        // Resource-based authorization (hotel scope)
        builder.Services.AddSingleton<IAuthorizationHandler, HotelManagementAuthorizationHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, RoomOperationsAuthorizationHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, HotelStaffAuthorizationHandler>();
    }
}

