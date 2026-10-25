using System.Globalization;
using BackendAwRoomTrack.API.Housekeeping.Application.OutboundServices;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Application.OutboundServices;
using BackendAwRoomTrack.API.Shared.Infrastructure.Configuration;
using BackendAwRoomTrack.API.Shared.Infrastructure.Email.Templates;
using Microsoft.Extensions.Options;

namespace BackendAwRoomTrack.API.Housekeeping.Infrastructure.Notifications;

/// <summary>Cleaning task e-mails to the hotel staff (neutral Spanish) with a link to the task in the web app.</summary>
public class CleaningTaskEmailNotificationService(
    IEmailSender emailSender,
    IOptions<ApplicationUrlsSettings> urls) : ICleaningTaskNotificationService
{
    public Task SendAssignedAsync(UserContact assignee, CleaningTaskNotice task, string? assignedBy)
    {
        var layout = EmailLayout.Create()
            .Greeting(Greeting(assignee))
            .Paragraph($"Se te asignó la limpieza de la habitación {task.RoomNumber} de {task.HotelName}, con prioridad {Label(task.Priority)}" +
                       (assignedBy is null ? " (asignación automática)." : $" (asignada por {assignedBy})."));
        if (task.Notes is not null) layout.Paragraph($"Indicaciones: {task.Notes}");
        return emailSender.SendAsync(layout
            .Paragraph("Márcala como «en limpieza» al empezar y como «limpia» al terminar. Si encuentras un desperfecto, repórtalo desde la misma tarea.")
            .Action("Ver la tarea", Link(task))
            .To(assignee.Email, $"Limpieza asignada: habitación {task.RoomNumber}"));
    }

    public Task SendUnassignedAsync(UserContact previousAssignee, CleaningTaskNotice task, string? reassignedBy) =>
        emailSender.SendAsync(EmailLayout.Create()
            .Greeting(Greeting(previousAssignee))
            .Paragraph($"La limpieza de la habitación {task.RoomNumber} de {task.HotelName} se asignó a otra persona" +
                       (reassignedBy is null ? "." : $" ({reassignedBy}).") + " Ya no aparece en tu lista de tareas.")
            .To(previousAssignee.Email, $"Limpieza reasignada: habitación {task.RoomNumber}"));

    public async Task SendRoomReadyAsync(IReadOnlyList<UserContact> recipients, CleaningTaskNotice task, string? cleanedBy)
    {
        foreach (var recipient in recipients)
            await emailSender.SendAsync(EmailLayout.Create()
                .Greeting(Greeting(recipient))
                .Paragraph($"La habitación {task.RoomNumber} de {task.HotelName} ya está limpia" +
                           (cleanedBy is null ? "" : $" ({cleanedBy})") + " y disponible para asignarla a un huésped.")
                .Action("Ver la tarea", Link(task))
                .To(recipient.Email, $"Habitación {task.RoomNumber} lista"));
    }

    private string Link(CleaningTaskNotice task) =>
        urls.Value.WebLink("cleaning-tasks", ("id", task.CleaningTaskId.ToString(CultureInfo.InvariantCulture)));

    private static string Greeting(UserContact contact) =>
        contact.FullName is null ? "Hola:" : $"Hola, {contact.FullName.Split(' ')[0]}:";

    private static string Label(CleaningTaskPriority priority) => priority switch
    {
        CleaningTaskPriority.Low => "baja",
        CleaningTaskPriority.Normal => "normal",
        CleaningTaskPriority.High => "alta",
        _ => "urgente"
    };
}
