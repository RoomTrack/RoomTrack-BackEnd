using BackendAwRoomTrack.API.Analytics.Application.Internal.Consumers;
using BackendAwRoomTrack.API.Analytics.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Hosting;
using BackendAwRoomTrack.API.Shared.Infrastructure.Messaging;
using BackendAwRoomTrack.API.shared.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Analytics.API.Infrastructure.Persistence;

// Analytics service: the performance metrics of the hotels, computed on its own read model, which the integration
// events of the Accommodations and Bookings services keep up to date (eventual consistency).
var builder = WebApplication.CreateBuilder(args);

builder.AddRoomTrackServiceDefaults<AnalyticsDbContext, Program>("Analytics");

builder.AddAnalyticsContextServices();

// Optional analytics cache lab: Redis + ActiveMQ fallback (only when configured)
builder.AddAnalyticsCacheServices();

// RabbitMQ: consumers of the integration events that feed the read model
builder.AddRoomTrackMessaging<AnalyticsDbContext>(bus =>
{
    bus.AddConsumer<RoomRegisteredConsumer>();
    bus.AddConsumer<RoomRemovedConsumer>();
    bus.AddConsumer<BookingStateChangedConsumer>();
    bus.AddConsumer<PaymentStateChangedConsumer>();
});

var app = builder.Build();

await app.MigrateDatabaseAsync<AnalyticsDbContext>();

app.UseRoomTrackServiceDefaults();

app.Run();

/// <summary>Entry point, exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
