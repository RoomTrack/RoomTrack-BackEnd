using BackendAwRoomTrack.API.Audit.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Audit.Domain.Model.Queries;
using BackendAwRoomTrack.API.Audit.Domain.Repositories;
using BackendAwRoomTrack.API.Audit.Domain.Services;

namespace BackendAwRoomTrack.API.Audit.Application.Internal.QueryServices;

public class AuditQueryService(IAuditEntryRepository auditEntryRepository) : IAuditQueryService
{
    public Task<PagedResult<AuditEntry>> Handle(GetAuditEntriesQuery query) => auditEntryRepository.SearchAsync(query);
}
