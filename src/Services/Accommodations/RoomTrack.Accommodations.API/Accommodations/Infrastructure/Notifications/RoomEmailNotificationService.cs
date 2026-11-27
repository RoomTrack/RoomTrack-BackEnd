using System.Globalization;
using RoomTrack.Accommodations.Application.OutboundServices;
using RoomTrack.Accommodations.Domain.Model.ValueObjects;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Infrastructure.Configuration;
using RoomTrack.Shared.Infrastructure.Email.Templates;
using Microsoft.Extensions.Options;

namespace RoomTrack.Accommodations.Infrastructure.Notifications;

/// <summary>Room e-mails to the hotel staff (neutral Spanish) with a link to the room map of the web app.</summary>
public class RoomEmailNotificationService(
    IEmailSender emailSender,
    IOptions<ApplicationUrlsSettings> urls,
    TimeProvider timeProvider) : IRoomNotificationService
{
    public async Task SendStatusChangedAsync(IReadOnlyList<UserContact> recipients, RoomNotice room, RoomStatus from,
        RoomStatus to, string? changedBy)
    {
        foreach (var recipient in recipients)
            await emailSender.SendAsync(EmailLayout.Create()
                .Greeting(Greeting(recipient))
                .Paragraph($"La habitación {room.RoomNumber} de {room.HotelName} cambió de {Label(from)} a {Label(to)}" +
                           (changedBy is null ? "." : $" (cambio hecho por {changedBy})."))
                .Paragraph(Instruction(to))
                .Action("Ver el mapa de habitaciones", urls.Value.WebLink("rooms"))
                .To(recipient.Email, $"Habitación {room.RoomNumber}: {Label(to)}"));
    }

    public async Task SendMaintenanceOverdueAsync(IReadOnlyList<UserContact> recipients, RoomNotice room, DateTimeOffset maintenanceSince)
    {
        var hours = (int)Math.Floor((timeProvider.GetUtcNow() - maintenanceSince).TotalHours);
        foreach (var recipient in recipients)
            await emailSender.SendAsync(EmailLayout.Create()
                .Greeting(Greeting(recipient))
                .Paragraph($"La habitación {room.RoomNumber} de {room.HotelName} lleva {hours.ToString(CultureInfo.InvariantCulture)} horas en mantenimiento y no se puede reservar.")
                .Paragraph("Revisa con el equipo de mantenimiento si la reparación avanza o si la habitación ya puede volver a limpieza o a disponible.")
                .Action("Ver el mapa de habitaciones", urls.Value.WebLink("rooms"))
                .To(recipient.Email, $"Alerta: habitación {room.RoomNumber} en mantenimiento desde hace {hours.ToString(CultureInfo.InvariantCulture)} horas"));
    }

    public async Task SendNotReadyForArrivalAsync(IReadOnlyList<UserContact> recipients, RoomNotice room, RoomStatus status,
        string bookingCode, DateTimeOffset expectedAt)
    {
        var at = TimeZoneInfo.ConvertTime(expectedAt, LimaTime).ToString("HH:mm", CultureInfo.InvariantCulture);
        foreach (var recipient in recipients)
            await emailSender.SendAsync(EmailLayout.Create()
                .Greeting(Greeting(recipient))
                .Paragraph($"El huésped de la reserva {bookingCode} llega hoy a partir de las {at} y la habitación {room.RoomNumber} de {room.HotelName} todavía no está lista (estado: {Label(status)}).")
                .Paragraph(status switch
                {
                    RoomStatus.Cleaning => "Prioriza su limpieza o asigna al huésped otra habitación disponible del mismo tipo.",
                    RoomStatus.Occupied => "El huésped anterior aún no hace check-out: coordina la salida o asigna otra habitación al que llega.",
                    _ => "La habitación está en mantenimiento: asigna al huésped otra habitación disponible."
                })
                .Action("Ver el mapa de habitaciones", urls.Value.WebLink("rooms"))
                .To(recipient.Email, $"Alerta: la habitación {room.RoomNumber} no está lista para la llegada de las {at}"));
    }

    private static readonly TimeZoneInfo LimaTime = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

    private static string Greeting(UserContact contact) =>
        contact.FullName is null ? "Hola:" : $"Hola, {contact.FullName.Split(' ')[0]}:";

    private static string Label(RoomStatus status) => status switch
    {
        RoomStatus.Available => "Disponible",
        RoomStatus.Occupied => "Ocupada",
        RoomStatus.Cleaning => "Limpieza",
        _ => "Mantenimiento"
    };

    private static string Instruction(RoomStatus status) => status switch
    {
        RoomStatus.Cleaning => "Por favor, prepárala para el siguiente huésped y márcala como disponible al terminar.",
        RoomStatus.Occupied => "La habitación tiene un huésped: coordina el servicio de limpieza de la estadía.",
        RoomStatus.Maintenance => "Revisa la incidencia; mientras esté en mantenimiento no se puede reservar.",
        _ => "La habitación ya se puede asignar y reservar."
    };
}
