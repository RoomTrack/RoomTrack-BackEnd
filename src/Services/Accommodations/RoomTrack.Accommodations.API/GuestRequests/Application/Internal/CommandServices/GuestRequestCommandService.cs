using RoomTrack.Accommodations.Domain.Model.Queries;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.GuestRequests.Application.Internal.Configuration;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Commands;
using RoomTrack.GuestRequests.Domain.Model.Exceptions;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.GuestRequests.Domain.Repositories;
using RoomTrack.GuestRequests.Domain.Services;
using RoomTrack.IAM.Interfaces.ACL;
using RoomTrack.Shared.Domain.Repositories;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Microsoft.Extensions.Options;

namespace RoomTrack.GuestRequests.Application.Internal.CommandServices;

/// <summary>
///     Use cases of the guest requests (US-11, US-19). The stay of a guest comes from the Bookings service (its ACL
///     facade); the staff of each area from IAM. The notifications are sent by the event handlers, in the same
///     transaction.
/// </summary>
public class GuestRequestCommandService(
    IGuestRequestRepository requestRepository,
    IRoomReservationsFacade roomReservationsFacade,
    IRoomQueryService roomQueryService,
    IIamContextFacade iamContextFacade,
    IUnitOfWork unitOfWork,
    IOptions<GuestRequestSettings> settings,
    TimeProvider timeProvider) : IGuestRequestCommandService
{
    public async Task<GuestRequest> Handle(SendGuestRequestCommand command)
    {
        // The request is about the room of the guest's stay today.
        var stay = await roomReservationsFacade.FetchCurrentStayAsync(command.Guest.UserId) ?? throw new NoCurrentStayException();
        var now = timeProvider.GetUtcNow();
        var area = command.Area ?? ServiceArea.Reception;
        var request = GuestRequest.SendByGuest(stay.HotelId, stay.RoomId, stay.BookingId, command.Title, command.Description,
            area, command.Guest, now + settings.Value.AttentionTimeOf(area), now);
        return await AddAsync(request);
    }

    public async Task<GuestRequest> Handle(RegisterGuestRequestCommand command)
    {
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(command.RoomId))
                   ?? throw new EntityNotFoundException("Room", command.RoomId);
        var now = timeProvider.GetUtcNow();
        // The guest staying in the room today: when their booking has an account, they follow it in the app.
        var stay = await roomReservationsFacade.FetchCurrentStayOfRoomAsync(room.Id);
        var request = GuestRequest.RegisterByFrontDesk(room.HotelId, room.Id, stay?.BookingId, stay?.GuestUserId,
            command.Title, command.Description, command.Area, command.FrontDesk, now + settings.Value.AttentionTimeOf(command.Area), now);
        return await AddAsync(request);
    }

    public Task<GuestRequest?> Handle(RouteGuestRequestCommand command) =>
        ChangeAsync(command.GuestRequestId, request =>
        {
            var now = timeProvider.GetUtcNow();
            request.RouteTo(command.Area, command.Actor, now + settings.Value.AttentionTimeOf(command.Area), now);
            return Task.CompletedTask;
        });

    public Task<GuestRequest?> Handle(AssignGuestRequestCommand command) =>
        ChangeAsync(command.GuestRequestId, async request =>
        {
            // The assignee must work in the area of the request at its hotel (the front desk and the administrators too).
            var staff = await iamContextFacade.ListHotelStaffAsync(request.HotelId, [ServiceAreas.RoleOf(request.Area)]);
            if (command.AssigneeUserId != command.Actor.UserId && staff.All(contact => contact.UserId != command.AssigneeUserId))
                throw new InvalidFieldException("assigneeUserId", GuestRequestErrorCodes.AssigneeNotOfArea,
                    $"User {command.AssigneeUserId} is not an active staff member of the {request.Area} area of hotel {request.HotelId}.");
            request.AssignTo(command.AssigneeUserId, command.Actor, timeProvider.GetUtcNow());
        });

    public Task<GuestRequest?> Handle(StartGuestRequestCommand command) =>
        ChangeAsync(command.GuestRequestId, request =>
        {
            request.Start(command.Actor, timeProvider.GetUtcNow());
            return Task.CompletedTask;
        });

    public Task<GuestRequest?> Handle(ResolveGuestRequestCommand command) =>
        ChangeAsync(command.GuestRequestId, request =>
        {
            request.Resolve(command.Note, command.Actor, timeProvider.GetUtcNow());
            return Task.CompletedTask;
        });

    public Task<GuestRequest?> Handle(CancelGuestRequestCommand command) =>
        ChangeAsync(command.GuestRequestId, request =>
        {
            request.Cancel(command.Reason, command.Actor, timeProvider.GetUtcNow());
            return Task.CompletedTask;
        });

    private async Task<GuestRequest> AddAsync(GuestRequest request)
    {
        // The creation event needs the generated id: saved first, then published in the same transaction.
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await requestRepository.AddAsync(request);
            await unitOfWork.CompleteAsync();
        });
        return request;
    }

    private async Task<GuestRequest?> ChangeAsync(int requestId, Func<GuestRequest, Task> change)
    {
        var request = await requestRepository.FindByIdAsync(requestId);
        if (request is null) return null;
        await change(request);
        await unitOfWork.CompleteAsync();
        return request;
    }
}
