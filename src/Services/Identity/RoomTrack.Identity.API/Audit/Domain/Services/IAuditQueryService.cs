using RoomTrack.Audit.Domain.Model.Aggregates;
using RoomTrack.Audit.Domain.Model.Queries;

namespace RoomTrack.Audit.Domain.Services;

public interface IAuditQueryService
{
    Task<PagedResult<AuditEntry>> Handle(GetAuditEntriesQuery query);
}
