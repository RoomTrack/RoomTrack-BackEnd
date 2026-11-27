using System.ComponentModel.DataAnnotations;
using RoomTrack.Bookings.Domain.Model.Aggregates;

namespace RoomTrack.Bookings.Interfaces.REST.Resources;

/// <summary>The guest's evaluation of their stay (US-21). Ratings go from 1 to 5.</summary>
public record SubmitStayReviewResource
{
    /// <summary>The stay as a whole.</summary>
    /// <example>5</example>
    [Required]
    [Range(StayReview.MinRating, StayReview.MaxRating)]
    public int? OverallRating { get; init; }

    /// <summary>The front desk: check-in, check-out, answers to requests (optional).</summary>
    /// <example>5</example>
    [Range(StayReview.MinRating, StayReview.MaxRating)]
    public int? FrontDeskRating { get; init; }

    /// <summary>The cleaning of the room (optional).</summary>
    /// <example>4</example>
    [Range(StayReview.MinRating, StayReview.MaxRating)]
    public int? HousekeepingRating { get; init; }

    /// <summary>The state of the room and its repairs (optional).</summary>
    /// <example>3</example>
    [Range(StayReview.MinRating, StayReview.MaxRating)]
    public int? MaintenanceRating { get; init; }

    /// <summary>What the guest wants to tell the hotel (up to 1000 characters).</summary>
    /// <example>Excelente atención en recepción. La ducha tardaba en calentar.</example>
    [StringLength(StayReview.CommentMaxLength)]
    public string? Comment { get; init; }
}

/// <summary>The evaluation of a stay.</summary>
/// <param name="Id">The evaluation.</param>
/// <param name="BookingId">The booking of the stay.</param>
/// <param name="HotelId">The hotel.</param>
/// <param name="RoomId">The room.</param>
/// <param name="GuestUserId">The guest who evaluated it.</param>
/// <param name="OverallRating">The stay as a whole, 1-5.</param>
/// <param name="FrontDeskRating">The front desk, 1-5 (null when not rated).</param>
/// <param name="HousekeepingRating">The cleaning, 1-5 (null when not rated).</param>
/// <param name="MaintenanceRating">The state of the room and its repairs, 1-5 (null when not rated).</param>
/// <param name="Comment">The comment of the guest.</param>
/// <param name="SubmittedAt">When it was submitted.</param>
public record StayReviewResource(
    int Id,
    int BookingId,
    int HotelId,
    int RoomId,
    int GuestUserId,
    int OverallRating,
    int? FrontDeskRating,
    int? HousekeepingRating,
    int? MaintenanceRating,
    string? Comment,
    DateTimeOffset SubmittedAt);

/// <summary>What the guests said about the stay or an area.</summary>
/// <param name="Count">Ratings received.</param>
/// <param name="Average">Average rating, 1-5 (null without ratings).</param>
/// <param name="SatisfiedPercent">Share of ratings of 4 or 5, 0-100 (null without ratings).</param>
/// <param name="Distribution">How many ratings of 1, 2, 3, 4 and 5, in that order.</param>
public record SatisfactionScoreResource(int Count, double? Average, double? SatisfiedPercent, IReadOnlyList<int> Distribution);

/// <summary>The satisfaction of the guests with an area.</summary>
/// <param name="Area">FrontDesk, Housekeeping or Maintenance.</param>
/// <param name="Count">Ratings received.</param>
/// <param name="Average">Average rating, 1-5 (null without ratings).</param>
/// <param name="SatisfiedPercent">Share of ratings of 4 or 5, 0-100 (null without ratings).</param>
/// <param name="Distribution">How many ratings of 1, 2, 3, 4 and 5, in that order.</param>
public record AreaSatisfactionResource(
    string Area,
    int Count,
    double? Average,
    double? SatisfiedPercent,
    IReadOnlyList<int> Distribution);

/// <summary>US-26: the satisfaction of the guests of a hotel, overall and by area.</summary>
/// <param name="HotelId">The hotel.</param>
/// <param name="From">Start of the period (inclusive).</param>
/// <param name="To">End of the period (exclusive).</param>
/// <param name="Overall">The overall rating of the stays.</param>
/// <param name="Areas">FrontDesk, Housekeeping and Maintenance, always the three.</param>
public record SatisfactionReportResource(
    int HotelId,
    DateTimeOffset From,
    DateTimeOffset To,
    SatisfactionScoreResource Overall,
    IReadOnlyList<AreaSatisfactionResource> Areas);
