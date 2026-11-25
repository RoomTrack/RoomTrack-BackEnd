namespace RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;

/// <summary>
///     What each RoomTrack microservice owns, for the description of its own OpenAPI document (each service documents
///     its bounded contexts only, not the whole product).
/// </summary>
public static class ServiceDescriptions
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Identity"] = """
            Identity service of RoomTrack: accounts and access. Sign-in with MFA (TOTP), refresh tokens and sessions,
            users and roles of the hotel staff, password policy and recovery, push device registration and the audit log
            of security events. It is the issuer of the JWT that the other services validate.
            """,
        ["Accommodations"] = """
            Accommodations service of RoomTrack: hotel operations around the rooms. Hotels, rooms and room types and the
            room status board; housekeeping (cleaning tasks, automatic assignment and the cleaning checklist per room
            type, US-05/06/07); incidents and preventive maintenance (US-08/09/10/30); guest requests (US-11/19);
            conversations between staff and guests (US-12/20); shift handovers (US-32); hotel images and the IoT emulator.
            """,
        ["Bookings"] = """
            Bookings service of RoomTrack: the stay of a guest. Room availability and bookings, digital and assisted
            check-in and check-out (US-13/14/27), the payment that confirms a booking (US-15), the guest folio with
            additional charges, guarantee deposit and split payments (US-16) and the receipts and invoices of every
            payment (US-17).
            """,
        ["Profiles"] = """
            Profiles service of RoomTrack: people. Guest profiles and staff profiles (employee code, position, hotel
            assignments), the staff shifts with coverage check and shift swaps (US-31), and the demo requests of the
            landing page.
            """,
        ["Analytics"] = """
            Analytics service of RoomTrack: read model fed by the integration events of the other services (bookings,
            payments, rooms) to compute occupancy, revenue and performance metrics of the hotels.
            """
    };

    /// <summary>The description of the service, or a generic one for an unknown title.</summary>
    public static string For(string serviceTitle) =>
        Descriptions.TryGetValue(serviceTitle, out var description)
            ? description.Trim()
            : $"{serviceTitle} service of RoomTrack, the hotel management platform.";
}
