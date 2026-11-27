using RoomTrack.StaffPerformance.Domain.Model.ValueObjects;

namespace RoomTrack.StaffPerformance.Domain.Repositories;

/// <summary>Reads the work finished in a hotel (read only: the Housekeeping, Incidents and GuestRequests contexts own it).</summary>
public interface IFinishedWorkRepository
{
    /// <summary>
    ///     The cleaning tasks completed, the incidents resolved and the guest requests resolved in the hotel with their
    ///     finish instant in [<paramref name="from"/>, <paramref name="to"/>).
    /// </summary>
    Task<IReadOnlyList<FinishedWork>> ListFinishedAsync(int hotelId, DateTimeOffset from, DateTimeOffset to);
}
