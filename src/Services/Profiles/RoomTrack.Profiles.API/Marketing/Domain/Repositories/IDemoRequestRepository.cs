using BackendAwRoomTrack.API.Marketing.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Marketing.Domain.Model.Queries;
using BackendAwRoomTrack.API.Shared.Domain.Repositories;

namespace BackendAwRoomTrack.API.Marketing.Domain.Repositories;

public interface IDemoRequestRepository : IBaseRepository<DemoRequest>
{
    /// <summary>Requests still Received that arrived at or before <paramref name="receivedAtOrBefore"/>.</summary>
    Task<IReadOnlyList<DemoRequest>> FindWaitingSinceAsync(DateTimeOffset receivedAtOrBefore);

    Task<DemoRequestPage> SearchAsync(GetDemoRequestsQuery query);
}
