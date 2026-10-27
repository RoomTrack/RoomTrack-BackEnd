using RoomTrack.Messaging.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.GuestRequests.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Accommodations.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Controllers.Authorization;
using RoomTrack.Housekeeping.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Incidents.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Media.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Shared.Infrastructure.Hosting;
using RoomTrack.Shared.Infrastructure.Internal;
using RoomTrack.Shared.Infrastructure.Messaging;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Accommodations.API.Infrastructure.Persistence;
using RoomTrack.BuildingBlocks.Clients;

// Accommodations service: hotels, rooms and room types (Accommodations), incidents of the rooms (Incidents), cleaning
// tasks of the rooms (Housekeeping), requests of the guests (GuestRequests), conversations (Messaging), hotel images (Media) and the IoT emulator
// of the rooms' devices.
var builder = WebApplication.CreateBuilder(args);

builder.AddRoomTrackServiceDefaults<AccommodationsDbContext, Program>("Accommodations");

builder.AddAccommodationsContextServices();
builder.AddIncidentsContextServices();
builder.AddHousekeepingContextServices();
builder.AddGuestRequestsContextServices();
builder.AddMessagingContextServices();
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
