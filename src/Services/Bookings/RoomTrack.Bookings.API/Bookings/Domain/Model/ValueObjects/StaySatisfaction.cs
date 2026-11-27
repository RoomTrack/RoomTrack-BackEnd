namespace RoomTrack.Bookings.Domain.Model.ValueObjects;

/// <summary>An area of the hotel the guests rate (US-26).</summary>
public enum SatisfactionArea
{
    FrontDesk,
    Housekeeping,
    Maintenance
}

/// <summary>What the guests said about the stay or an area.</summary>
/// <param name="Count">Ratings received.</param>
/// <param name="Average">Average rating, 1-5 (null without ratings).</param>
/// <param name="SatisfiedPercent">Share of ratings of 4 or 5, 0-100 (null without ratings).</param>
/// <param name="Distribution">How many ratings of 1, 2, 3, 4 and 5 (in that order).</param>
public sealed record SatisfactionScore(int Count, double? Average, double? SatisfiedPercent, IReadOnlyList<int> Distribution);

/// <summary>The satisfaction of the guests with one area.</summary>
public sealed record AreaSatisfaction(SatisfactionArea Area, SatisfactionScore Score);

/// <summary>US-26: the satisfaction of the guests of a hotel, overall and by area, from the stays evaluated in a period.</summary>
public sealed record SatisfactionReport(
    int HotelId,
    DateTimeOffset From,
    DateTimeOffset To,
    SatisfactionScore Overall,
    IReadOnlyList<AreaSatisfaction> Areas);
