using RoomTrack.IAM.Domain.Model.Aggregates;
using RoomTrack.IAM.Domain.Model.Enums;
using RoomTrack.IAM.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Repositories;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.IAM.Infrastructure.Persistence.EFC.Repositories;

public class AccountTokenRepository(AppDbContext context) : BaseRepository<AccountToken>(context), IAccountTokenRepository
{
    public Task<AccountToken?> FindByHashAsync(AccountTokenPurpose purpose, string tokenHash) =>
        Context.Set<AccountToken>().FirstOrDefaultAsync(t => t.Purpose == purpose && t.TokenHash == tokenHash);

    public async Task<IReadOnlyList<AccountToken>> ListOutstandingAsync(int userId, AccountTokenPurpose purpose) =>
        await Context.Set<AccountToken>()
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAt == null && t.RevokedAt == null)
            .ToListAsync();
}
