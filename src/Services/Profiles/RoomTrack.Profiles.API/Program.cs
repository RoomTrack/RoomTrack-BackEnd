using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Marketing.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using BackendAwRoomTrack.API.Profiles.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Hosting;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;
using BackendAwRoomTrack.API.Shared.Infrastructure.Messaging;
using BackendAwRoomTrack.API.shared.Infrastructure.Persistence.EFC.Configuration.Extensions;
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
