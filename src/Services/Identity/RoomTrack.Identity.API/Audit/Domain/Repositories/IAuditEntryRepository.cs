using RoomTrack.Audit.Domain.Model.Aggregates;
using RoomTrack.Audit.Domain.Model.Queries;

namespace RoomTrack.Audit.Domain.Repositories;

/// <summary>Append-only store of the audit log.</summary>
public interface IAuditEntryRepository
{
    Task AddAsync(AuditEntry entry);

    Task<PagedResult<AuditEntry>> SearchAsync(GetAuditEntriesQuery query);
}
