namespace RoomTrack.Housekeeping.Domain.Model.Entities;

/// <summary>
///     One step of the cleaning protocol of a task (US-07), copied from the checklist of the room type when the task
///     is created, so later changes to the protocol do not alter tasks already given out.
/// </summary>
public class CleaningChecklistItem
{
    public const int MaxLabelLength = 150;

    /// <summary>EF Core constructor.</summary>
    protected CleaningChecklistItem()
    {
        Label = string.Empty;
    }

    internal CleaningChecklistItem(int position, string label, bool isRequired)
    {
        Position = position;
        Label = label;
        IsRequired = isRequired;
    }

    public long Id { get; private set; }
    public int CleaningTaskId { get; private set; }

    /// <summary>Order of the step in the protocol (1-based).</summary>
    public int Position { get; private set; }

    /// <summary>What to do, e.g. "Change the bed linen".</summary>
    public string Label { get; private set; }

    /// <summary>The task cannot be completed until a required step is done.</summary>
    public bool IsRequired { get; private set; }

    public bool IsDone { get; private set; }
    public DateTimeOffset? DoneAt { get; private set; }
    public int? DoneByUserId { get; private set; }

    /// <returns>False when the item already had that state (nothing changes).</returns>
    internal bool Mark(bool done, int userId, DateTimeOffset now)
    {
        if (IsDone == done) return false;
        IsDone = done;
        DoneAt = done ? now : null;
        DoneByUserId = done ? userId : null;
        return true;
    }
}
