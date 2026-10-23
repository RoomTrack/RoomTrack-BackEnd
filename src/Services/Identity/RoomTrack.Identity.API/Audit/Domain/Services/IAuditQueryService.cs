using BackendAwRoomTrack.API.Audit.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Audit.Domain.Model.Queries;

namespace BackendAwRoomTrack.API.Audit.Domain.Services;

public interface IAuditQueryService
{
    Task<PagedResult<AuditEntry>> Handle(GetAuditEntriesQuery query);
}
