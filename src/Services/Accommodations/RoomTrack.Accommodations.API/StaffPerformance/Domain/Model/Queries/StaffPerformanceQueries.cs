namespace RoomTrack.StaffPerformance.Domain.Model.Queries;

/// <summary>US-24: the work finished by each staff member and each area of a hotel in [From, To).</summary>
public record GetStaffProductivityQuery(int HotelId, DateTimeOffset From, DateTimeOffset To);

/// <summary>US-25: the average service times of a hotel for the work finished in [From, To).</summary>
public record GetServiceTimesQuery(int HotelId, DateTimeOffset From, DateTimeOffset To);
