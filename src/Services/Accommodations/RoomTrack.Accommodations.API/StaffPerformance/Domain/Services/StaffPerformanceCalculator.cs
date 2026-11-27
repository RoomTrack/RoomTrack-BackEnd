using RoomTrack.StaffPerformance.Domain.Model.ValueObjects;

namespace RoomTrack.StaffPerformance.Domain.Services;

/// <summary>
///     Calculates the productivity of the staff (US-24) and the service times (US-25) from the work finished in a
///     period. Pure: the same work always gives the same report.
/// </summary>
public static class StaffPerformanceCalculator
{
    /// <summary>
    ///     The work finished by each staff member (most productive first) and by each area. Every area is listed, with
    ///     zeros when it finished nothing, so the areas can be compared side by side.
    /// </summary>
    public static ProductivityReport Productivity(int hotelId, DateTimeOffset from, DateTimeOffset to,
        IReadOnlyCollection<FinishedWork> work, IReadOnlyDictionary<int, StaffMember> staff)
    {
        var collaborators = work
            .Where(item => item.UserId is not null)
            .GroupBy(item => item.UserId!.Value)
            .Select(group =>
            {
                staff.TryGetValue(group.Key, out var member);
                return new CollaboratorProductivity(
                    group.Key,
                    member?.FullName,
                    member?.Email,
                    member?.Role,
                    group.Count(item => item.Kind == WorkKind.CleaningTask),
                    group.Count(item => item.Kind == WorkKind.Incident),
                    group.Count(item => item.Kind == WorkKind.GuestRequest),
                    AverageMinutes(group.Select(item => item.WorkTime)));
            })
            .OrderByDescending(collaborator => collaborator.TotalCompleted)
            .ThenBy(collaborator => collaborator.UserId)
            .ToList();

        var areas = Enum.GetValues<WorkArea>()
            .Select(area =>
            {
                var done = work.Where(item => item.Area == area).ToList();
                var people = done.Where(item => item.UserId is not null).Select(item => item.UserId).Distinct().Count();
                return new AreaProductivity(
                    area,
                    done.Count,
                    people,
                    people == 0 ? 0 : Round((double)done.Count / people),
                    AverageMinutes(done.Select(item => item.WorkTime)));
            })
            .ToList();

        return new ProductivityReport(hotelId, from, to, work.Count, collaborators, areas);
    }

    /// <summary>
    ///     The average response, work and total times of the cleaning tasks, the incidents (only corrective ones) and the
    ///     guest requests (also by area).
    /// </summary>
    public static ServiceTimesReport ServiceTimes(int hotelId, DateTimeOffset from, DateTimeOffset to,
        IReadOnlyCollection<FinishedWork> work)
    {
        var measured = work.Where(item => item.CountsForServiceTimes).ToList();
        var requests = measured.Where(item => item.Kind == WorkKind.GuestRequest).ToList();

        return new ServiceTimesReport(
            hotelId,
            from,
            to,
            Indicator(measured.Where(item => item.Kind == WorkKind.CleaningTask)),
            Indicator(measured.Where(item => item.Kind == WorkKind.Incident)),
            Indicator(requests),
            Enum.GetValues<WorkArea>()
                .Select(area => new AreaServiceTime(area, Indicator(requests.Where(item => item.Area == area))))
                .ToList());
    }

    private static ServiceTimeIndicator Indicator(IEnumerable<FinishedWork> work)
    {
        var items = work.ToList();
        return new ServiceTimeIndicator(
            items.Count,
            AverageMinutes(items.Select(item => item.ResponseTime)),
            AverageMinutes(items.Select(item => item.WorkTime)),
            AverageMinutes(items.Select(item => (TimeSpan?)item.TotalTime)),
            items.Count == 0 ? null : Round(items.Max(item => item.TotalTime).TotalMinutes));
    }

    private static double? AverageMinutes(IEnumerable<TimeSpan?> spans)
    {
        var minutes = spans.Where(span => span is not null).Select(span => span!.Value.TotalMinutes).ToList();
        return minutes.Count == 0 ? null : Round(minutes.Average());
    }

    private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
