using RoomTrack.Accommodations.Interfaces.ACL;
using RoomTrack.Bookings.Domain.Model.Events;
using RoomTrack.Bookings.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Payments.Domain.Model.Events;
using RoomTrack.Payments.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using RoomTrack.Profiles.Interfaces.ACL;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Shared.Infrastructure.Hosting;
using RoomTrack.Shared.Infrastructure.Internal;
using RoomTrack.Shared.Infrastructure.Messaging;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration.Extensions;
using RoomTrack.Bookings.API.Application.IntegrationEvents;
using RoomTrack.Bookings.API.Infrastructure.Concurrency;
using RoomTrack.Bookings.API.Infrastructure.Persistence;
using RoomTrack.BuildingBlocks.Clients;

// Bookings service: bookings, availability and digital check-in (Bookings) and the payments of the bookings.
var builder = WebApplication.CreateBuilder(args);

builder.AddRoomTrackServiceDefaults<BookingsDbContext, Program>("Bookings");

builder.AddBookingsContextServices();
builder.AddPaymentsContextServices();

// Ports to the other services (HTTP). The Accommodations port adds the local room lock of the bookings.
builder.Services.AddInternalServiceClient<IAccommodationsContextFacade, BookingsAccommodationsFacade>(builder.Configuration, "Accommodations");
builder.Services.AddInternalServiceClient<IGuestProfilesContextFacade, HttpGuestProfilesContextFacade>(builder.Configuration, "Profiles");
builder.Services.AddInternalServiceClient<IIamContextFacade, HttpIamContextFacade>(builder.Configuration, "Identity");

// Integration events for the other services (Analytics), from the domain events of bookings and payments
builder.Services.AddScoped<BookingsIntegrationEventPublisher>();
builder.Services.AddScoped<IDomainEventHandler<BookingCreatedEvent>>(sp => sp.GetRequiredService<BookingsIntegrationEventPublisher>());
builder.Services.AddScoped<IDomainEventHandler<BookingConfirmedEvent>>(sp => sp.GetRequiredService<BookingsIntegrationEventPublisher>());
builder.Services.AddScoped<IDomainEventHandler<BookingCancelledEvent>>(sp => sp.GetRequiredService<BookingsIntegrationEventPublisher>());
builder.Services.AddScoped<IDomainEventHandler<BookingRescheduledEvent>>(sp => sp.GetRequiredService<BookingsIntegrationEventPublisher>());
builder.Services.AddScoped<IDomainEventHandler<GuestCheckedInEvent>>(sp => sp.GetRequiredService<BookingsIntegrationEventPublisher>());
builder.Services.AddScoped<IDomainEventHandler<GuestCheckedOutEvent>>(sp => sp.GetRequiredService<BookingsIntegrationEventPublisher>());
builder.Services.AddScoped<IDomainEventHandler<PaymentCompletedEvent>>(sp => sp.GetRequiredService<BookingsIntegrationEventPublisher>());
builder.Services.AddScoped<IDomainEventHandler<PaymentRefundedEvent>>(sp => sp.GetRequiredService<BookingsIntegrationEventPublisher>());

// RabbitMQ: integration events and e-mails (booking, check-in) through the transactional outbox
builder.AddRoomTrackMessaging<BookingsDbContext>();

var app = builder.Build();

await app.MigrateDatabaseAsync<BookingsDbContext>();

app.UseRoomTrackServiceDefaults();

app.Run();

/// <summary>Entry point, exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
