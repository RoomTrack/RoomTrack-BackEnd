using RoomTrack.Accommodations.Domain.Model.Events;
using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Accommodations.Domain.Model.Commands;
using RoomTrack.Accommodations.Domain.Model.Entities;
using RoomTrack.Accommodations.Domain.Model.Exceptions;
using RoomTrack.Accommodations.Domain.Model.ValueObjects;

namespace RoomTrack.Accommodations.Domain.Model.Aggregates;

/// <summary>
/// Represents a room aggregate in the accommodations domain: its price and its operational status.
/// </summary>
public partial class Room : IHasDomainEvents
{
    private readonly List<IEvent> _domainEvents = [];

    public IReadOnlyCollection<IEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Initializes a new instance of the <see cref="Room"/> class with default values.
    /// </summary>
    public Room()
    {
        Description = string.Empty;
        Amenities = new List<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Room"/> class from a create command.
    /// </summary>
    /// <param name="command">The command containing room creation data.</param>
    public Room(CreateRoomCommand command) : this()
    {
        Number = RoomNumber.Normalize(command.Number);
        EnsureValidPrice(command.Price);

        RoomTypeId = command.RoomTypeId;
        // NUEVOS CAMPOS
        HotelId = command.HotelId;
        Price = command.Price;
        // -------------
        Description = command.Description;
        Amenities = command.Amenities;
        Status = RoomStatus.Available;
        StatusChangedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     The number staff and guests use for the room (e.g. "101", "2B"), unique within its hotel. It is what
    ///     the room map shows.
    /// </summary>
    public string Number { get; private set; } = string.Empty;

    /// <summary>Renumbers the room (uniqueness in the hotel is checked by the application service).</summary>
    public void Renumber(string number) => Number = RoomNumber.Normalize(number);

    /// <summary>A room is sold for a positive price per night.</summary>
    private static void EnsureValidPrice(decimal price)
    {
        if (price <= 0)
            throw new InvalidFieldException("price", AccommodationErrorCodes.RoomPriceOutOfRange, "The price per night must be greater than 0.");
        if (price > RoomNumber.MaxPrice)
            throw new InvalidFieldException("price", AccommodationErrorCodes.RoomPriceOutOfRange, $"The price per night cannot exceed {RoomNumber.MaxPrice}.");
    }

    /// <summary>Operational status. New rooms are Available.</summary>
    public RoomStatus Status { get; private set; } = RoomStatus.Available;

    /// <summary>Since when the room has its current status (map and maintenance alert).</summary>
    public DateTimeOffset StatusChangedAt { get; private set; }

    /// <summary>When the overdue-maintenance alert of the current maintenance period was sent.</summary>
    public DateTimeOffset? MaintenanceAlertSentAt { get; private set; }

    /// <summary>A room under maintenance is never offered for booking.</summary>
    public bool IsOfferedForBooking => Status != RoomStatus.Maintenance;

    /// <summary>
    ///     An open incident that blocks the room keeps it out of service: it cannot become Available until every
    ///     blocking incident is resolved (the Incidents context holds and releases it).
    /// </summary>
    public bool IsBlockedByIncident { get; private set; }

    /// <summary>The statuses the room can change to now (Available is withheld while an incident blocks it).</summary>
    public IReadOnlyList<RoomStatus> AllowedNextStatuses =>
        RoomStatusTransitions.AllowedFrom(Status).Where(status => !(IsBlockedByIncident && status == RoomStatus.Available)).ToList();

    /// <summary>
    ///     Moves the room to <paramref name="newStatus"/> if the transition is valid and returns the history line to
    ///     store with it, or null when the status does not change.
    /// </summary>
    /// <exception cref="InvalidRoomStatusTransitionException">The transition is not allowed.</exception>
    public RoomStatusChange? ChangeStatus(RoomStatus newStatus, RoomStatusChangeOrigin origin, int? changedByUserId,
        string? changedByEmail, DateTimeOffset now)
    {
        if (!RoomStatusTransitions.IsAllowed(Status, newStatus))
            throw new InvalidRoomStatusTransitionException(Id, Status, newStatus);
        if (newStatus == Status) return null;
        if (newStatus == RoomStatus.Available && IsBlockedByIncident)
            throw new RoomBlockedByIncidentException(Id);

        var change = new RoomStatusChange(Id, HotelId, Status, newStatus, origin, changedByUserId, changedByEmail, now);
        _domainEvents.Add(new RoomStatusChangedEvent(Id, HotelId, Status, newStatus, origin, changedByUserId, now));
        Status = newStatus;
        StatusChangedAt = now;
        MaintenanceAlertSentAt = null;
        return change;
    }

    /// <summary>The guest of a completed check-in moves in. Only an Available room can be occupied.</summary>
    public RoomStatusChange OccupyForCheckIn(int? guestUserId, string? guestEmail, DateTimeOffset now)
    {
        if (Status != RoomStatus.Available)
            throw new RoomNotReadyForCheckInException(Id, Status);
        return ChangeStatus(RoomStatus.Occupied, RoomStatusChangeOrigin.CheckIn, guestUserId, guestEmail, now)!;
    }

    /// <summary>
    ///     The guest checked out: an Occupied room goes to Cleaning ("pendiente de limpieza"), which gives it its
    ///     cleaning task. Returns the history line, or null when the room was no longer Occupied (e.g. a blocking
    ///     incident took it to Maintenance during the stay: it is cleaned when the incident is resolved).
    /// </summary>
    public RoomStatusChange? VacateAfterCheckOut(int? userId, string? email, DateTimeOffset now) =>
        Status == RoomStatus.Occupied
            ? ChangeStatus(RoomStatus.Cleaning, RoomStatusChangeOrigin.CheckOut, userId, email, now)
            : null;

    /// <summary>
    ///     A blocking incident was reported: the room is taken out of service (Maintenance) until it is released.
    ///     Returns the history line of the status change, or null when the room was already under maintenance.
    /// </summary>
    public RoomStatusChange? HoldForIncident(int? reportedByUserId, string? reportedByEmail, DateTimeOffset now)
    {
        IsBlockedByIncident = true;
        return ChangeStatus(RoomStatus.Maintenance, RoomStatusChangeOrigin.Incident, reportedByUserId, reportedByEmail, now);
    }

    /// <summary>
    ///     The last blocking incident of the room was resolved: the room goes from Maintenance to Cleaning, so
    ///     housekeeping prepares it before it is Available again. Returns the history line, or null when the room had
    ///     already left Maintenance.
    /// </summary>
    public RoomStatusChange? ReleaseFromIncident(int? resolvedByUserId, string? resolvedByEmail, DateTimeOffset now)
    {
        IsBlockedByIncident = false;
        return Status == RoomStatus.Maintenance
            ? ChangeStatus(RoomStatus.Cleaning, RoomStatusChangeOrigin.Incident, resolvedByUserId, resolvedByEmail, now)
            : null;
    }

    /// <summary>
    ///     Housekeeping starts cleaning the room (its cleaning task is started): the room goes to Cleaning. Returns the
    ///     history line, or null when it was already being cleaned.
    /// </summary>
    /// <exception cref="RoomBlockedByIncidentException">An open incident keeps the room out of service.</exception>
    /// <exception cref="RoomOccupiedException">A guest is still staying in it.</exception>
    public RoomStatusChange? BeginCleaning(int? userId, string? email, DateTimeOffset now)
    {
        if (IsBlockedByIncident) throw new RoomBlockedByIncidentException(Id);
        if (Status == RoomStatus.Occupied) throw new RoomOccupiedException(Id);
        return ChangeStatus(RoomStatus.Cleaning, RoomStatusChangeOrigin.Housekeeping, userId, email, now);
    }

    /// <summary>
    ///     Housekeeping finished cleaning the room: it becomes Available, so reception can assign it to a new guest.
    ///     Returns the history line, or null when it was already Available.
    /// </summary>
    /// <exception cref="RoomBlockedByIncidentException">An open incident keeps the room out of service.</exception>
    public RoomStatusChange? FinishCleaning(int? userId, string? email, DateTimeOffset now) =>
        ChangeStatus(RoomStatus.Available, RoomStatusChangeOrigin.Housekeeping, userId, email, now);

    /// <summary>
    ///     The room has been under maintenance for at least <paramref name="threshold"/> and no alert
    ///     was sent for this maintenance period yet.
    /// </summary>
    public bool IsMaintenanceAlertDue(DateTimeOffset now, TimeSpan threshold) =>
        Status == RoomStatus.Maintenance && now - StatusChangedAt >= threshold && MaintenanceAlertSentAt is null;

    /// <summary>Records the overdue-maintenance alert (once per maintenance period).</summary>
    public bool RaiseMaintenanceAlert(DateTimeOffset now, TimeSpan threshold)
    {
        if (!IsMaintenanceAlertDue(now, threshold)) return false;
        MaintenanceAlertSentAt = now;
        _domainEvents.Add(new RoomMaintenanceOverdueEvent(Id, HotelId, StatusChangedAt, now));
        return true;
    }
    
    /// <summary>
    /// Updates the mutable information of the room aggregate.
    /// This method enforces business invariants during updates.
    /// </summary>
    /// <param name="roomTypeId">The new room type identifier.</param>
    /// <param name="price">The new price per night.</param>
    /// <param name="description">The new description.</param>
    /// <param name="amenities">The new list of amenities.</param>
    public void UpdateInformation(int roomTypeId, decimal price, string description, List<string> amenities)
    {
        EnsureValidPrice(price);

        RoomTypeId = roomTypeId;
        Price = price;
        Description = description;
        Amenities = amenities;
    }

    /// <summary>
    /// The unique identifier of the room.
    /// </summary>
    public int Id { get; }
    /// <summary>
    /// The identifier of the room type.
    /// </summary>
    public int RoomTypeId { get; private set; }
    
    // NUEVOS CAMPOS
    /// <summary>
    /// The identifier of the hotel this room belongs to.
    /// </summary>
    public int HotelId { get; private set; } // FK al Hotel
    /// <summary>
    /// The price per night for the room.
    /// </summary>
    public decimal Price { get; private set; } // Precio por noche
    // -------------
    
    /// <summary>
    /// A description of the room.
    /// </summary>
    public string Description { get; private set; }
    /// <summary>
    /// The list of amenities provided by the room.
    /// </summary>
    public List<string> Amenities { get; private set; }

    // Navigation Properties
    /// <summary>
    /// The room type associated with this room.
    /// </summary>
    public virtual RoomType RoomType { get; private set; } = null!;
    /// <summary>
    /// The hotel this room belongs to.
    /// </summary>
    public virtual Hotel Hotel { get; private set; } = null!;
}