using BackendAwRoomTrack.API.Housekeeping.Domain.Model.ValueObjects;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions;

namespace BackendAwRoomTrack.API.Housekeeping.Domain.Model.Exceptions;

/// <summary>Stable error codes of the Housekeeping context.</summary>
public static class CleaningTaskErrorCodes
{
    public const string InvalidStatusTransition = "cleaning_task.invalid_status_transition";
    public const string Closed = "cleaning_task.closed";
    public const string NotAssigned = "cleaning_task.not_assigned";
    public const string NotAssignee = "cleaning_task.not_assignee";
    public const string AssignNotAllowed = "cleaning_task.assign_not_allowed";
    public const string ManageNotAllowed = "cleaning_task.manage_not_allowed";
    public const string AssigneeNotHousekeeping = "cleaning_task.assignee_not_housekeeping";
    public const string NoHousekeeperAvailable = "cleaning_task.no_housekeeper_available";
    public const string RoomAlreadyHasOpenTask = "cleaning_task.room_already_has_open_task";
    public const string NotesLength = "cleaning_task.notes_length";
    public const string CancellationReasonLength = "cleaning_task.cancellation_reason_length";
}

/// <summary>The task is not in a status that allows the operation.</summary>
public class InvalidCleaningTaskStatusTransitionException(int taskId, CleaningTaskStatus from, string operation)
    : BusinessRuleViolationException(CleaningTaskErrorCodes.InvalidStatusTransition,
        $"Cleaning task {taskId} cannot be {operation} while it is {from}.");

/// <summary>A completed or cancelled task is closed: it cannot be assigned, re-prioritized or worked on.</summary>
public class CleaningTaskClosedException(int taskId, CleaningTaskStatus status)
    : BusinessRuleViolationException(CleaningTaskErrorCodes.Closed, $"Cleaning task {taskId} is already {status}.");

/// <summary>The task has nobody assigned: it must be assigned before work starts.</summary>
public class CleaningTaskNotAssignedException(int taskId)
    : BusinessRuleViolationException(CleaningTaskErrorCodes.NotAssigned,
        $"Cleaning task {taskId} has no assignee. Assign it to a housekeeping staff member first.");

/// <summary>Only the assignee (or an administrator) works on a cleaning task.</summary>
public class NotCleaningTaskAssigneeException(int taskId)
    : OperationNotAllowedException(CleaningTaskErrorCodes.NotAssignee,
        $"Cleaning task {taskId} is assigned to someone else. Only its assignee or an administrator can work on it.");

/// <summary>Only the front desk or an administrator creates, re-prioritizes and cancels cleaning tasks.</summary>
public class CleaningTaskManageNotAllowedException(string operation)
    : OperationNotAllowedException(CleaningTaskErrorCodes.ManageNotAllowed,
        $"Only reception or an administrator can {operation} a cleaning task.");

/// <summary>A room has at most one open (Pending or InProgress) cleaning task.</summary>
public class RoomAlreadyHasOpenCleaningTaskException(int roomId, int openTaskId)
    : BusinessRuleViolationException(CleaningTaskErrorCodes.RoomAlreadyHasOpenTask,
        $"Room {roomId} already has the open cleaning task {openTaskId}. Assign or reprioritize that one instead.");

/// <summary>An automatic assignment found no active housekeeping staff member in the hotel.</summary>
public class NoHousekeeperAvailableException(int hotelId)
    : BusinessRuleViolationException(CleaningTaskErrorCodes.NoHousekeeperAvailable,
        $"Hotel {hotelId} has no active housekeeping staff member to assign the task to.");
