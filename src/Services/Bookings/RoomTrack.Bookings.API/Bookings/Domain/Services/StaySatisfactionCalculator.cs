using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.ValueObjects;

namespace RoomTrack.Bookings.Domain.Services;

/// <summary>Groups the ratings of the stay reviews by area (US-26). Pure: the same reviews always give the same report.</summary>
public static class StaySatisfactionCalculator
{
    /// <summary>Ratings from this value up count as satisfied.</summary>
    public const int SatisfiedFrom = 4;

    /// <summary>The overall satisfaction and that of every area (always the three, with no ratings when none was rated).</summary>
    public static SatisfactionReport Summarize(int hotelId, DateTimeOffset from, DateTimeOffset to,
        IReadOnlyCollection<StayReview> reviews) =>
        new(hotelId, from, to,
            Score(reviews.Select(review => (int?)review.OverallRating)),
            Enum.GetValues<SatisfactionArea>()
                .Select(area => new AreaSatisfaction(area, Score(reviews.Select(review => RatingOf(review, area)))))
                .ToList());

    private static int? RatingOf(StayReview review, SatisfactionArea area) => area switch
    {
        SatisfactionArea.FrontDesk => review.FrontDeskRating,
        SatisfactionArea.Housekeeping => review.HousekeepingRating,
        _ => review.MaintenanceRating
    };

    private static SatisfactionScore Score(IEnumerable<int?> ratings)
    {
        var given = ratings.Where(rating => rating is not null).Select(rating => rating!.Value).ToList();
        var distribution = Enumerable.Range(StayReview.MinRating, StayReview.MaxRating - StayReview.MinRating + 1)
            .Select(value => given.Count(rating => rating == value))
            .ToList();
        if (given.Count == 0) return new SatisfactionScore(0, null, null, distribution);

        return new SatisfactionScore(
            given.Count,
            Math.Round(given.Average(), 2, MidpointRounding.AwayFromZero),
            Math.Round(100d * given.Count(rating => rating >= SatisfiedFrom) / given.Count, 1, MidpointRounding.AwayFromZero),
            distribution);
    }
}
