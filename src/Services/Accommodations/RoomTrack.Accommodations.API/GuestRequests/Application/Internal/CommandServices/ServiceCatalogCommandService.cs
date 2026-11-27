using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Commands;
using RoomTrack.GuestRequests.Domain.Repositories;
using RoomTrack.GuestRequests.Domain.Services;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.GuestRequests.Application.Internal.CommandServices;

/// <summary>The service catalog of each hotel (US-33), kept by its administrators.</summary>
public class ServiceCatalogCommandService(
    IServiceCatalogRepository catalogRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IServiceCatalogCommandService
{
    public async Task<ServiceCatalogItem> Handle(CreateServiceCatalogItemCommand command)
    {
        var item = ServiceCatalogItem.Create(command.HotelId, command.Details, timeProvider.GetUtcNow());
        await catalogRepository.AddAsync(item);
        await unitOfWork.CompleteAsync();
        return item;
    }

    public async Task<ServiceCatalogItem?> Handle(UpdateServiceCatalogItemCommand command)
    {
        var item = await catalogRepository.FindByIdAsync(command.ServiceItemId);
        if (item is null) return null;
        item.Update(command.Details, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return item;
    }

    public async Task<bool> Handle(DeleteServiceCatalogItemCommand command)
    {
        var item = await catalogRepository.FindByIdAsync(command.ServiceItemId);
        if (item is null) return false;
        catalogRepository.Remove(item);
        await unitOfWork.CompleteAsync();
        return true;
    }
}
