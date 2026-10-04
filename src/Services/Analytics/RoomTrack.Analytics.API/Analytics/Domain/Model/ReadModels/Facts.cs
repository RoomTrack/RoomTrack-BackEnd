namespace BackendAwRoomTrack.API.Analytics.Domain.Model.ReadModels;

/// <summary>
///     Base of the read model rows: a local copy of another service's state, kept up to date by its integration
///     events. <see cref="LastEventAt"/> is the instant of the last fact applied, so an older event that arrives late
///     (RabbitMQ does not guarantee the order across retries) never overwrites a newer state.
/// </summary>
public abstract class Fact
{
    public DateTimeOffset LastEventAt { get; protected set; }

    /// <summary>True when the event at <paramref name="occurredOn"/> is newer than the state already stored.</summary>
    public bool IsOlderThan(DateTimeOffset occurredOn) => occurredOn > LastEventAt;
}

/// <summary>A room of a hotel (from the Accommodations service).</summary>
public class RoomFact : Fact
{
    private RoomFact()
    {
    }

    public RoomFact(int roomId) => RoomId = roomId;

    public int RoomId { get; private set; }
    public int HotelId { get; private set; }

    /// <summary>The room was deleted (kept as a tombstone so a late "registered" event does not bring it back).</summary>
    public bool Removed { get; private set; }

    public void Apply(int hotelId, bool removed, DateTimeOffset occurredOn)
    {
        HotelId = hotelId;
        Removed = removed;
        LastEventAt = occurredOn;
    }
}

/// <summary>A booking (from the Bookings service).</summary>
public class BookingFact : Fact
{
    private BookingFact()
    {
    }

    public BookingFact(int bookingId) => BookingId = bookingId;

    public int BookingId { get; private set; }
    public int HotelId { get; private set; }
    public int RoomId { get; private set; }
    public DateTime CheckInDate { get; private set; }
    public DateTime CheckOutDate { get; private set; }

    /// <summary>Pending, Confirmed, Cancelled, CheckedIn or Completed.</summary>
    public string Status { get; private set; } = string.Empty;

    public void Apply(int hotelId, int roomId, DateTime checkIn, DateTime checkOut, string status, DateTimeOffset occurredOn)
    {
        HotelId = hotelId;
        RoomId = roomId;
        CheckInDate = checkIn;
        CheckOutDate = checkOut;
        Status = status;
        LastEventAt = occurredOn;
    }
}

/// <summary>The payment of a booking (from the Bookings service).</summary>
public class PaymentFact : Fact
{
    private PaymentFact()
    {
    }

    public PaymentFact(int paymentId) => PaymentId = paymentId;

    public int PaymentId { get; private set; }
    public int BookingId { get; private set; }
    public int HotelId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime PaymentDate { get; private set; }

    /// <summary>Completed or Refunded.</summary>
    public string Status { get; private set; } = string.Empty;

    public void Apply(int bookingId, int hotelId, decimal amount, DateTime paymentDate, string status, DateTimeOffset occurredOn)
    {
        BookingId = bookingId;
        HotelId = hotelId;
        Amount = amount;
        PaymentDate = paymentDate;
        Status = status;
        LastEventAt = occurredOn;
    }
}
