using BackendAwRoomTrack.API.Accommodations.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Queries;

namespace BackendAwRoomTrack.API.Accommodations.Domain.Services;

public interface IHotelQueryService
{
    Task<Hotel?> Handle(GetHotelByIdQuery query);
    Task<IEnumerable<Hotel>> Handle(GetAllHotelsQuery query);
}