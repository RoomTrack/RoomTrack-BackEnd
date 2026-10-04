using BackendAwRoomTrack.API.Marketing.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Marketing.Domain.Model.Queries;
using BackendAwRoomTrack.API.Marketing.Domain.Repositories;
using BackendAwRoomTrack.API.Marketing.Domain.Services;

namespace BackendAwRoomTrack.API.Marketing.Application.Internal.QueryServices;

public class DemoRequestQueryService(IDemoRequestRepository demoRequestRepository) : IDemoRequestQueryService
{
    public Task<DemoRequestPage> Handle(GetDemoRequestsQuery query) => demoRequestRepository.SearchAsync(query);

    public Task<DemoRequest?> Handle(GetDemoRequestByIdQuery query) => demoRequestRepository.FindByIdAsync(query.Id);
}
