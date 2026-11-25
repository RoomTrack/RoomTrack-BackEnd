using Microsoft.EntityFrameworkCore;
using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Payments.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;

namespace RoomTrack.Payments.Infrastructure.Persistence.EFC.Repositories;

public class GuestFolioRepository(AppDbContext context) : IGuestFolioRepository
{
    public async Task AddAsync(GuestFolio folio) => await context.Set<GuestFolio>().AddAsync(folio);

    public Task<GuestFolio?> FindByBookingIdAsync(int bookingId) =>
        context.Set<GuestFolio>().Include(f => f.Charges).Include(f => f.Payments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.BookingId == bookingId);

    public async Task<IReadOnlyList<GuestFolio>> ListWithActivityAsync(int hotelId, DateTimeOffset from, DateTimeOffset to) =>
        await context.Set<GuestFolio>().Include(f => f.Charges).Include(f => f.Payments)
            .Where(f => f.HotelId == hotelId
                        && (f.Payments.Any(p => p.PaidAt >= from && p.PaidAt <= to)
                            || (f.DepositHeldAt != null && f.DepositHeldAt >= from && f.DepositHeldAt <= to)
                            || (f.DepositReleasedAt != null && f.DepositReleasedAt >= from && f.DepositReleasedAt <= to)))
            .AsSplitQuery()
            .ToListAsync();
}

public class ReceiptRepository(AppDbContext context) : IReceiptRepository
{
    public async Task AddAsync(Receipt receipt) => await context.Set<Receipt>().AddAsync(receipt);

    public Task<Receipt?> FindByIdAsync(int id) => context.Set<Receipt>().FirstOrDefaultAsync(r => r.Id == id);

    public async Task<bool> ExistsForSourceAsync(ReceiptSource source, int sourceId) =>
        context.ChangeTracker.Entries<Receipt>().Any(entry => entry.Entity.Source == source && entry.Entity.SourceId == sourceId)
        || await context.Set<Receipt>().AnyAsync(r => r.Source == source && r.SourceId == sourceId);

    public async Task<IReadOnlyList<Receipt>> ListByBookingIdAsync(int bookingId) =>
        await context.Set<Receipt>().Where(r => r.BookingId == bookingId).OrderBy(r => r.IssuedAt).ThenBy(r => r.Id).ToListAsync();

    public async Task<IReadOnlyList<Receipt>> ListByHotelAsync(int hotelId, DateTimeOffset from, DateTimeOffset to) =>
        await context.Set<Receipt>().Where(r => r.HotelId == hotelId && r.IssuedAt >= from && r.IssuedAt <= to).ToListAsync();

    public async Task<int> NextNumberAsync(int hotelId, string series)
    {
        var stored = await context.Set<Receipt>().Where(r => r.HotelId == hotelId && r.Series == series)
            .MaxAsync(r => (int?)r.Number) ?? 0;
        var added = context.ChangeTracker.Entries<Receipt>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.HotelId == hotelId && entry.Entity.Series == series)
            .Select(entry => entry.Entity.Number).DefaultIfEmpty(0).Max();
        return Math.Max(stored, added) + 1;
    }
}
