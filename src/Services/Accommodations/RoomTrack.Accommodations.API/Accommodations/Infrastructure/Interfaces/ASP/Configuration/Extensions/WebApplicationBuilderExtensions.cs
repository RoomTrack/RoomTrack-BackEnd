using BackendAwRoomTrack.API.Accommodations.Application.ACL;
using BackendAwRoomTrack.API.Accommodations.Application.Internal.Configuration;
using BackendAwRoomTrack.API.Accommodations.Application.Internal.EventHandlers;
using BackendAwRoomTrack.API.Accommodations.Application.OutboundServices;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Events;
using BackendAwRoomTrack.API.Accommodations.Infrastructure.Notifications;
using BackendAwRoomTrack.API.Shared.Application.Internal.EventHandlers;
using BackendAwRoomTrack.API.Accommodations.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Accommodations.Application.Internal.QueryServices;
using BackendAwRoomTrack.API.Accommodations.Domain.Repositories;
using BackendAwRoomTrack.API.Accommodations.Domain.Services;
using BackendAwRoomTrack.API.Accommodations.Infrastructure.Persistence.EFC.Repositories;
using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace BackendAwRoomTrack.API.Accommodations.Infrastructure.Interfaces.ASP.Configuration.Extensions;

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

