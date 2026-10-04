namespace BackendAwRoomTrack.API.Marketing.Domain.Model.ValueObjects;

/// <summary>Kind of accommodation the visitor runs (segments).</summary>
public enum AccommodationType { Boutique, Alternative, Chain }

/// <summary>Size of the property.</summary>
public enum RoomsRange { From1To10, From11To30, From31To60, MoreThan60 }

/// <summary>How the visitor heard about RoomTrack.</summary>
public enum ReferralSource { Search, Social, Referral, Event, Other }

/// <summary>Landing profile the visitor was browsing.</summary>
public enum VisitorProfile { Admin, Guest }

/// <summary>Lifecycle of a demo request.</summary>
public enum DemoRequestStatus
{
    /// <summary>Received and confirmed to the visitor; waiting for the sales team.</summary>
    Received,
    /// <summary>The automatic follow-up reminder was sent.</summary>
    FollowedUp
}
