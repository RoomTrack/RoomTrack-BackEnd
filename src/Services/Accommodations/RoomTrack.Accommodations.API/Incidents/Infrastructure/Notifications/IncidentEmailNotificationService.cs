using System.Globalization;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Incidents.Application.OutboundServices;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Infrastructure.Configuration;
using RoomTrack.Shared.Infrastructure.Email.Templates;
using Microsoft.Extensions.Options;

namespace RoomTrack.Incidents.Infrastructure.Notifications;

/// <summary>Incident e-mails to the hotel staff (neutral Spanish) with a link to the incident in the web app.</summary>
public class IncidentEmailNotificationService(
    IEmailSender emailSender,
    IOptions<ApplicationUrlsSettings> urls,
    TimeProvider timeProvider) : IIncidentNotificationService
{
    public async Task SendReportedAsync(IReadOnlyList<UserContact> recipients, IncidentNotice incident, string? reportedBy)
    {
        foreach (var recipient in recipients)
            await emailSender.SendAsync(EmailLayout.Create()
                .Greeting(Greeting(recipient))
                .Paragraph($"Se reportó una incidencia de prioridad {Label(incident.Priority)} en la habitación {incident.RoomNumber} de {incident.HotelName}" +
                           (reportedBy is null ? "." : $" (reportada por {reportedBy})."))
                .Paragraph($"«{incident.Title}»")
                .Paragraph(incident.BlocksRoom
                    ? "La habitación queda fuera de servicio hasta que la incidencia se resuelva."
                    : "La habitación sigue en uso mientras se atiende la incidencia.")
                .Action("Ver la incidencia", Link(incident))
                .To(recipient.Email, $"Incidencia {Label(incident.Priority)}: habitación {incident.RoomNumber}"));
    }

    public Task SendAssignedAsync(UserContact assignee, IncidentNotice incident, string? assignedBy) =>
        emailSender.SendAsync(EmailLayout.Create()
            .Greeting(Greeting(assignee))
            .Paragraph($"Se te asignó la incidencia «{incident.Title}» de la habitación {incident.RoomNumber} de {incident.HotelName}, " +
                       $"con prioridad {Label(incident.Priority)}" + (assignedBy is null ? "." : $" (asignada por {assignedBy})."))
            .Paragraph("Márcala como «en proceso» cuando empieces a atenderla y como «resuelta» al terminar, con tus observaciones.")
            .Action("Ver la incidencia", Link(incident))
            .To(assignee.Email, $"Incidencia asignada: habitación {incident.RoomNumber}"));

    public Task SendProgressAsync(UserContact reporter, IncidentNotice incident, string? changedBy)
    {
        var resolved = incident.Status == IncidentStatus.Resolved;
        var layout = EmailLayout.Create()
            .Greeting(Greeting(reporter))
            .Paragraph($"La incidencia «{incident.Title}» que reportaste en la habitación {incident.RoomNumber} " +
                       (resolved ? "fue resuelta" : "está en proceso") + (changedBy is null ? "." : $" ({changedBy})."));
        if (resolved && incident.ResolutionNotes is not null)
            layout.Paragraph($"Observaciones: {incident.ResolutionNotes}");
        if (resolved && incident.BlocksRoom)
            layout.Paragraph("La habitación pasó a limpieza antes de volver a estar disponible.");
        return emailSender.SendAsync(layout
            .Action("Ver la incidencia", Link(incident))
            .To(reporter.Email, $"Incidencia {(resolved ? "resuelta" : "en proceso")}: habitación {incident.RoomNumber}"));
    }

    public async Task SendEscalatedAsync(IReadOnlyList<UserContact> recipients, IncidentNotice incident, DateTimeOffset reportedAt)
    {
        var minutes = (int)Math.Floor((timeProvider.GetUtcNow() - reportedAt).TotalMinutes);
        foreach (var recipient in recipients)
            await emailSender.SendAsync(EmailLayout.Create()
                .Greeting(Greeting(recipient))
                .Paragraph($"La incidencia crítica «{incident.Title}» de la habitación {incident.RoomNumber} de {incident.HotelName} " +
                           $"lleva {minutes.ToString(CultureInfo.InvariantCulture)} minutos sin ser atendida.")
                .Paragraph("Asígnala a una persona de mantenimiento o verifica por qué no se ha iniciado.")
                .Action("Ver la incidencia", Link(incident))
                .To(recipient.Email, $"Alerta: incidencia crítica sin atender en la habitación {incident.RoomNumber}"));
    }

    private string Link(IncidentNotice incident) =>
        urls.Value.WebLink("incidents", ("id", incident.IncidentId.ToString(CultureInfo.InvariantCulture)));

    private static string Greeting(UserContact contact) =>
        contact.FullName is null ? "Hola:" : $"Hola, {contact.FullName.Split(' ')[0]}:";

    private static string Label(IncidentPriority priority) => priority switch
    {
        IncidentPriority.Low => "baja",
        IncidentPriority.Medium => "media",
        IncidentPriority.High => "alta",
        _ => "crítica"
    };
}
