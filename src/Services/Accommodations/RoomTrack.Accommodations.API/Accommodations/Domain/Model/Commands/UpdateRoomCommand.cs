namespace BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;

/// <summary>Edit a room (the hotel cannot change). A new price only applies to new bookings.</summary>
public record UpdateRoomCommand(
    int Id,
    int RoomTypeId,
    decimal Price,
    string Description,
    List<string> Amenities,
    string? Number = null);
