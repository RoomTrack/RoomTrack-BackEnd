using RoomTrack.IAM.Domain.Model.Constants;

namespace RoomTrack.GuestRequests.Domain.Model.ValueObjects;

/// <summary>The operational area that attends a request.</summary>
public enum ServiceArea
{
    /// <summary>The front desk: questions, documents, keys... Where a guest's request arrives first (US-19).</summary>
    Reception,
    /// <summary>Towels, cleaning, amenities, bedding.</summary>
    Housekeeping,
    /// <summary>Something broken or not working.</summary>
    Maintenance
}

public static class ServiceAreas
{
    /// <summary>The IAM role of the staff of an area (who is notified of its requests and works on them).</summary>
    public static string RoleOf(ServiceArea area) => area switch
    {
        ServiceArea.Housekeeping => UserRoles.Housekeeping,
        ServiceArea.Maintenance => UserRoles.Maintenance,
        _ => UserRoles.Reception
    };

    /// <summary>The area of a staff role, null for roles that are not an operational area (administrators, guests).</summary>
    public static ServiceArea? OfRole(string role) => role switch
    {
        UserRoles.Housekeeping => ServiceArea.Housekeeping,
        UserRoles.Maintenance => ServiceArea.Maintenance,
        UserRoles.Reception => ServiceArea.Reception,
        _ => null
    };
}

/// <summary>Pending (waiting for its area) → InProgress → Resolved; or Cancelled while it is open.</summary>
public enum GuestRequestStatus
{
    Pending,
    InProgress,
    Resolved,
    Cancelled
}

/// <summary>What one line of the history of a request records.</summary>
public enum GuestRequestHistoryAction
{
    Created,
    Routed,
    Assigned,
    Started,
    Resolved,
    Cancelled
}

/// <summary>Who acts on a request.</summary>
/// <param name="UserId">IAM user id.</param>
/// <param name="Email">Their e-mail (kept in the history).</param>
/// <param name="Role">Their IAM role.</param>
public sealed record GuestRequestActor(int UserId, string? Email, string Role)
{
    public bool IsGuest => Role == UserRoles.Guest;

    public bool IsAdministrator => Role is UserRoles.Admin or UserRoles.ChainAdmin;

    /// <summary>The front desk (and the administrators) register, route and assign the requests of the hotel (US-11).</summary>
    public bool CanManage => Role is UserRoles.Reception || IsAdministrator;

    /// <summary>The area this staff member works in, if any.</summary>
    public ServiceArea? Area => ServiceAreas.OfRole(Role);
}
