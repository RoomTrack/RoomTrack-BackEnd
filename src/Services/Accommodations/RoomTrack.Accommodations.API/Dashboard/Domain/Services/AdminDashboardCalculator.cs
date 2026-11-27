using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Dashboard.Domain.Model;

namespace RoomTrack.Dashboard.Domain.Services;

/// <summary>Builds the administrator's panel from the operational state of the hotel (US-18). Pure.</summary>
public static class AdminDashboardCalculator
{
    /// <summary>The most items listed under "needs attention".</summary>
    public const int MaxAttentionItems = 10;

    public static AdminDashboard Build(int hotelId, HotelOperations operations, FrontDeskSummary? frontDesk,
        TimeSpan overdueAfter, DateTimeOffset now)
    {
        var rooms = operations.Rooms;
        var occupied = rooms.Count(room => room.Status == "Occupied");
        var roomNumbers = rooms.ToDictionary(room => room.RoomId, room => room.Number);
        var incidents = operations.Incidents;
        var requests = operations.GuestRequests;
        var late = requests.Where(request => now > request.EstimatedAttentionBy).ToList();

        var attention = incidents
            .Where(incident => incident.EscalatedAt is not null || incident.Priority == "Critical")
            .Select(incident => new AttentionItem("Incident", incident.IncidentId, incident.RoomId, Number(roomNumbers, incident.RoomId),
                incident.Title, incident.EscalatedAt is null ? "Critical" : "Escalated", incident.ReportedAt))
            .Concat(late.Select(request => new AttentionItem("GuestRequest", request.RequestId, request.RoomId,
                Number(roomNumbers, request.RoomId), request.Title, "Late", request.EstimatedAttentionBy)))
            // Escalated first, then critical, then late requests; the oldest first within each.
            .OrderBy(item => item.Reason switch { "Escalated" => 0, "Critical" => 1, _ => 2 })
            .ThenBy(item => item.Since)
            .Take(MaxAttentionItems)
            .ToList();

        return new AdminDashboard(
            hotelId,
            now,
            new RoomsSummary(
                rooms.Count,
                rooms.Count(room => room.Status == "Available"),
                occupied,
                rooms.Count(room => room.Status == "Cleaning"),
                rooms.Count(room => room.Status == "Maintenance"),
                rooms.Count == 0 ? 0 : Math.Round(100d * occupied / rooms.Count, 1, MidpointRounding.AwayFromZero)),
            new CleaningSummary(
                operations.CleaningTasks.Count,
                operations.CleaningTasks.Count(task => task.AssigneeUserId is null),
                operations.CleaningTasks.Count(task => now - task.CreatedAt >= overdueAfter),
                (int)overdueAfter.TotalMinutes),
            new IncidentsSummary(
                incidents.Count,
                incidents.Count(incident => incident.Priority == "Critical"),
                incidents.Count(incident => incident.Priority == "High"),
                incidents.Count(incident => incident.BlocksRoom),
                incidents.Count(incident => incident.EscalatedAt is not null),
                incidents.Count(incident => incident.IsRecurring)),
            new GuestRequestsSummary(
                requests.Count,
                requests.Count(request => request.Status == "Pending"),
                late.Count),
            frontDesk,
            attention);
    }

    private static string? Number(IReadOnlyDictionary<int, string> numbers, int roomId) =>
        numbers.TryGetValue(roomId, out var number) ? number : null;
}
