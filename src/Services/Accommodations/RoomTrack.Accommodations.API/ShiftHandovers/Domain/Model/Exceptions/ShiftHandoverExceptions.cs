namespace RoomTrack.ShiftHandovers.Domain.Model.Exceptions;

/// <summary>Stable error codes of the ShiftHandovers context.</summary>
public static class ShiftHandoverErrorCodes
{
    public const string NotesLength = "shift_handover.notes_length";
    public const string Empty = "shift_handover.empty";
    public const string OwnHandover = "shift_handover.own_handover";
    public const string AreaRequired = "shift_handover.area_required";
}
