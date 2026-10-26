using RoomTrack.GuestRequests.Application.Internal.CommandServices;
using RoomTrack.GuestRequests.Application.Internal.Configuration;
using RoomTrack.GuestRequests.Application.Internal.EventHandlers;
using RoomTrack.GuestRequests.Application.Internal.QueryServices;
using RoomTrack.GuestRequests.Domain.Model.Events;
using RoomTrack.GuestRequests.Domain.Repositories;
using RoomTrack.GuestRequests.Domain.Services;
using RoomTrack.GuestRequests.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Shared.Application.Internal.EventHandlers;

namespace RoomTrack.GuestRequests.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>Guest requests context: requests of the guests during their stay and their coordination by area (US-11, US-19).</summary>
    public static void AddGuestRequestsContextServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<GuestRequestSettings>()
            .Bind(builder.Configuration.GetSection(GuestRequestSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddScoped<IGuestRequestRepository, GuestRequestRepository>();
        builder.Services.AddScoped<IGuestRequestCommandService, GuestRequestCommandService>();
        builder.Services.AddScoped<IGuestRequestQueryService, GuestRequestQueryService>();

        // Push notifications to the staff of the area and to the guest (US-22)
        builder.Services.AddScoped<GuestRequestPushHandler>();
        builder.Services.AddScoped<IDomainEventHandler<GuestRequestCreatedEvent>>(sp => sp.GetRequiredService<GuestRequestPushHandler>());
        builder.Services.AddScoped<IDomainEventHandler<GuestRequestRoutedEvent>>(sp => sp.GetRequiredService<GuestRequestPushHandler>());
        builder.Services.AddScoped<IDomainEventHandler<GuestRequestAssignedEvent>>(sp => sp.GetRequiredService<GuestRequestPushHandler>());
        builder.Services.AddScoped<IDomainEventHandler<GuestRequestStatusChangedEvent>>(sp => sp.GetRequiredService<GuestRequestPushHandler>());
    }
}
