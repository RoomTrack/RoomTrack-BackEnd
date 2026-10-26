using RoomTrack.Notifications.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Audit.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.IAM.Application.OutboundServices;
using RoomTrack.IAM.Infrastructure.Extensions;
using RoomTrack.IAM.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Shared.Infrastructure.Hosting;
using RoomTrack.Shared.Infrastructure.Messaging;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Identity.API.Infrastructure.Persistence;
using RoomTrack.Identity.API.Infrastructure.Sessions;

// Identity service: user accounts, sign-in, sessions and MFA (IAM), the access audit log (Audit) and the
// devices and preferences of the push notifications (Notifications).
// It issues the access tokens every other service validates on its own.
var builder = WebApplication.CreateBuilder(args);

builder.AddRoomTrackServiceDefaults<IdentityDbContext, Program>("Identity", validateSessionsLocally: true);
builder.Services.AddScoped<IUserSessionValidator, LocalUserSessionValidator>();

builder.AddIamContextServices();
builder.AddAuditContextServices();
builder.AddNotificationsContextServices();

// RabbitMQ: e-mails (account verification, password recovery...) through the transactional outbox, and the push
// notifications of every service, delivered to the devices of their users
builder.AddRoomTrackMessaging<IdentityDbContext>(bus => bus.AddNotificationsConsumers());

var app = builder.Build();

// Fail fast: never start with a broken schema
await app.MigrateDatabaseAsync<IdentityDbContext>();
await app.SeedDatabaseAsync();

app.UseRoomTrackServiceDefaults();

app.Run();

/// <summary>Entry point, exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
