using BackendAwRoomTrack.API.Bookings.Domain.Model.Events;
using BackendAwRoomTrack.API.Payments.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Payments.Application.Internal.EventHandlers;
using BackendAwRoomTrack.API.Payments.Application.OutboundServices;
using BackendAwRoomTrack.API.Payments.Infrastructure.Gateways;
using BackendAwRoomTrack.API.Shared.Application.Internal.EventHandlers;
using BackendAwRoomTrack.API.Payments.Application.Internal.QueryServices;
using BackendAwRoomTrack.API.Payments.Domain.Repositories;
using BackendAwRoomTrack.API.Payments.Domain.Services;
using BackendAwRoomTrack.API.Payments.Infrastructure.Persistence.EFC.Repositories;

namespace BackendAwRoomTrack.API.Payments.Infrastructure.Interfaces.ASP.Configuration.Extensions;

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

