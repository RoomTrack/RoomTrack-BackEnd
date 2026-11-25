using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Shifts.Domain;

/// <summary>The part of the day a shift covers.</summary>
public enum ShiftPeriod
{
    /// <summary>07:00-15:00.</summary>
    Morning,
    /// <summary>15:00-23:00.</summary>
    Afternoon,
    /// <summary>23:00-07:00.</summary>
    Night
}

/// <summary>The operational area a shift staffs.</summary>
public enum ShiftArea
{
    FrontDesk,
    Housekeeping,
    Maintenance
}

/// <summary>Pending (waiting for an administrator), Approved (the calendar was updated) or Rejected.</summary>
public enum ShiftSwapStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>Stable error codes of the shifts (US-31).</summary>
public static class ShiftErrorCodes
{
    public const string MinimumStaffRange = "shift.minimum_staff_range";
    public const string DateInPast = "shift.date_in_past";
    public const string AlreadyExists = "shift.already_exists";
    public const string AlreadyAssigned = "shift.already_assigned";
    public const string DoubleBooked = "shift.double_booked";
    public const string NotStaffOfHotel = "shift.not_staff_of_hotel";
    public const string NotAssigned = "shift.not_assigned";
    public const string SwapNotPending = "shift_swap.not_pending";
    public const string SwapSameShift = "shift_swap.same_shift";
    public const string SwapOtherHotel = "shift_swap.other_hotel";
    public const string SwapReasonLength = "shift_swap.reason_length";
}

/// <summary>
///     A shift of an area of the hotel on a date (US-31): who works it and how many people the area needs. A shift with
///     fewer people than <see cref="MinimumStaff"/> is a coverage gap.
/// </summary>
public class WorkShift
{
    public const int MaxMinimumStaff = 50;

    private readonly List<ShiftAssignee> _assignees = [];

    /// <summary>EF Core constructor.</summary>
    protected WorkShift() { }

    /// <summary>An administrator defines a shift of an area on a date (today or later).</summary>
    public static WorkShift Define(int hotelId, DateOnly date, ShiftPeriod period, ShiftArea area, int minimumStaff,
        int createdByUserId, DateOnly today, DateTimeOffset now)
    {
        if (date < today)
            throw new InvalidFieldException("date", ShiftErrorCodes.DateInPast, "A shift cannot be defined in the past.");
        ValidateMinimum(minimumStaff);
        return new WorkShift
        {
            HotelId = hotelId,
            Date = date,
            Period = period,
            Area = area,
            MinimumStaff = minimumStaff,
            CreatedByUserId = createdByUserId,
            CreatedAt = now
        };
    }

    public int Id { get; private set; }
    public int HotelId { get; private set; }
    public DateOnly Date { get; private set; }
    public ShiftPeriod Period { get; private set; }
    public ShiftArea Area { get; private set; }

    /// <summary>People the area needs in this shift.</summary>
    public int MinimumStaff { get; private set; }

    public int CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who works it.</summary>
    public IReadOnlyList<ShiftAssignee> Assignees => _assignees.AsReadOnly();

    /// <summary>People missing to reach <see cref="MinimumStaff"/> (0: covered).</summary>
    public int CoverageGap => Math.Max(0, MinimumStaff - _assignees.Count);

    public bool IsUnderstaffed => CoverageGap > 0;

    public bool HasAssignee(int userId) => _assignees.Any(assignee => assignee.UserId == userId);

    /// <summary>Assigns a staff member (whose hotel and other shifts the application service checked).</summary>
    public void Assign(int userId, string displayName)
    {
        if (HasAssignee(userId))
            throw new BusinessRuleViolationException(ShiftErrorCodes.AlreadyAssigned,
                $"User {userId} already works the {Period} shift of {Date:yyyy-MM-dd}.");
        _assignees.Add(new ShiftAssignee(userId, displayName));
    }

    /// <summary>Removes a staff member from the shift.</summary>
    public void Unassign(int userId)
    {
        var assignee = _assignees.FirstOrDefault(candidate => candidate.UserId == userId)
                       ?? throw new BusinessRuleViolationException(ShiftErrorCodes.NotAssigned,
                           $"User {userId} does not work the {Period} shift of {Date:yyyy-MM-dd}.");
        _assignees.Remove(assignee);
    }

    /// <summary>Changes how many people the area needs.</summary>
    public void ChangeMinimumStaff(int minimumStaff)
    {
        ValidateMinimum(minimumStaff);
        MinimumStaff = minimumStaff;
    }

    private static void ValidateMinimum(int minimumStaff)
    {
        if (minimumStaff is < 1 or > MaxMinimumStaff)
            throw new InvalidFieldException("minimumStaff", ShiftErrorCodes.MinimumStaffRange,
                $"A shift needs between 1 and {MaxMinimumStaff} people.",
                new Dictionary<string, object?> { ["max"] = MaxMinimumStaff });
    }
}

/// <summary>A staff member working a shift.</summary>
public class ShiftAssignee
{
    public const int MaxDisplayNameLength = 150;

    /// <summary>EF Core constructor.</summary>
    protected ShiftAssignee()
    {
        DisplayName = string.Empty;
    }

    internal ShiftAssignee(int userId, string displayName)
    {
        UserId = userId;
        DisplayName = displayName.Length > MaxDisplayNameLength ? displayName[..MaxDisplayNameLength] : displayName;
    }

    public long Id { get; private set; }
    public int WorkShiftId { get; private set; }
    public int UserId { get; private set; }
    public string DisplayName { get; private set; }
}

