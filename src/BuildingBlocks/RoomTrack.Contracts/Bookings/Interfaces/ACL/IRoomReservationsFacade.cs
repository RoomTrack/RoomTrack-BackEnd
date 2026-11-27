namespace RoomTrack.Bookings.Interfaces.ACL;

/// <summary>The stay of a guest today: their checked-in booking, or the confirmed one whose dates include today.</summary>
/// <param name="BookingId">The booking of the stay.</param>
/// <param name="Code">The booking code shown to the guest.</param>
/// <param name="HotelId">The hotel of the stay.</param>
/// <param name="RoomId">The room of the stay.</param>
/// <param name="GuestUserId">The IAM account of the guest, when the booking has one.</param>
/// <param name="CheckInDate">The first night of the stay.</param>
/// <param name="CheckOutDate">The day the guest leaves.</param>
/// <param name="CheckedIn">Whether the guest already checked in.</param>
public sealed record GuestStay(int BookingId, string Code, int HotelId, int RoomId, int? GuestUserId, DateTime CheckInDate,
    DateTime CheckOutDate, bool CheckedIn);

/// <summary>
///     Read-only view of the reservations of rooms, for the Accommodations context (e.g. a room with active bookings
///     cannot be deleted; a guest controls the devices of the room of their current stay). Separate from the Bookings
///     facade used by Payments so it depends on nothing of Accommodations (no dependency cycle between the two
///     contexts).
/// </summary>
/// <summary>The front desk of a hotel today and what its guests said lately, for the administrator's panel (US-18).</summary>
/// <param name="ArrivalsToday">Paid bookings whose stay covers today and that are not checked in yet.</param>
/// <param name="InHouse">Guests checked in.</param>
/// <param name="DeparturesToday">Guests checked in whose stay ends today (or ended and are still in).</param>
/// <param name="ReviewsLast30Days">Stays evaluated in the last 30 days.</param>
/// <param name="AverageRatingLast30Days">Their average overall rating, 1-5 (null without evaluations).</param>
/// <param name="LatestReviews">The latest evaluations, newest first (at most 3).</param>
public sealed record FrontDeskSummary(
    int ArrivalsToday,
    int InHouse,
    int DeparturesToday,
    int ReviewsLast30Days,
    double? AverageRatingLast30Days,
    IReadOnlyList<ReviewSnippet> LatestReviews);

/// <summary>An evaluation of a stay (US-21), as the panel shows it.</summary>
public sealed record ReviewSnippet(
    int BookingId,
    int RoomId,
    int OverallRating,
    int? FrontDeskRating,
    int? HousekeepingRating,
    int? MaintenanceRating,
    string? Comment,
    DateTimeOffset SubmittedAt);

public interface IRoomReservationsFacade
{
    /// <summary>How many active bookings (Pending, Confirmed, CheckedIn) hold each of <paramref name="roomIds"/>.</summary>
    Task<IReadOnlyDictionary<int, int>> CountActiveBookingsAsync(IReadOnlyCollection<int> roomIds);

    /// <summary>
    ///     True when the guest has a Confirmed booking of <paramref name="roomId"/> whose dates include
    ///     <paramref name="day"/> (R5: a guest controls the devices of the room they are staying in).
    /// </summary>
    Task<bool> HasCurrentConfirmedStayAsync(int guestUserId, int roomId, DateTime day);

    /// <summary>
    ///     The stay of the guest today (hotel time), or null when they are not staying anywhere: what a guest request or
    ///     a conversation with the front desk is about (US-19, US-20).
    /// </summary>
    Task<GuestStay?> FetchCurrentStayAsync(int guestUserId);

    /// <summary>The stay in the room today (hotel time), or null when nobody is staying there.</summary>
    Task<GuestStay?> FetchCurrentStayOfRoomAsync(int roomId);

    /// <summary>Arrivals, guests in house and departures of the hotel today, and its latest evaluations (US-18).</summary>
    Task<FrontDeskSummary> FetchFrontDeskSummaryAsync(int hotelId);
}
