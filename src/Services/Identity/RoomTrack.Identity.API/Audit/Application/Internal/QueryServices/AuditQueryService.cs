using RoomTrack.Audit.Domain.Model.Aggregates;
using RoomTrack.Audit.Domain.Model.Queries;
using RoomTrack.Audit.Domain.Repositories;
using RoomTrack.Audit.Domain.Services;

namespace RoomTrack.Audit.Application.Internal.QueryServices;

public class AuditQueryService(IAuditEntryRepository auditEntryRepository) : IAuditQueryService
{
    public Task<PagedResult<AuditEntry>> Handle(GetAuditEntriesQuery query) => auditEntryRepository.SearchAsync(query);
}
