using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Accommodations.Domain.Model.Queries;

namespace RoomTrack.Accommodations.Domain.Services;

public interface IHotelQueryService
{
    Task<Hotel?> Handle(GetHotelByIdQuery query);
    Task<IEnumerable<Hotel>> Handle(GetAllHotelsQuery query);
}