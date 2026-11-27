using Microsoft.EntityFrameworkCore;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;

namespace RoomTrack.GuestRequests.Infrastructure.Persistence.EFC.Repositories;

public class ServiceCatalogRepository(AppDbContext context) : IServiceCatalogRepository
{
    public async Task AddAsync(ServiceCatalogItem item) => await context.Set<ServiceCatalogItem>().AddAsync(item);

    public Task<ServiceCatalogItem?> FindByIdAsync(int id) =>
        context.Set<ServiceCatalogItem>().FirstOrDefaultAsync(item => item.Id == id);

    public async Task<IReadOnlyList<ServiceCatalogItem>> ListAsync(int hotelId, bool onlyAvailable)
    {
        var items = context.Set<ServiceCatalogItem>().Where(item => item.HotelId == hotelId);
        if (onlyAvailable) items = items.Where(item => item.IsAvailable);
        return await items.OrderBy(item => item.Area).ThenBy(item => item.Name).ToListAsync();
    }

    public void Remove(ServiceCatalogItem item) => context.Set<ServiceCatalogItem>().Remove(item);
}
