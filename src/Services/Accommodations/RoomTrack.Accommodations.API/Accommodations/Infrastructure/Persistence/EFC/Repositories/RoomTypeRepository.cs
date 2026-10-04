using BackendAwRoomTrack.API.Accommodations.Domain.Model.Entities;
using BackendAwRoomTrack.API.Accommodations.Domain.Repositories;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Repositories;

namespace BackendAwRoomTrack.API.Accommodations.Infrastructure.Persistence.EFC.Repositories;

public class RoomTypeRepository(AppDbContext context) : BaseRepository<RoomType>(context), IRoomTypeRepository;