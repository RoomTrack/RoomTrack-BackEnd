using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.GuestRequests.Domain.Model.Exceptions;

/// <summary>Stable error codes of the guest requests context.</summary>
public static class GuestRequestErrorCodes
{
    public const string TitleLength = "guest_request.title_length";
    public const string DescriptionLength = "guest_request.description_length";
    public const string NoteLength = "guest_request.note_length";
    public const string NoCurrentStay = "guest_request.no_current_stay";
    public const string Closed = "guest_request.closed";
    public const string NotAllowed = "guest_request.not_allowed";
    public const string AssigneeNotOfArea = "guest_request.assignee_not_of_area";
    public const string RoomRequired = "guest_request.room_required";
    public const string AreaRequired = "guest_request.area_required";
}

/// <summary>A guest without a stay today cannot send requests: there is no room to attend.</summary>
public class NoCurrentStayException()
    : BusinessRuleViolationException(GuestRequestErrorCodes.NoCurrentStay,
        "You can send requests during your stay (after your booking is confirmed for today).");

public class GuestRequestClosedException(int requestId, GuestRequestStatus status)
    : BusinessRuleViolationException(GuestRequestErrorCodes.Closed,
        $"Request {requestId} is {status.ToString().ToLowerInvariant()}: it can no longer change.");

public class GuestRequestNotAllowedException(string operation)
    : OperationNotAllowedException(GuestRequestErrorCodes.NotAllowed, $"You cannot {operation} this request.");
