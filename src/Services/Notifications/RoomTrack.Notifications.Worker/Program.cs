using MassTransit;
using RoomTrack.Notifications.Worker.Email;
using RoomTrack.Notifications.Worker.Email.Configuration;

// Notifications worker: the only process that talks to the mail system. Every service publishes SendEmail messages
// through its transactional outbox; this worker consumes them from RabbitMQ and delivers them, with redelivery.
// It also answers /health over HTTP so it can run as a (free) web service on Render; the API gateway calls it to
// keep it awake while the app is in use.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddEmailTransports(builder.Configuration);

var retry = builder.Configuration.GetSection($"{EmailSettings.SectionName}:Retry").Get<EmailSettings.RetrySettings>()
            ?? new EmailSettings.RetrySettings();
var rabbit = builder.Configuration.GetSection("RabbitMq");

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<SendEmailConsumer>(consumer =>
    {
        // Retries with exponential backoff while the message stays unacknowledged in RabbitMQ: if the worker stops
        // meanwhile, RabbitMQ hands the message to the next instance. After the last attempt it goes to the
        // "_error" queue. (Longer, persistent delays would need RabbitMQ's delayed-message plugin.)
        consumer.UseMessageRetry(r => r.Exponential(
            Math.Max(retry.MaxAttempts - 1, 1),
            TimeSpan.FromSeconds(retry.InitialRetryDelaySeconds),
            TimeSpan.FromMinutes(retry.MaxRetryDelayMinutes),
            TimeSpan.FromSeconds(retry.InitialRetryDelaySeconds)));
        // A few e-mails at a time per instance: plenty for this volume and gentle with the provider's rate limit.
        consumer.ConcurrentMessageLimit = 4;
    });

    bus.UsingRabbitMq((context, configurator) =>
    {
        configurator.Host(rabbit["Host"] ?? "localhost", ushort.Parse(rabbit["Port"] ?? "5672"), rabbit["VirtualHost"] ?? "/", host =>
        {
            host.Username(rabbit["Username"] ?? "guest");
            host.Password(rabbit["Password"] ?? "guest");
        });
        configurator.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("notifications", false));
    });
});

var app = builder.Build();
app.MapHealthChecks("/health");
app.Run();
