using RoomTrack.Accommodations.Interfaces.ACL;
using RoomTrack.Marketing.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Profiles.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Shared.Infrastructure.Hosting;
using RoomTrack.Shared.Infrastructure.Internal;
using RoomTrack.Shared.Infrastructure.Messaging;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.BuildingBlocks.Clients;
using RoomTrack.Profiles.API.Infrastructure.Persistence;

// Profiles service: guest and staff profiles (Profiles) and the demo requests of the landing (Marketing).
var builder = WebApplication.CreateBuilder(args);

builder.AddRoomTrackServiceDefaults<ProfilesDbContext, Program>("Profiles");

builder.AddProfilesContextServices();
builder.AddMarketingContextServices();

// Ports to the other services (HTTP)
builder.Services.AddInternalServiceClient<IAccommodationsContextFacade, HttpAccommodationsContextFacade>(builder.Configuration, "Accommodations");

// RabbitMQ: e-mails (demo requests) through the transactional outbox
builder.AddRoomTrackMessaging<ProfilesDbContext>();

var app = builder.Build();

await app.MigrateDatabaseAsync<ProfilesDbContext>();

app.UseRoomTrackServiceDefaults();

app.Run();

/// <summary>Entry point, exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
