using RoomTrack.Accommodations.Domain.Model.Entities;
using RoomTrack.Accommodations.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Repositories;

namespace RoomTrack.Accommodations.Infrastructure.Persistence.EFC.Repositories;

public class RoomTypeRepository(AppDbContext context) : BaseRepository<RoomType>(context), IRoomTypeRepository;