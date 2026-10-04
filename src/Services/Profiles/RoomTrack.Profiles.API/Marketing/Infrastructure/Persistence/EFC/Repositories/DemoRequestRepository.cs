using BackendAwRoomTrack.API.Marketing.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Marketing.Domain.Model.Queries;
using BackendAwRoomTrack.API.Marketing.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Marketing.Domain.Repositories;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BackendAwRoomTrack.API.Marketing.Infrastructure.Persistence.EFC.Repositories;

public class DemoRequestRepository(AppDbContext context) : BaseRepository<DemoRequest>(context), IDemoRequestRepository
{
    public async Task<IReadOnlyList<DemoRequest>> FindWaitingSinceAsync(DateTimeOffset receivedAtOrBefore) =>
        await Context.Set<DemoRequest>()
            .Where(r => r.Status == DemoRequestStatus.Received && r.ReceivedAt <= receivedAtOrBefore)
            .OrderBy(r => r.ReceivedAt)
            .ToListAsync();

    public async Task<DemoRequestPage> SearchAsync(GetDemoRequestsQuery query)
    {
        var requests = Context.Set<DemoRequest>().AsNoTracking();
        if (query.Status is { } status) requests = requests.Where(r => r.Status == status);

        var total = await requests.CountAsync();
        var items = await requests
            .OrderByDescending(r => r.ReceivedAt).ThenByDescending(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();
        return new DemoRequestPage(items, query.Page, query.PageSize, total);
    }
}
