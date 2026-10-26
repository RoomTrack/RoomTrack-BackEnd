using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Domain.Model.Entities;
using RoomTrack.Accommodations.Domain.Repositories;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.Accommodations.Application.Internal.CommandServices;

/// <summary>
/// Handles command operations related to the RoomType entity.
/// Provides application-level logic for creating room type records.
/// </summary>

public class RoomTypeCommandService(
    IRoomTypeRepository roomTypeRepository,
    IUnitOfWork unitOfWork)
    : IRoomTypeCommandService
{/// <summary>
    /// Creates a new room type using the provided command
    /// and persists it through the repository.
    /// </summary>
    /// <param name="command">Command containing room type creation data.</param>
    /// <returns>The created RoomType entity.</returns>
    /// <inheritdoc />
    public async Task<RoomType?> Handle(CreateRoomTypeCommand command)
    {
        var roomType = new RoomType(command);
        await roomTypeRepository.AddAsync(roomType);
        await unitOfWork.CompleteAsync();

        return roomType;
    }
}

