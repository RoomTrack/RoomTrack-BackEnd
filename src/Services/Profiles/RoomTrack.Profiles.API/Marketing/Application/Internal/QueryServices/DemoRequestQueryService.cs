using RoomTrack.Marketing.Domain.Model.Aggregates;
using RoomTrack.Marketing.Domain.Model.Queries;
using RoomTrack.Marketing.Domain.Repositories;
using RoomTrack.Marketing.Domain.Services;

namespace RoomTrack.Marketing.Application.Internal.QueryServices;

public class DemoRequestQueryService(IDemoRequestRepository demoRequestRepository) : IDemoRequestQueryService
{
    public Task<DemoRequestPage> Handle(GetDemoRequestsQuery query) => demoRequestRepository.SearchAsync(query);

    public Task<DemoRequest?> Handle(GetDemoRequestByIdQuery query) => demoRequestRepository.FindByIdAsync(query.Id);
}
