using System.Globalization;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Infrastructure.Configuration;
using RoomTrack.Shared.Infrastructure.Email.Templates;
using RoomTrack.StaffPerformance.Application.OutboundServices;
using RoomTrack.StaffPerformance.Domain.Model.Aggregates;
using RoomTrack.StaffPerformance.Domain.Model.ValueObjects;
using Microsoft.Extensions.Options;

namespace RoomTrack.StaffPerformance.Infrastructure.Notifications;

/// <summary>
///     The staff reports by e-mail (neutral Spanish): a summary of each report in the body and a link to the web app,
///     where the detail can be downloaded as CSV. The e-mails go through the outbox, in the same transaction.
/// </summary>
public class StaffReportEmailNotificationService(
    IEmailSender emailSender,
    IOptions<ApplicationUrlsSettings> urls) : IStaffReportNotificationService
{
    /// <summary>Lima: the dates of the period are shown in the time of the hotels.</summary>
    private static readonly TimeSpan HotelOffset = TimeSpan.FromHours(-5);

    private static readonly CultureInfo Numbers = CultureInfo.InvariantCulture;

    public async Task SendAsync(IReadOnlyList<string> recipients, StaffReports reports)
    {
        var period = $"{Day(reports.From)} al {Day(reports.To.AddTicks(-1))}";
        var subject = $"Reporte {(reports.Frequency == ReportFrequency.Weekly ? "semanal" : "mensual")} del personal — {reports.HotelName}";

        var layout = EmailLayout.Create()
            .Greeting("Hola:")
            .Paragraph($"Este es el reporte del personal de {reports.HotelName} del {period}.");

        if (reports.Productivity is { } productivity)
        {
            layout.Paragraph($"Productividad: {productivity.TotalCompleted} tareas terminadas en el periodo.");
            foreach (var area in productivity.Areas)
                layout.Paragraph($"• {Area(area.Area)}: {area.Completed} tareas, {area.Collaborators} colaboradores " +
                                 $"({area.AveragePerCollaborator.ToString("0.#", Numbers)} por colaborador)" +
                                 (area.AverageWorkMinutes is { } minutes ? $", {Minutes(minutes)} en promedio por tarea." : "."));
            foreach (var collaborator in productivity.Collaborators.Take(5))
                layout.Paragraph($"• {collaborator.FullName ?? $"Usuario {collaborator.UserId}"}: {collaborator.TotalCompleted} tareas " +
                                 $"({collaborator.CleaningTasksCompleted} limpiezas, {collaborator.IncidentsResolved} incidencias, " +
                                 $"{collaborator.GuestRequestsResolved} solicitudes).");
        }

        if (reports.ServiceTimes is { } times)
        {
            layout.Paragraph("Tiempos de atención promedio (espera hasta iniciar / trabajo / total):");
            layout.Paragraph($"• Limpieza de habitaciones: {Indicator(times.Cleaning)}");
            layout.Paragraph($"• Incidencias: {Indicator(times.Incidents)}");
            layout.Paragraph($"• Solicitudes de huéspedes: {Indicator(times.GuestRequests)}");
        }

        layout
            .Action("Ver los reportes", urls.Value.WebLink("reports"))
            .Footnote("Recibes este correo porque un administrador del hotel programó el envío de los reportes. " +
                      "En la aplicación puedes descargar el detalle en CSV o cambiar la programación.");

        foreach (var recipient in recipients)
            await emailSender.SendAsync(layout.To(recipient, subject));
    }

    private static string Day(DateTimeOffset instant) => instant.ToOffset(HotelOffset).ToString("dd/MM/yyyy", Numbers);

    private static string Minutes(double minutes) => $"{minutes.ToString("0.#", Numbers)} min";

    private static string Indicator(ServiceTimeIndicator indicator) =>
        indicator.Count == 0
            ? "sin actividad en el periodo."
            : $"{Optional(indicator.AverageResponseMinutes)} / {Optional(indicator.AverageWorkMinutes)} / " +
              $"{Optional(indicator.AverageTotalMinutes)} ({indicator.Count} medidas).";

    private static string Optional(double? minutes) => minutes is { } value ? Minutes(value) : "—";

    private static string Area(WorkArea area) => area switch
    {
        WorkArea.FrontDesk => "Recepción",
        WorkArea.Housekeeping => "Limpieza",
        _ => "Mantenimiento"
    };
}
