using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Infrastructure.Configuration;
using RoomTrack.Shared.Infrastructure.Push;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RoomTrack.Shared.Infrastructure.Email.Configuration;

public static class EmailServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the <see cref="IEmailSender"/> and <see cref="IPushSender"/> ports (published to RabbitMQ through
    ///     the transactional outbox and delivered by the Notifications worker). Also binds <see cref="ApplicationUrlsSettings"/> used to build the
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
        services.AddScoped<IPushSender, BusPushSender>();

        return services;
    }
}
