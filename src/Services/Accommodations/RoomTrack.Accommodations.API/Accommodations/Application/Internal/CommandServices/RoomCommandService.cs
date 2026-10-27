using RoomTrack.Accommodations.Application.Internal.Configuration;
using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Domain.Model.Exceptions;
using RoomTrack.Accommodations.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Microsoft.Extensions.Options;
using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Domain.Repositories;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.Shared.Domain.Repositories;
using MassTransit;
using RoomTrack.Contracts.Messaging;

namespace RoomTrack.Accommodations.Application.Internal.CommandServices;

/// <summary>
/// Service implementation for handling room commands.
/// Orchestrates the flow between the repository and the domain logic.
/// </summary>
public class RoomCommandService(
    IRoomRepository roomRepository,
    IRoomStatusChangeRepository roomStatusChangeRepository,
    IRoomTypeRepository roomTypeRepository,
    IRoomReservationsFacade roomReservationsFacade,
    IUnitOfWork unitOfWork,
    IOptions<RoomOperationsSettings> settings,
    TimeProvider timeProvider,
    IPublishEndpoint publishEndpoint)
    : IRoomCommandService
{
    public async Task<Room?> Handle(CreateRoomCommand command)
    {
        var room = new Room(command);
        await EnsureRoomTypeExistsAsync(command.RoomTypeId);
        if (await roomRepository.ExistsNumberInHotelAsync(room.HotelId, room.Number))
            throw new DuplicateRoomNumberException(room.Number, room.HotelId);
        // The integration event needs the generated id: saved first, then published through the outbox in the same
        // transaction (the event exists if and only if the room does).
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await roomRepository.AddAsync(room);
            await unitOfWork.CompleteAsync();
            await publishEndpoint.Publish(new RoomRegisteredIntegrationEvent(room.Id, room.HotelId, timeProvider.GetUtcNow()));
            await unitOfWork.CompleteAsync();
        });
        return room;
    }

    public async Task<Room?> Handle(UpdateRoomCommand command)
    {
        var room = await roomRepository.FindByIdAsync(command.Id);
        if (room is null) return null;

        await EnsureRoomTypeExistsAsync(command.RoomTypeId);
        if (command.Number is not null)
        {
            room.Renumber(command.Number);
            if (await roomRepository.ExistsNumberInHotelAsync(room.HotelId, room.Number, excludingRoomId: room.Id))
                throw new DuplicateRoomNumberException(room.Number, room.HotelId);
        }

        // Apply domain updates (a new price only applies to new bookings: bookings keep the price they were made at)
        room.UpdateInformation(
            command.RoomTypeId,
            command.Price,
            command.Description,
            command.Amenities
        );

        roomRepository.Update(room);
        await unitOfWork.CompleteAsync();
        return room;
    }

    public async Task<Room?> Handle(ChangeRoomStatusCommand command)
    {
        Room? room = null;
        // Locked like every other status change: the Housekeeping handler of this change checks the open cleaning
        // task of the room under the same lock as a task created by reception.
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            room = await roomRepository.FindByIdForUpdateAsync(command.RoomId);
            if (room is null) return;

            // The history line is saved with the change itself (same unit of work).
            var change = room.ChangeStatus(command.Status, RoomStatusChangeOrigin.Staff, command.ChangedByUserId,
                command.ChangedByEmail, timeProvider.GetUtcNow());
            if (change is not null) await roomStatusChangeRepository.AddAsync(change);
            await unitOfWork.CompleteAsync();
        });
        return room;
    }

    public async Task<Room> Handle(OccupyRoomForCheckInCommand command)
    {
        var room = await roomRepository.FindByIdForUpdateAsync(command.RoomId)
                   ?? throw new EntityNotFoundException("Room", command.RoomId);
        await roomStatusChangeRepository.AddAsync(room.OccupyForCheckIn(command.GuestUserId, command.GuestEmail, timeProvider.GetUtcNow()));
        await unitOfWork.CompleteAsync();
        return room;
    }

    public async Task<Room> Handle(VacateRoomAfterCheckOutCommand command)
    {
        Room? room = null;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            room = await roomRepository.FindByIdForUpdateAsync(command.RoomId)
                   ?? throw new EntityNotFoundException("Room", command.RoomId);
            var change = room.VacateAfterCheckOut(command.CheckedOutByUserId, command.CheckedOutByEmail, timeProvider.GetUtcNow());
            if (change is not null) await roomStatusChangeRepository.AddAsync(change);
            await unitOfWork.CompleteAsync();
        });
        return room!;
    }

    public async Task<Room> Handle(HoldRoomForIncidentCommand command)
    {
        var room = await roomRepository.FindByIdForUpdateAsync(command.RoomId)
                   ?? throw new EntityNotFoundException("Room", command.RoomId);
        var change = room.HoldForIncident(command.ReportedByUserId, command.ReportedByEmail, timeProvider.GetUtcNow());
        if (change is not null) await roomStatusChangeRepository.AddAsync(change);
        await unitOfWork.CompleteAsync();
        return room;
    }

    public async Task<Room> Handle(ReleaseRoomFromIncidentCommand command)
    {
        var room = await roomRepository.FindByIdForUpdateAsync(command.RoomId)
                   ?? throw new EntityNotFoundException("Room", command.RoomId);
        var change = room.ReleaseFromIncident(command.ResolvedByUserId, command.ResolvedByEmail, timeProvider.GetUtcNow());
        if (change is not null) await roomStatusChangeRepository.AddAsync(change);
        await unitOfWork.CompleteAsync();
        return room;
    }

    public async Task<Room> Handle(BeginRoomCleaningCommand command)
    {
        var room = await roomRepository.FindByIdForUpdateAsync(command.RoomId)
                   ?? throw new EntityNotFoundException("Room", command.RoomId);
        var change = room.BeginCleaning(command.CleanedByUserId, command.CleanedByEmail, timeProvider.GetUtcNow());
        if (change is not null) await roomStatusChangeRepository.AddAsync(change);
        await unitOfWork.CompleteAsync();
        return room;
    }

    public async Task<Room> Handle(FinishRoomCleaningCommand command)
    {
        var room = await roomRepository.FindByIdForUpdateAsync(command.RoomId)
                   ?? throw new EntityNotFoundException("Room", command.RoomId);
        var change = room.FinishCleaning(command.CleanedByUserId, command.CleanedByEmail, timeProvider.GetUtcNow());
        if (change is not null) await roomStatusChangeRepository.AddAsync(change);
        await unitOfWork.CompleteAsync();
        return room;
    }

    private async Task EnsureRoomTypeExistsAsync(int roomTypeId)
    {
        if (await roomTypeRepository.FindByIdAsync(roomTypeId) is null)
            throw new InvalidFieldException("roomTypeId", AccommodationErrorCodes.RoomTypeNotFound, $"Room type {roomTypeId} does not exist.");
    }

    public async Task<int> Handle(RaiseMaintenanceAlertsCommand command)
    {
        var now = timeProvider.GetUtcNow();
        var alerted = 0;
        foreach (var room in await roomRepository.ListInMaintenanceAsync())
            if (room.RaiseMaintenanceAlert(now, settings.Value.MaintenanceAlertAfter)) alerted++;
        await unitOfWork.CompleteAsync();
        return alerted;
    }

public async Task<Room?> Handle(DeleteRoomCommand command)
{
    // Find the room by its identifier
    var room = await roomRepository.FindByIdAsync(command.Id);

    // Return null if the room does not exist
    if (room is null) return null;

    // A room that still holds bookings cannot disappear under them.
    var active = await roomReservationsFacade.CountActiveBookingsAsync([room.Id]);
    if (active.TryGetValue(room.Id, out var count) && count > 0)
        throw new RoomHasActiveBookingsException(AccommodationErrorCodes.RoomHasActiveBookings,
            $"Room {room.Number} has {count} active booking(s) (pending, confirmed or checked in). Cancel or move them before deleting the room.",
            count);

    // Remove the room from the repository
    roomRepository.Remove(room);
    await publishEndpoint.Publish(new RoomRemovedIntegrationEvent(room.Id, room.HotelId, timeProvider.GetUtcNow()));

    // Save the changes to the database
    await unitOfWork.CompleteAsync();

    // Return the deleted room
    return room;
}
}
