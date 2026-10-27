using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;

namespace RoomTrack.Bookings.Domain.Services;

public interface IRoomAvailabilityQueryService
{
    Task<IReadOnlyList<AvailableRoom>> Handle(GetAvailableRoomsQuery query);
}
