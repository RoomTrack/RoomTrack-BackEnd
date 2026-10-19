using BackendAwRoomTrack.API.Audit.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using BackendAwRoomTrack.API.IAM.Application.OutboundServices;
using BackendAwRoomTrack.API.IAM.Infrastructure.Extensions;
using BackendAwRoomTrack.API.IAM.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Hosting;
using BackendAwRoomTrack.API.Shared.Infrastructure.Messaging;
using BackendAwRoomTrack.API.shared.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Identity.API.Infrastructure.Persistence;
using RoomTrack.Identity.API.Infrastructure.Sessions;

// Identity service: user accounts, sign-in, sessions and MFA (IAM) and the access audit log (Audit).
// It issues the access tokens every other service validates on its own.
var builder = WebApplication.CreateBuilder(args);

builder.AddRoomTrackServiceDefaults<IdentityDbContext, Program>("Identity", validateSessionsLocally: true);
builder.Services.AddScoped<IUserSessionValidator, LocalUserSessionValidator>();

builder.AddIamContextServices();
builder.AddAuditContextServices();

// RabbitMQ: e-mails (account verification, password recovery...) through the transactional outbox
builder.AddRoomTrackMessaging<IdentityDbContext>();

var app = builder.Build();

// Fail fast: never start with a broken schema
await app.MigrateDatabaseAsync<IdentityDbContext>();
await app.SeedDatabaseAsync();

app.UseRoomTrackServiceDefaults();

app.Run();

/// <summary>Entry point, exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
