using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Queries;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.GuestRequests.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.GuestRequests.Infrastructure.Persistence.EFC.Repositories;

public class GuestRequestRepository(AppDbContext context) : IGuestRequestRepository
{
    public async Task AddAsync(GuestRequest request) => await context.Set<GuestRequest>().AddAsync(request);

    public Task<GuestRequest?> FindByIdAsync(int id) =>
        context.Set<GuestRequest>().Include(r => r.History).FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IReadOnlyList<GuestRequest>> ListAsync(GetGuestRequestsQuery query)
    {
        var requests = context.Set<GuestRequest>().Where(r => r.HotelId == query.HotelId);
        if (query.Area is { } area) requests = requests.Where(r => r.Area == area);
        if (query.Status is { } status) requests = requests.Where(r => r.Status == status);
        if (query.OnlyOpen) requests = requests.Where(r => r.Status == GuestRequestStatus.Pending || r.Status == GuestRequestStatus.InProgress);
        if (query.RoomId is { } roomId) requests = requests.Where(r => r.RoomId == roomId);
        if (query.AssigneeUserId is { } assigneeUserId) requests = requests.Where(r => r.AssigneeUserId == assigneeUserId);
        return await requests.OrderByDescending(r => r.Id).Take(query.Limit).ToListAsync();
    }

    public async Task<IReadOnlyList<GuestRequest>> ListOfGuestAsync(GetGuestRequestsOfGuestQuery query) =>
        await context.Set<GuestRequest>().Where(r => r.GuestUserId == query.GuestUserId)
            .OrderByDescending(r => r.Id).Take(query.Limit).ToListAsync();
}
