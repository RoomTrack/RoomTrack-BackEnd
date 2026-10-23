using BackendAwRoomTrack.API.Bookings.Domain.Model.Queries;
using BackendAwRoomTrack.API.Bookings.Domain.Model.ValueObjects;

namespace BackendAwRoomTrack.API.Bookings.Domain.Services;

public interface IRoomAvailabilityQueryService
{
    Task<IReadOnlyList<AvailableRoom>> Handle(GetAvailableRoomsQuery query);
}
