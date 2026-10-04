using BackendAwRoomTrack.API.Accommodations.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using BackendAwRoomTrack.API.Bookings.Interfaces.ACL;
using BackendAwRoomTrack.API.Controllers.Authorization;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Media.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Hosting;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;
using BackendAwRoomTrack.API.Shared.Infrastructure.Messaging;
using BackendAwRoomTrack.API.shared.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Accommodations.API.Infrastructure.Persistence;
using RoomTrack.BuildingBlocks.Clients;

// Accommodations service: hotels, rooms and room types (Accommodations), hotel images (Media) and the IoT emulator
// of the rooms' devices.
var builder = WebApplication.CreateBuilder(args);

builder.AddRoomTrackServiceDefaults<AccommodationsDbContext, Program>("Accommodations");

builder.AddAccommodationsContextServices();
builder.AddMediaContextServices();
builder.AddIoTEmulatorServices();

// Ports to the other services (HTTP)
builder.Services.AddInternalServiceClient<IIamContextFacade, HttpIamContextFacade>(builder.Configuration, "Identity");
builder.Services.AddInternalServiceClient<IRoomReservationsFacade, HttpRoomReservationsFacade>(builder.Configuration, "Bookings");

// RabbitMQ: room integration events (Analytics) and e-mails (room alerts) through the transactional outbox
builder.AddRoomTrackMessaging<AccommodationsDbContext>();

var app = builder.Build();

await app.MigrateDatabaseAsync<AccommodationsDbContext>();

app.UseRoomTrackServiceDefaults();

app.Run();

/// <summary>Entry point, exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
