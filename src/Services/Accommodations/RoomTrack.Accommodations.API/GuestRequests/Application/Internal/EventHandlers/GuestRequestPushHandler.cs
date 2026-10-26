using System.Globalization;
using RoomTrack.Accommodations.Domain.Model.Queries;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.GuestRequests.Domain.Model.Events;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.GuestRequests.Domain.Repositories;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Contracts.Messaging;

namespace RoomTrack.GuestRequests.Application.Internal.EventHandlers;

/// <summary>
///     Push notifications of the guest requests: the staff of the area that attends a new or derived request (US-11,
///     scenario 2: a maintenance request reaches maintenance at once), the staff member assigned to it, and the guest
///     at every change of status and with the estimated attention time (US-19, US-11 scenario 4). The person who made
///     the change is never notified of it.
/// </summary>
public class GuestRequestPushHandler(
    IGuestRequestRepository requestRepository,
    IRoomQueryService roomQueryService,
    IIamContextFacade iamContextFacade,
    IPushSender pushSender,
    TimeProvider timeProvider) :
    IDomainEventHandler<GuestRequestCreatedEvent>,
    IDomainEventHandler<GuestRequestRoutedEvent>,
    IDomainEventHandler<GuestRequestAssignedEvent>,
    IDomainEventHandler<GuestRequestStatusChangedEvent>
{
    public async Task HandleAsync(GuestRequestCreatedEvent e, CancellationToken cancellationToken)
    {
        var title = await TitleAsync(e.GuestRequestId);
        await NotifyAreaAsync(e.HotelId, e.Area, e.CreatedByUserId, $"Nueva solicitud: habitación {await RoomAsync(e.RoomId)}",
            title, e.GuestRequestId, cancellationToken);

        // The guest gets the confirmation with the estimated attention time (also when the front desk registered it).
        if (e.GuestUserId is { } guest)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((e.EstimatedAttentionBy - timeProvider.GetUtcNow()).TotalMinutes));
            await pushSender.SendAsync(new PushMessage([guest], PushCategories.GuestRequest, "Recibimos tu solicitud",
                $"{title}: la atenderemos en unos {minutes} minutos.", Data(e.GuestRequestId)), cancellationToken);
        }
    }

    public async Task HandleAsync(GuestRequestRoutedEvent e, CancellationToken cancellationToken) =>
        await NotifyAreaAsync(e.HotelId, e.ToArea, e.RoutedByUserId, $"Solicitud derivada: habitación {await RoomAsync(e.RoomId)}",
            await TitleAsync(e.GuestRequestId), e.GuestRequestId, cancellationToken);

    public async Task HandleAsync(GuestRequestAssignedEvent e, CancellationToken cancellationToken)
    {
        if (e.AssigneeUserId == e.AssignedByUserId) return;
        await pushSender.SendAsync(new PushMessage([e.AssigneeUserId], PushCategories.GuestRequest,
            $"Solicitud asignada: habitación {await RoomAsync(e.RoomId)}", await TitleAsync(e.GuestRequestId),
            Data(e.GuestRequestId)), cancellationToken);
    }

    public async Task HandleAsync(GuestRequestStatusChangedEvent e, CancellationToken cancellationToken)
    {
        if (e.GuestUserId is not { } guest || guest == e.ChangedByUserId) return;
        var title = await TitleAsync(e.GuestRequestId);
        var (heading, body) = e.Status switch
        {
            GuestRequestStatus.InProgress => ("Estamos atendiendo tu solicitud", title),
            GuestRequestStatus.Resolved => ("Tu solicitud fue atendida", title),
            _ => ("Tu solicitud fue cancelada", title)
        };
        await pushSender.SendAsync(new PushMessage([guest], PushCategories.GuestRequest, heading, body, Data(e.GuestRequestId)),
            cancellationToken);
    }

    private async Task NotifyAreaAsync(int hotelId, ServiceArea area, int actorUserId, string heading, string body, int requestId,
        CancellationToken cancellationToken)
    {
        var staff = (await iamContextFacade.ListHotelStaffAsync(hotelId, [ServiceAreas.RoleOf(area)]))
            .Select(contact => contact.UserId).Where(userId => userId != actorUserId).ToList();
        await pushSender.SendAsync(new PushMessage(staff, PushCategories.GuestRequest, heading, body, Data(requestId)), cancellationToken);
    }

    private async Task<string> TitleAsync(int requestId) =>
        (await requestRepository.FindByIdAsync(requestId))?.Title ?? $"Solicitud {requestId}";

    private async Task<string> RoomAsync(int roomId) =>
        (await roomQueryService.Handle(new GetRoomByIdQuery(roomId)))?.Number ?? roomId.ToString(CultureInfo.InvariantCulture);

    private static Dictionary<string, string> Data(int requestId) =>
        new() { ["screen"] = "guest-request", ["id"] = requestId.ToString(CultureInfo.InvariantCulture) };
}
