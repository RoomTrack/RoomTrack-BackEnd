using BackendAwRoomTrack.API.Marketing.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Marketing.Application.Internal.Configuration;
using BackendAwRoomTrack.API.Marketing.Application.Internal.QueryServices;
using BackendAwRoomTrack.API.Marketing.Application.OutboundServices;
using BackendAwRoomTrack.API.Marketing.Domain.Repositories;
using BackendAwRoomTrack.API.Marketing.Domain.Services;
using BackendAwRoomTrack.API.Marketing.Infrastructure.Notifications;
using BackendAwRoomTrack.API.Marketing.Infrastructure.Persistence.EFC.Repositories;

namespace BackendAwRoomTrack.API.Marketing.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>Marketing bounded context: demo requests of the landing.</summary>
    public static void AddMarketingContextServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<SalesSettings>()
            .Bind(builder.Configuration.GetSection(SalesSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddOptions<DemoRequestSettings>()
            .Bind(builder.Configuration.GetSection(DemoRequestSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddScoped<IDemoRequestRepository, DemoRequestRepository>();
        builder.Services.AddScoped<IDemoRequestCommandService, DemoRequestCommandService>();
        builder.Services.AddScoped<IDemoRequestQueryService, DemoRequestQueryService>();
        builder.Services.AddScoped<IDemoRequestNotificationService, DemoRequestEmailNotificationService>();
    }
}
