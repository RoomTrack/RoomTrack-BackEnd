using RoomTrack.Bookings.Application.ACL;
using RoomTrack.Bookings.Application.Internal.Configuration;
using RoomTrack.Bookings.Application.Internal.EventHandlers;
using RoomTrack.Bookings.Application.OutboundServices;
using RoomTrack.Bookings.Domain.Model.Events;
using RoomTrack.Bookings.Infrastructure.Notifications;
using RoomTrack.Bookings.Infrastructure.Storage;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Bookings.Application.Internal.CommandServices;
using RoomTrack.Bookings.Application.Internal.QueryServices;
using RoomTrack.Bookings.Domain.Repositories;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Bookings.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Bookings.Interfaces.ACL;

namespace RoomTrack.Bookings.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static void AddBookingsContextServices(this WebApplicationBuilder builder)
    {
        // Bookings Bounded Context Injection Configuration

        builder.Services.AddOptions<BookingPolicySettings>()
            .Bind(builder.Configuration.GetSection(BookingPolicySettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddScoped<HotelCalendar>();

        // Repositories
        builder.Services.AddScoped<IBookingRepository, BookingRepository>();
        builder.Services.AddScoped<IDigitalCheckInRepository, DigitalCheckInRepository>();

        // Digital check-in: documents stored encrypted in the database, staff e-mails through the outbox
        builder.Services.AddScoped<IDocumentStorage, DatabaseDocumentStorage>();
        builder.Services.AddScoped<ICheckInService, CheckInService>();
        builder.Services.AddScoped<ICheckInNotificationService, CheckInEmailNotificationService>();
        builder.Services.AddScoped<CheckInStaffNotificationHandler>();
        builder.Services.AddScoped<IDomainEventHandler<GuestCheckedInEvent>>(sp => sp.GetRequiredService<CheckInStaffNotificationHandler>());
        builder.Services.AddScoped<IDomainEventHandler<CheckInAssistanceRequestedEvent>>(sp => sp.GetRequiredService<CheckInStaffNotificationHandler>());

        // Guest e-mails, sent by the handlers of the booking events
        builder.Services.AddScoped<IBookingNotificationService, BookingEmailNotificationService>();
        builder.Services.AddScoped<BookingGuestNotificationHandler>();
        builder.Services.AddScoped<IDomainEventHandler<BookingCreatedEvent>>(sp => sp.GetRequiredService<BookingGuestNotificationHandler>());
        builder.Services.AddScoped<IDomainEventHandler<BookingConfirmedEvent>>(sp => sp.GetRequiredService<BookingGuestNotificationHandler>());
        builder.Services.AddScoped<IDomainEventHandler<BookingCancelledEvent>>(sp => sp.GetRequiredService<BookingGuestNotificationHandler>());
        builder.Services.AddScoped<IDomainEventHandler<BookingRescheduledEvent>>(sp => sp.GetRequiredService<BookingGuestNotificationHandler>());
        builder.Services.AddScoped<IDomainEventHandler<GuestCheckedOutEvent>>(sp => sp.GetRequiredService<BookingGuestNotificationHandler>());

        // Domain Services
        builder.Services.AddScoped<RoomAvailabilityService>();

        // Command Services
        builder.Services.AddScoped<IBookingCommandService, BookingCommandService>();

        // Query Services
        builder.Services.AddScoped<IBookingQueryService, BookingQueryService>();
        builder.Services.AddScoped<IRoomAvailabilityQueryService, RoomAvailabilityQueryService>();

        // ACL Facade
        builder.Services.AddScoped<IBookingsContextFacade, BookingsContextFacade>();
        builder.Services.AddScoped<IRoomReservationsFacade, RoomReservationsFacade>();
    }
}