/// <summary>
///     A staff member asks to exchange one of their shifts with a colleague's (US-31). When an administrator approves
///     it, both calendars are updated at once.
/// </summary>
public class ShiftSwapRequest
{
    public const int MaxReasonLength = 300;

    /// <summary>EF Core constructor.</summary>
    protected ShiftSwapRequest() { }

    public static ShiftSwapRequest Request(WorkShift requesterShift, int requesterUserId, WorkShift colleagueShift,
        int colleagueUserId, string? reason, DateTimeOffset now)
    {
        if (requesterShift.Id == colleagueShift.Id)
            throw new BusinessRuleViolationException(ShiftErrorCodes.SwapSameShift, "Both people already work the same shift.");
        if (requesterShift.HotelId != colleagueShift.HotelId)
            throw new BusinessRuleViolationException(ShiftErrorCodes.SwapOtherHotel, "Shifts are swapped within the same hotel.");
        if (!requesterShift.HasAssignee(requesterUserId))
            throw new BusinessRuleViolationException(ShiftErrorCodes.NotAssigned, "You do not work the shift you want to give away.");
        if (!colleagueShift.HasAssignee(colleagueUserId))
            throw new BusinessRuleViolationException(ShiftErrorCodes.NotAssigned, "Your colleague does not work that shift.");
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmed is { Length: > MaxReasonLength })
            throw new InvalidFieldException("reason", ShiftErrorCodes.SwapReasonLength,
                $"The reason cannot exceed {MaxReasonLength} characters.");
        return new ShiftSwapRequest
        {
            HotelId = requesterShift.HotelId,
            RequesterUserId = requesterUserId,
            RequesterShiftId = requesterShift.Id,
            ColleagueUserId = colleagueUserId,
            ColleagueShiftId = colleagueShift.Id,
            Reason = trimmed,
            Status = ShiftSwapStatus.Pending,
            RequestedAt = now
        };
    }

    public int Id { get; private set; }
    public int HotelId { get; private set; }
    public int RequesterUserId { get; private set; }
    public int RequesterShiftId { get; private set; }
    public int ColleagueUserId { get; private set; }
    public int ColleagueShiftId { get; private set; }
    public string? Reason { get; private set; }
    public ShiftSwapStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public int? DecidedByUserId { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>
    ///     Approves the swap: the requester takes the colleague's shift and the colleague the requester's, so the
    ///     calendar of both is updated.
    /// </summary>
    public void Approve(WorkShift requesterShift, WorkShift colleagueShift, int administratorUserId, DateTimeOffset now)
    {
        EnsurePending();
        var requester = requesterShift.Assignees.FirstOrDefault(assignee => assignee.UserId == RequesterUserId)
                        ?? throw new BusinessRuleViolationException(ShiftErrorCodes.NotAssigned,
                            "The requester no longer works the shift to give away.");
        var colleague = colleagueShift.Assignees.FirstOrDefault(assignee => assignee.UserId == ColleagueUserId)
                        ?? throw new BusinessRuleViolationException(ShiftErrorCodes.NotAssigned,
                            "The colleague no longer works the shift to exchange.");
        requesterShift.Unassign(RequesterUserId);
        colleagueShift.Unassign(ColleagueUserId);
        requesterShift.Assign(ColleagueUserId, colleague.DisplayName);
        colleagueShift.Assign(RequesterUserId, requester.DisplayName);
        Decide(ShiftSwapStatus.Approved, administratorUserId, now);
    }

    public void Reject(int administratorUserId, DateTimeOffset now)
    {
        EnsurePending();
        Decide(ShiftSwapStatus.Rejected, administratorUserId, now);
    }

    private void Decide(ShiftSwapStatus status, int administratorUserId, DateTimeOffset now)
    {
        Status = status;
        DecidedByUserId = administratorUserId;
        DecidedAt = now;
    }

    private void EnsurePending()
    {
        if (Status != ShiftSwapStatus.Pending)
            throw new BusinessRuleViolationException(ShiftErrorCodes.SwapNotPending, $"The shift swap was already {Status.ToString().ToLowerInvariant()}.");
    }
}

/// <summary>A shift with fewer people than it needs (US-31).</summary>
/// <param name="Shift">The shift.</param>
/// <param name="Missing">People missing.</param>
/// <param name="IsCritical">The area is critical (front desk and maintenance always need someone): highlighted first.</param>
public record CoverageGap(WorkShift Shift, int Missing, bool IsCritical);

public interface IWorkShiftRepository
{
    Task AddAsync(WorkShift shift);

    Task<WorkShift?> FindByIdAsync(int id);

    Task<WorkShift?> FindAsync(int hotelId, DateOnly date, ShiftPeriod period, ShiftArea area);

    /// <summary>The shifts of the hotel between two dates (inclusive), by date and period.</summary>
    Task<IReadOnlyList<WorkShift>> ListAsync(int hotelId, DateOnly from, DateOnly to, ShiftArea? area);

    /// <summary>The shifts a person works between two dates (any hotel).</summary>
    Task<IReadOnlyList<WorkShift>> ListOfUserAsync(int userId, DateOnly from, DateOnly to);
}

public interface IShiftSwapRequestRepository
{
    Task AddAsync(ShiftSwapRequest request);

    Task<ShiftSwapRequest?> FindByIdAsync(int id);

    Task<IReadOnlyList<ShiftSwapRequest>> ListAsync(int hotelId, ShiftSwapStatus? status, int? userId);
}
