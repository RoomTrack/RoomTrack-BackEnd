using BackendAwRoomTrack.API.Marketing.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Marketing.Domain.Model.Commands;
using BackendAwRoomTrack.API.Marketing.Domain.Model.Queries;

namespace BackendAwRoomTrack.API.Marketing.Domain.Services;

public interface IDemoRequestCommandService
{
    Task<DemoRequest> Handle(SubmitDemoRequestCommand command);

    /// <summary>Returns how many requests got their follow-up in this run.</summary>
    Task<int> Handle(SendDemoFollowUpsCommand command);
}

public interface IDemoRequestQueryService
{
    Task<DemoRequestPage> Handle(GetDemoRequestsQuery query);

    Task<DemoRequest?> Handle(GetDemoRequestByIdQuery query);
}
