using RoomTrack.Marketing.Application.Internal.CommandServices;
using RoomTrack.Marketing.Application.Internal.Configuration;
using RoomTrack.Marketing.Application.Internal.QueryServices;
using RoomTrack.Marketing.Application.OutboundServices;
using RoomTrack.Marketing.Domain.Repositories;
using RoomTrack.Marketing.Domain.Services;
using RoomTrack.Marketing.Infrastructure.Notifications;
using RoomTrack.Marketing.Infrastructure.Persistence.EFC.Repositories;

namespace RoomTrack.Marketing.Infrastructure.Interfaces.ASP.Configuration.Extensions;

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
