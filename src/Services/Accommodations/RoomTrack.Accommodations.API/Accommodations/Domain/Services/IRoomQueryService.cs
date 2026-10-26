using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Domain.Model.Queries;

namespace RoomTrack.Accommodations.Domain.Services;

public interface IRoomQueryService
{

    Task<Room?> Handle(GetRoomByIdQuery query);

    Task<Room?> Handle(GetRoomByIdForUpdateQuery query);

    Task<IEnumerable<Room>> Handle(GetAllRoomsQuery query);

    Task<IEnumerable<Room>> Handle(GetRoomsByTypeQuery query);

    Task<IEnumerable<Room>> Handle(GetRoomsOfferedForBookingQuery query);

    Task<IReadOnlyList<Room>> Handle(GetRoomMapQuery query);

    Task<IReadOnlyList<Model.Entities.RoomStatusChange>> Handle(GetRoomStatusHistoryQuery query);
}

