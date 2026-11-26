using RoomTrack.Bookings.Application.OutboundServices;
using RoomTrack.Payments.Application.ACL;
using RoomTrack.Payments.Infrastructure.Notifications;
using RoomTrack.Payments.Domain.Model.Events;
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

        // Folio of the stay (US-15/16) and receipts (US-17)
        builder.Services.AddScoped<IGuestFolioRepository, GuestFolioRepository>();
        builder.Services.AddScoped<IReceiptRepository, ReceiptRepository>();
        builder.Services.AddScoped<IFolioCommandService, FolioCommandService>();
        builder.Services.AddScoped<IFolioQueryService, FolioQueryService>();
        builder.Services.AddScoped<ReceiptIssuer>();
        builder.Services.AddScoped<IReceiptNotificationService, ReceiptEmailNotificationService>();
        builder.Services.AddScoped<IssueReceiptOnPaymentHandler>();
        builder.Services.AddScoped<IDomainEventHandler<PaymentCompletedEvent>>(sp => sp.GetRequiredService<IssueReceiptOnPaymentHandler>());
        builder.Services.AddScoped<IDomainEventHandler<FolioPaymentRecordedEvent>>(sp => sp.GetRequiredService<IssueReceiptOnPaymentHandler>());
        builder.Services.AddScoped<IDomainEventHandler<GuestCheckedOutEvent>, CloseFolioOnCheckOutHandler>();

        // Port of the check-out (Bookings) to the folio: nothing unpaid at check-out
        builder.Services.AddScoped<IStayFolioGate, StayFolioGate>();
    }
}

