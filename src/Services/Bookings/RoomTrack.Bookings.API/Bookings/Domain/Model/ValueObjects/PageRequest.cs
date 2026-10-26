using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Bookings.Domain.Model.ValueObjects;

/// <summary>A page of a list: <see cref="Page"/> starts at 1 and holds at most <see cref="MaxPageSize"/> items.</summary>
public sealed record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public PageRequest(int page, int pageSize)
    {
        if (page < 1)
            throw new InvalidFieldException("page", BookingErrorCodes.PageInvalid, "The page starts at 1.");
        if (pageSize is < 1 or > MaxPageSize)
            throw new InvalidFieldException("pageSize", BookingErrorCodes.PageSizeInvalid,
                $"The page size goes from 1 to {MaxPageSize}.",
                new Dictionary<string, object?> { ["maxPageSize"] = MaxPageSize });
        Page = page;
        PageSize = pageSize;
    }

    public int Page { get; }
    public int PageSize { get; }

    /// <summary>Items before this page.</summary>
    public int Skip => (Page - 1) * PageSize;
}
