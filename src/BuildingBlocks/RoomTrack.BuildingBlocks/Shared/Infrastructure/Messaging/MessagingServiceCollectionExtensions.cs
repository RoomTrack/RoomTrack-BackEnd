using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using MassTransit;

namespace BackendAwRoomTrack.API.Shared.Infrastructure.Messaging;

/// <summary>
///     RabbitMQ through MassTransit, with the Entity Framework transactional outbox of the service's database.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the bus (<c>RabbitMq:*</c> settings) with the transactional outbox of <typeparamref name="TContext"/>:
    ///     <list type="bullet">
    ///         <item><b>Bus outbox</b>: <c>IPublishEndpoint</c> resolved in a request scope writes the messages to
    ///         <c>outbox_messages</c> in the same <c>SaveChanges</c> as the business change; a background delivery
    ///         service relays them to RabbitMQ afterwards (at-least-once).</item>
    ///         <item><b>Consumer inbox</b>: the consumers registered by <paramref name="configureConsumers"/> get
    ///         message de-duplication (inbox) and their own outbox, so a redelivered event is processed once.</item>
    ///     </list>
    /// </summary>
    public static void AddRoomTrackMessaging<TContext>(this WebApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
        where TContext : AppDbContext
    {
        var settings = builder.Configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()
                       ?? new RabbitMqSettings();
        var queuePrefix = builder.Environment.ApplicationName;

        builder.Services.AddMassTransit(bus =>
        {
            bus.AddEntityFrameworkOutbox<TContext>(outbox =>
            {
                outbox.UseMySql();
                outbox.UseBusOutbox();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
                outbox.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(retry =>
                    retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(2)));
                endpoint.UseEntityFrameworkOutbox<TContext>(context);
            });

            configureConsumers?.Invoke(bus);

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(settings.Host, settings.Port, settings.VirtualHost, host =>
                {
                    host.Username(settings.Username);
                    host.Password(settings.Password);
                });
                // Queues of this service carry its name (e.g. roomtrack-analytics-api-booking-state-changed...).
                rabbit.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(queuePrefix, false));
            });
        });
    }
}

/// <summary>Connection to RabbitMQ (<c>RabbitMq</c> section).</summary>
public class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";

    public ushort Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";
}
