using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Interfaces.REST.Resources;

namespace RoomTrack.Bookings.Interfaces.REST.Transform;

public static class StayReviewResourceAssembler
{
    public static StayRatings ToRatings(this SubmitStayReviewResource resource) => new(resource.OverallRating!.Value,
        resource.FrontDeskRating, resource.HousekeepingRating, resource.MaintenanceRating);

    public static StayReviewResource ToResource(StayReview review) => new(review.Id, review.BookingId, review.HotelId,
        review.RoomId, review.GuestUserId, review.OverallRating, review.FrontDeskRating, review.HousekeepingRating,
        review.MaintenanceRating, review.Comment, review.SubmittedAt);

    public static SatisfactionReportResource ToResource(SatisfactionReport report) => new(report.HotelId, report.From,
        report.To, ToResource(report.Overall),
        report.Areas.Select(area => new AreaSatisfactionResource(area.Area.ToString(), area.Score.Count, area.Score.Average,
            area.Score.SatisfiedPercent, area.Score.Distribution)).ToList());

    private static SatisfactionScoreResource ToResource(SatisfactionScore score) =>
        new(score.Count, score.Average, score.SatisfiedPercent, score.Distribution);
}
