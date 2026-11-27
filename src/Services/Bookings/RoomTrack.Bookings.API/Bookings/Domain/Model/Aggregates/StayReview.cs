using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Bookings.Domain.Model.Aggregates;

/// <summary>
///     The guest's evaluation of a finished stay (US-21): an overall rating, optional ratings of the areas that served
///     them (front desk, housekeeping, maintenance) and a comment. The ratings of the areas feed the satisfaction by
///     area (US-26).
/// </summary>
/// <remarks>
///     One per booking, by the guest of the booking, once the stay is checked out and for
///     <see cref="ReviewWindowDays"/> days after. The booking is a reference (id, hotel, room): the review never
///     changes it.
/// </remarks>
public class StayReview
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int CommentMaxLength = 1000;

    /// <summary>Days after the check-out during which the guest can still evaluate the stay.</summary>
    public const int ReviewWindowDays = 30;

    /// <summary>EF Core constructor.</summary>
    protected StayReview() { }

    private StayReview(Booking booking, int guestUserId, StayRatings ratings, string? comment, DateTimeOffset now)
    {
        BookingId = booking.Id;
        HotelId = booking.HotelId;
        RoomId = booking.RoomId;
        GuestUserId = guestUserId;
        OverallRating = ratings.Overall;
        FrontDeskRating = ratings.FrontDesk;
        HousekeepingRating = ratings.Housekeeping;
        MaintenanceRating = ratings.Maintenance;
        Comment = comment;
        SubmittedAt = now;
    }

    /// <summary>The guest of <paramref name="booking"/> evaluates their finished stay.</summary>
    /// <exception cref="BookingNotFoundException">The booking is not the guest's.</exception>
    /// <exception cref="InvalidBookingTransitionException">The stay is not checked out, or the window to evaluate it closed.</exception>
    /// <exception cref="InvalidFieldException">A rating out of 1-5 or a comment too long.</exception>
    public static StayReview Submit(Booking booking, BookingRequester guest, StayRatings ratings, string? comment,
        DateTimeOffset now)
    {
        if (!booking.IsOwnedBy(guest)) throw new BookingNotFoundException(booking.Id);
        if (booking.Status != BookingStatus.Completed || booking.CheckedOutAt is not { } checkedOutAt)
            throw new InvalidBookingTransitionException(BookingErrorCodes.ReviewStayNotFinished,
                $"A stay is evaluated after its check-out; this booking is {booking.Status.ToString().ToLowerInvariant()}.");
        if (now - checkedOutAt > TimeSpan.FromDays(ReviewWindowDays))
            throw new InvalidBookingTransitionException(BookingErrorCodes.ReviewWindowClosed,
                $"A stay can be evaluated up to {ReviewWindowDays} days after its check-out.");

        EnsureRating("overallRating", ratings.Overall);
        EnsureRating("frontDeskRating", ratings.FrontDesk);
        EnsureRating("housekeepingRating", ratings.Housekeeping);
        EnsureRating("maintenanceRating", ratings.Maintenance);

        var text = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (text is { Length: > CommentMaxLength })
            throw new InvalidFieldException("comment", BookingErrorCodes.ReviewCommentTooLong,
                $"The comment can have at most {CommentMaxLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = CommentMaxLength });

        return new StayReview(booking, guest.UserId, ratings, text, now);
    }

    public int Id { get; private set; }
    public int BookingId { get; private set; }
    public int HotelId { get; private set; }
    public int RoomId { get; private set; }

    /// <summary>The guest account that evaluated the stay.</summary>
    public int GuestUserId { get; private set; }

    /// <summary>The stay as a whole, 1-5.</summary>
    public int OverallRating { get; private set; }

    /// <summary>The front desk (check-in, check-out, requests), 1-5; null when the guest did not rate it.</summary>
    public int? FrontDeskRating { get; private set; }

    /// <summary>The cleaning of the room, 1-5; null when the guest did not rate it.</summary>
    public int? HousekeepingRating { get; private set; }

    /// <summary>The state of the room and its repairs, 1-5; null when the guest did not rate it.</summary>
    public int? MaintenanceRating { get; private set; }

    public string? Comment { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }

    private static void EnsureRating(string field, int? rating)
    {
        if (rating is null or (>= MinRating and <= MaxRating)) return;
        throw new InvalidFieldException(field, BookingErrorCodes.ReviewRatingOutOfRange,
            $"A rating goes from {MinRating} to {MaxRating}.",
            new Dictionary<string, object?> { ["min"] = MinRating, ["max"] = MaxRating });
    }
}

/// <summary>The ratings of a stay, 1-5: overall (required) and by area (optional).</summary>
public sealed record StayRatings(int Overall, int? FrontDesk = null, int? Housekeeping = null, int? Maintenance = null);

/// <summary>The guest already evaluated this stay.</summary>
public class StayReviewAlreadySubmittedException(int bookingId)
    : BusinessRuleViolationException(BookingErrorCodes.ReviewAlreadySubmitted, $"The stay of booking {bookingId} was already evaluated.");
