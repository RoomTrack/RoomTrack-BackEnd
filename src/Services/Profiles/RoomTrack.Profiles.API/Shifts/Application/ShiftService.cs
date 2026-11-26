using RoomTrack.Profiles.Domain.Model.Enums;
using RoomTrack.Profiles.Domain.Model.ValueObjects;
using RoomTrack.Profiles.Domain.Repositories;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Repositories;
using RoomTrack.Shifts.Domain;

namespace RoomTrack.Shifts.Application;

/// <summary>
///     Use cases of the staff shifts (US-31): define the shifts of each area, assign the staff of the hotel (whose
///     profile and hotel assignment live in the Profiles context of this service), detect coverage gaps and swap shifts
///     between colleagues with the approval of an administrator.
/// </summary>
public class ShiftService(
    IWorkShiftRepository shiftRepository,
    IShiftSwapRequestRepository swapRepository,
    IStaffProfileRepository staffRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private static readonly TimeZoneInfo HotelZone = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

    /// <summary>The hotels' current date.</summary>
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), HotelZone).DateTime);

    /// <summary>Defines a shift and assigns its first staff members (US-31 scenario 1).</summary>
    public async Task<WorkShift> DefineAsync(int hotelId, DateOnly date, ShiftPeriod period, ShiftArea area, int minimumStaff,
        IReadOnlyCollection<int> assigneeUserIds, int administratorUserId)
    {
        if (await shiftRepository.FindAsync(hotelId, date, period, area) is { } existing)
            throw new BusinessRuleViolationException(ShiftErrorCodes.AlreadyExists,
                $"The {area} area already has its {period} shift of {date:yyyy-MM-dd} (shift {existing.Id}).");
        var shift = WorkShift.Define(hotelId, date, period, area, minimumStaff, administratorUserId, Today, timeProvider.GetUtcNow());
        foreach (var userId in assigneeUserIds.Distinct())
            shift.Assign(userId, await StaffNameAsync(hotelId, userId, date, period));
        await shiftRepository.AddAsync(shift);
        await unitOfWork.CompleteAsync();
        return shift;
    }

    /// <returns>The shift, or null when it does not exist.</returns>
    public async Task<WorkShift?> AssignAsync(int shiftId, int userId)
    {
        var shift = await shiftRepository.FindByIdAsync(shiftId);
        if (shift is null) return null;
        shift.Assign(userId, await StaffNameAsync(shift.HotelId, userId, shift.Date, shift.Period));
        await unitOfWork.CompleteAsync();
        return shift;
    }

    /// <returns>The shift, or null when it does not exist.</returns>
    public async Task<WorkShift?> UnassignAsync(int shiftId, int userId)
    {
        var shift = await shiftRepository.FindByIdAsync(shiftId);
        if (shift is null) return null;
        shift.Unassign(userId);
        await unitOfWork.CompleteAsync();
        return shift;
    }

    public Task<WorkShift?> FindAsync(int shiftId) => shiftRepository.FindByIdAsync(shiftId);

    public Task<IReadOnlyList<WorkShift>> ScheduleAsync(int hotelId, DateOnly from, DateOnly to, ShiftArea? area) =>
        shiftRepository.ListAsync(hotelId, from, to, area);

    /// <summary>The shifts of a staff member: what they see on their panel (US-31 scenario 1).</summary>
    public Task<IReadOnlyList<WorkShift>> ScheduleOfAsync(int userId, DateOnly from, DateOnly to) =>
        shiftRepository.ListOfUserAsync(userId, from, to);

    /// <summary>
    ///     US-31 scenario 2: the shifts with fewer people than they need, critical areas first (the front desk and
    ///     maintenance cannot be left alone), then by date.
    /// </summary>
    public async Task<IReadOnlyList<CoverageGap>> CoverageGapsAsync(int hotelId, DateOnly from, DateOnly to)
    {
        var shifts = await shiftRepository.ListAsync(hotelId, from, to, null);
        return shifts.Where(shift => shift.IsUnderstaffed)
            .Select(shift => new CoverageGap(shift, shift.CoverageGap,
                IsCritical: shift.Area is ShiftArea.FrontDesk or ShiftArea.Maintenance || shift.Assignees.Count == 0))
            .OrderByDescending(gap => gap.IsCritical).ThenBy(gap => gap.Shift.Date).ThenBy(gap => gap.Shift.Period)
            .ToList();
    }

    /// <summary>US-31 scenario 3: a staff member asks to exchange their shift with a colleague's.</summary>
    public async Task<ShiftSwapRequest> RequestSwapAsync(int requesterUserId, int requesterShiftId, int colleagueUserId,
        int colleagueShiftId, string? reason)
    {
        var mine = await shiftRepository.FindByIdAsync(requesterShiftId) ?? throw new EntityNotFoundException("Shift", requesterShiftId);
        var theirs = await shiftRepository.FindByIdAsync(colleagueShiftId) ?? throw new EntityNotFoundException("Shift", colleagueShiftId);
        var request = ShiftSwapRequest.Request(mine, requesterUserId, theirs, colleagueUserId, reason, timeProvider.GetUtcNow());
        await swapRepository.AddAsync(request);
        await unitOfWork.CompleteAsync();
        return request;
    }

    /// <summary>An administrator approves a swap: both calendars are updated in the same transaction.</summary>
    /// <returns>The request, or null when it does not exist.</returns>
    public async Task<ShiftSwapRequest?> ApproveSwapAsync(int requestId, int administratorUserId)
    {
        var request = await swapRepository.FindByIdAsync(requestId);
        if (request is null) return null;
        var mine = await shiftRepository.FindByIdAsync(request.RequesterShiftId)
                   ?? throw new EntityNotFoundException("Shift", request.RequesterShiftId);
        var theirs = await shiftRepository.FindByIdAsync(request.ColleagueShiftId)
                     ?? throw new EntityNotFoundException("Shift", request.ColleagueShiftId);
        await EnsureNotDoubleBookedAsync(request.ColleagueUserId, mine, ignoring: theirs);
        await EnsureNotDoubleBookedAsync(request.RequesterUserId, theirs, ignoring: mine);
        request.Approve(mine, theirs, administratorUserId, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return request;
    }

    /// <returns>The request, or null when it does not exist.</returns>
    public async Task<ShiftSwapRequest?> RejectSwapAsync(int requestId, int administratorUserId)
    {
        var request = await swapRepository.FindByIdAsync(requestId);
        if (request is null) return null;
        request.Reject(administratorUserId, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return request;
    }

    public Task<ShiftSwapRequest?> FindSwapAsync(int requestId) => swapRepository.FindByIdAsync(requestId);

    public Task<IReadOnlyList<ShiftSwapRequest>> SwapsAsync(int hotelId, ShiftSwapStatus? status, int? userId) =>
        swapRepository.ListAsync(hotelId, status, userId);

    /// <summary>The name of a staff member of the hotel free at that date and period, or a 400.</summary>
    private async Task<string> StaffNameAsync(int hotelId, int userId, DateOnly date, ShiftPeriod period)
    {
        var profile = await staffRepository.FindByUserIdAsync(new UserId(userId));
        var worksAtHotel = profile is { Status: ProfileStatus.Active }
                           && profile.Assignments.Any(assignment => assignment.Scope == ScopeLevel.Hotel
                                                                    && assignment.TargetId.Value == hotelId
                                                                    && assignment.IsCurrentOrScheduled());
        if (!worksAtHotel)
            throw new InvalidFieldException("userId", ShiftErrorCodes.NotStaffOfHotel,
                $"User {userId} is not an active staff member of hotel {hotelId}.");
        if ((await shiftRepository.ListOfUserAsync(userId, date, date)).Any(shift => shift.Period == period))
            throw new BusinessRuleViolationException(ShiftErrorCodes.DoubleBooked,
                $"User {userId} already works another {period} shift on {date:yyyy-MM-dd}.");
        return profile!.Name.FullName;
    }

    private async Task EnsureNotDoubleBookedAsync(int userId, WorkShift target, WorkShift ignoring)
    {
        if ((await shiftRepository.ListOfUserAsync(userId, target.Date, target.Date))
            .Any(shift => shift.Id != ignoring.Id && shift.Id != target.Id && shift.Period == target.Period))
            throw new BusinessRuleViolationException(ShiftErrorCodes.DoubleBooked,
                $"User {userId} already works another {target.Period} shift on {target.Date:yyyy-MM-dd}.");
    }
}
