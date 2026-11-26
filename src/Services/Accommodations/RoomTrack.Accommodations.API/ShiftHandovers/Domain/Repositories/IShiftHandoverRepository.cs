using RoomTrack.ShiftHandovers.Domain.Model.Aggregates;
using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;

namespace RoomTrack.ShiftHandovers.Domain.Repositories;

public interface IShiftHandoverRepository
{
    Task AddAsync(ShiftHandover handover);

    /// <summary>The handover with its items and confirmations, or null.</summary>
    Task<ShiftHandover?> FindByIdAsync(int id);

    /// <summary>The handovers of the hotel, newest first, with their items and confirmations.</summary>
    Task<IReadOnlyList<ShiftHandover>> ListAsync(int hotelId, ShiftArea? area, int limit);
}
