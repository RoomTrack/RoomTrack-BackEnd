using BackendAwRoomTrack.API.IAM.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.IAM.Domain.Model.Enums;
using BackendAwRoomTrack.API.IAM.Domain.Repositories;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BackendAwRoomTrack.API.IAM.Infrastructure.Persistence.EFC.Repositories;

public class AccountTokenRepository(AppDbContext context) : BaseRepository<AccountToken>(context), IAccountTokenRepository
{
    public Task<AccountToken?> FindByHashAsync(AccountTokenPurpose purpose, string tokenHash) =>
        Context.Set<AccountToken>().FirstOrDefaultAsync(t => t.Purpose == purpose && t.TokenHash == tokenHash);

    public async Task<IReadOnlyList<AccountToken>> ListOutstandingAsync(int userId, AccountTokenPurpose purpose) =>
        await Context.Set<AccountToken>()
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAt == null && t.RevokedAt == null)
            .ToListAsync();
}
