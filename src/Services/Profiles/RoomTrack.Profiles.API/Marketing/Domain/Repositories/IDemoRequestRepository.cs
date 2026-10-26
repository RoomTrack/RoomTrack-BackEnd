using RoomTrack.Marketing.Domain.Model.Aggregates;
using RoomTrack.Marketing.Domain.Model.Queries;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.Marketing.Domain.Repositories;

public interface IDemoRequestRepository : IBaseRepository<DemoRequest>
{
    /// <summary>Requests still Received that arrived at or before <paramref name="receivedAtOrBefore"/>.</summary>
    Task<IReadOnlyList<DemoRequest>> FindWaitingSinceAsync(DateTimeOffset receivedAtOrBefore);

    Task<DemoRequestPage> SearchAsync(GetDemoRequestsQuery query);
}
