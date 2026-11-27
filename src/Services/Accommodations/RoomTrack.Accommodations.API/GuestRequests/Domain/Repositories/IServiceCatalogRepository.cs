using RoomTrack.GuestRequests.Domain.Model.Aggregates;

namespace RoomTrack.GuestRequests.Domain.Repositories;

public interface IServiceCatalogRepository
{
    Task AddAsync(ServiceCatalogItem item);
    Task<ServiceCatalogItem?> FindByIdAsync(int id);

    /// <summary>The services of the hotel by area and name; only the available ones when <paramref name="onlyAvailable"/>.</summary>
    Task<IReadOnlyList<ServiceCatalogItem>> ListAsync(int hotelId, bool onlyAvailable);

    void Remove(ServiceCatalogItem item);
}
