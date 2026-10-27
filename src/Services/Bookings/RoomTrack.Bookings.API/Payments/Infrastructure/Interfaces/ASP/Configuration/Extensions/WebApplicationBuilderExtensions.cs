using RoomTrack.Bookings.Domain.Model.Events;
using RoomTrack.Payments.Application.Internal.CommandServices;
using RoomTrack.Payments.Application.Internal.EventHandlers;
using RoomTrack.Payments.Application.OutboundServices;
using RoomTrack.Payments.Infrastructure.Gateways;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Payments.Application.Internal.QueryServices;
using RoomTrack.Payments.Domain.Repositories;
using RoomTrack.Payments.Domain.Services;
using RoomTrack.Payments.Infrastructure.Persistence.EFC.Repositories;

namespace RoomTrack.Payments.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static void AddPaymentsContextServices(this WebApplicationBuilder builder)
    {
        // Payments Bounded Context Injection Configuration

        // How guests pay is configured per hotel (Accommodations: HotelPaymentSettings), not here.

        // Payment gateway port: payments received by the hotel (an online gateway adapter would replace it here)
        builder.Services.AddScoped<IPaymentGateway, ManualPaymentGateway>();

        // Subscriptions (R2: a cancelled paid booking gets its payment refunded)
        builder.Services.AddScoped<IDomainEventHandler<BookingCancelledEvent>, RefundPaymentOnBookingCancelledHandler>();

        // Repositories
        builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

        // Command Services
        builder.Services.AddScoped<IPaymentCommandService, PaymentCommandService>();

        // Query Services
        builder.Services.AddScoped<IPaymentQueryService, PaymentQueryService>();
    }
}

