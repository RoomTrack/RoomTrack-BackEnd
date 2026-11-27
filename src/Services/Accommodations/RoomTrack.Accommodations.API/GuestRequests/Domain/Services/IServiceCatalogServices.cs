using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Commands;
using RoomTrack.GuestRequests.Domain.Model.Queries;

namespace RoomTrack.GuestRequests.Domain.Services;

public interface IServiceCatalogCommandService
{
    Task<ServiceCatalogItem> Handle(CreateServiceCatalogItemCommand command);

    /// <returns>The service, or null when it does not exist.</returns>
    Task<ServiceCatalogItem?> Handle(UpdateServiceCatalogItemCommand command);

    /// <returns>False when it does not exist.</returns>
    Task<bool> Handle(DeleteServiceCatalogItemCommand command);
}

public interface IServiceCatalogQueryService
{
    Task<IReadOnlyList<ServiceCatalogItem>> Handle(GetServiceCatalogQuery query);
    Task<ServiceCatalogItem?> Handle(GetServiceCatalogItemByIdQuery query);
}
