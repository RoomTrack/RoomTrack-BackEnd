using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Queries;
using RoomTrack.GuestRequests.Domain.Repositories;
using RoomTrack.GuestRequests.Domain.Services;

namespace RoomTrack.GuestRequests.Application.Internal.QueryServices;

public class ServiceCatalogQueryService(IServiceCatalogRepository catalogRepository) : IServiceCatalogQueryService
{
    public Task<IReadOnlyList<ServiceCatalogItem>> Handle(GetServiceCatalogQuery query) =>
        catalogRepository.ListAsync(query.HotelId, query.OnlyAvailable);

    public Task<ServiceCatalogItem?> Handle(GetServiceCatalogItemByIdQuery query) =>
        catalogRepository.FindByIdAsync(query.ServiceItemId);
}
