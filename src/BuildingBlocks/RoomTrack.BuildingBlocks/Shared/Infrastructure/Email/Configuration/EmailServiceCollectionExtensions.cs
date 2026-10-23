using BackendAwRoomTrack.API.Shared.Application.OutboundServices;
using BackendAwRoomTrack.API.Shared.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BackendAwRoomTrack.API.Shared.Infrastructure.Email.Configuration;

public static class EmailServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the <see cref="IEmailSender"/> port (published to RabbitMQ through the transactional outbox and
    ///     delivered by the Notifications worker). Also binds <see cref="ApplicationUrlsSettings"/> used to build the
    ///     links of the e-mails.
    /// </summary>
    public static IServiceCollection AddEmailServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApplicationUrlsSettings>()
            .Bind(configuration.GetSection(ApplicationUrlsSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IEmailSender, BusEmailSender>();

        return services;
    }
}
