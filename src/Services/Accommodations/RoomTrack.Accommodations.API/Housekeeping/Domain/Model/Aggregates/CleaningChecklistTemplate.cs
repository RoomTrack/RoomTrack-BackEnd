using RoomTrack.Housekeeping.Domain.Model.Entities;
using RoomTrack.Housekeeping.Domain.Model.Exceptions;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Housekeeping.Domain.Model.Aggregates;

/// <summary>
///     The standardized cleaning protocol of a hotel for one room type (US-07), e.g. the suites get extra steps. A
///     template without room type is the hotel's default for the types it did not configure; a hotel that configured
///     nothing gives its tasks no checklist (<see cref="CleaningChecklistDefaults"/> is only offered as a starting point).
/// </summary>
public class CleaningChecklistTemplate
{
    public const int MaxItems = 30;

    private readonly List<CleaningChecklistTemplateItem> _items = [];

    /// <summary>EF Core constructor.</summary>
    protected CleaningChecklistTemplate() { }

    public CleaningChecklistTemplate(int hotelId, int? roomTypeId)
    {
        HotelId = hotelId;
        RoomTypeId = roomTypeId;
    }

    public int Id { get; private set; }
    public int HotelId { get; private set; }

    /// <summary>The room type (Accommodations catalog); null for the hotel's default protocol.</summary>
    public int? RoomTypeId { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }
    public int? UpdatedByUserId { get; private set; }

    /// <summary>The steps, in order.</summary>
    public IReadOnlyList<CleaningChecklistTemplateItem> Items => _items.OrderBy(item => item.Position).ToList();

    /// <summary>The steps as given to a new cleaning task.</summary>
    public IReadOnlyList<ChecklistItemDefinition> Definitions =>
        Items.Select(item => new ChecklistItemDefinition(item.Label, item.IsRequired)).ToList();

    /// <summary>Replaces the steps of the protocol (tasks already created keep theirs).</summary>
    public void Configure(IReadOnlyList<ChecklistItemDefinition> items, int updatedByUserId, DateTimeOffset now)
    {
        var normalized = Normalize(items);
        _items.Clear();
        _items.AddRange(normalized.Select((item, index) => new CleaningChecklistTemplateItem(index + 1, item.Label, item.IsRequired)));
        UpdatedAt = now;
        UpdatedByUserId = updatedByUserId;
    }

    /// <summary>Validates the steps of a protocol: 1 to <see cref="MaxItems"/>, non-empty and without duplicates.</summary>
    public static IReadOnlyList<ChecklistItemDefinition> Normalize(IReadOnlyList<ChecklistItemDefinition> items)
    {
        if (items.Count is 0 or > MaxItems)
            throw new InvalidFieldException("items", CleaningTaskErrorCodes.ChecklistItemsCount,
                $"A cleaning checklist has between 1 and {MaxItems} items.",
                new Dictionary<string, object?> { ["maxItems"] = MaxItems });

        var normalized = new List<ChecklistItemDefinition>(items.Count);
        foreach (var item in items)
        {
            var label = item.Label?.Trim();
            if (string.IsNullOrEmpty(label) || label.Length > CleaningChecklistItem.MaxLabelLength)
                throw new InvalidFieldException("items", CleaningTaskErrorCodes.ChecklistItemLabel,
                    $"Every checklist item needs a label of up to {CleaningChecklistItem.MaxLabelLength} characters.",
                    new Dictionary<string, object?> { ["maxLength"] = CleaningChecklistItem.MaxLabelLength });
            if (normalized.Any(existing => string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidFieldException("items", CleaningTaskErrorCodes.ChecklistItemDuplicated,
                    $"The checklist item \"{label}\" is repeated.", new Dictionary<string, object?> { ["label"] = label });
            normalized.Add(new ChecklistItemDefinition(label, item.IsRequired));
        }
        return normalized;
    }
}

/// <summary>One step of a <see cref="CleaningChecklistTemplate"/>.</summary>
public class CleaningChecklistTemplateItem
{
    /// <summary>EF Core constructor.</summary>
    protected CleaningChecklistTemplateItem()
    {
        Label = string.Empty;
    }

    internal CleaningChecklistTemplateItem(int position, string label, bool isRequired)
    {
        Position = position;
        Label = label;
        IsRequired = isRequired;
    }

    public long Id { get; private set; }
    public int CleaningChecklistTemplateId { get; private set; }
    public int Position { get; private set; }
    public string Label { get; private set; }
    public bool IsRequired { get; private set; }
}
