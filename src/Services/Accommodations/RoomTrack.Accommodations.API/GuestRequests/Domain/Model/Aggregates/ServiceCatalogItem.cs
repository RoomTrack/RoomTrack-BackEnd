using RoomTrack.GuestRequests.Domain.Model.Exceptions;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.GuestRequests.Domain.Model.Aggregates;

/// <summary>
///     A service a hotel offers to its guests (US-33): what it is, which area attends it, its price and how long it
///     usually takes. The guest picks it from the catalog when sending a request, and the request keeps its name and
///     price as they were at that moment.
/// </summary>
public class ServiceCatalogItem
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 500;
    public const decimal MaxPrice = 99_999.99m;
    public const int MaxEstimatedMinutes = 24 * 60;

    /// <summary>EF Core constructor.</summary>
    protected ServiceCatalogItem()
    {
        Name = string.Empty;
    }

    private ServiceCatalogItem(int hotelId, ServiceCatalogItemDetails details, DateTimeOffset now)
    {
        HotelId = hotelId;
        Name = string.Empty;
        Apply(details);
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>The administrator of the hotel adds a service to its catalog.</summary>
    public static ServiceCatalogItem Create(int hotelId, ServiceCatalogItemDetails details, DateTimeOffset now) =>
        new(hotelId, details, now);

    public int Id { get; private set; }
    public int HotelId { get; private set; }

    /// <summary>What the guest sees, e.g. "Desayuno a la habitación".</summary>
    public string Name { get; private set; }

    public string? Description { get; private set; }

    /// <summary>The area that attends it: the request goes straight to it.</summary>
    public ServiceArea Area { get; private set; }

    /// <summary>Price for the guest (0 when it is included in the stay).</summary>
    public decimal Price { get; private set; }

    /// <summary>How long it usually takes, in minutes (null when the hotel does not say).</summary>
    public int? EstimatedMinutes { get; private set; }

    /// <summary>Guests only see and request the available services.</summary>
    public bool IsAvailable { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsIncluded => Price == 0;

    /// <summary>The administrator changes the service. Requests already sent keep the name and price they had.</summary>
    public void Update(ServiceCatalogItemDetails details, DateTimeOffset now)
    {
        Apply(details);
        UpdatedAt = now;
    }

    /// <summary>A guest can ask for it: it is available.</summary>
    /// <exception cref="ServiceNotAvailableException">The hotel withdrew it from the catalog.</exception>
    public void EnsureAvailable()
    {
        if (!IsAvailable) throw new ServiceNotAvailableException(Id);
    }

    private void Apply(ServiceCatalogItemDetails details)
    {
        var name = details.Name?.Trim() ?? string.Empty;
        if (name.Length is < NameMinLength or > NameMaxLength)
            throw new InvalidFieldException("name", GuestRequestErrorCodes.ServiceNameLength,
                $"The name has {NameMinLength} to {NameMaxLength} characters.",
                new Dictionary<string, object?> { ["minLength"] = NameMinLength, ["maxLength"] = NameMaxLength });

        var description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
        if (description is { Length: > DescriptionMaxLength })
            throw new InvalidFieldException("description", GuestRequestErrorCodes.ServiceDescriptionLength,
                $"The description has at most {DescriptionMaxLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = DescriptionMaxLength });

        if (details.Price is < 0 or > MaxPrice || decimal.Round(details.Price, 2) != details.Price)
            throw new InvalidFieldException("price", GuestRequestErrorCodes.ServicePriceInvalid,
                $"The price goes from 0 (included) to {MaxPrice}, with at most 2 decimals.",
                new Dictionary<string, object?> { ["min"] = 0, ["max"] = MaxPrice });

        if (details.EstimatedMinutes is < 1 or > MaxEstimatedMinutes)
            throw new InvalidFieldException("estimatedMinutes", GuestRequestErrorCodes.ServiceEstimatedMinutesInvalid,
                $"The estimated time goes from 1 to {MaxEstimatedMinutes} minutes.",
                new Dictionary<string, object?> { ["min"] = 1, ["max"] = MaxEstimatedMinutes });

        Name = name;
        Description = description;
        Area = details.Area;
        Price = details.Price;
        EstimatedMinutes = details.EstimatedMinutes;
        IsAvailable = details.IsAvailable;
    }
}

/// <summary>What the administrator says about a service of the catalog.</summary>
public sealed record ServiceCatalogItemDetails(
    string? Name,
    string? Description,
    ServiceArea Area,
    decimal Price,
    int? EstimatedMinutes,
    bool IsAvailable = true);

/// <summary>The service of the catalog a request was made for, as it was when the request was sent.</summary>
public sealed record RequestedService(int ServiceItemId, string Name, decimal Price)
{
    public static RequestedService Of(ServiceCatalogItem item) => new(item.Id, item.Name, item.Price);
}
