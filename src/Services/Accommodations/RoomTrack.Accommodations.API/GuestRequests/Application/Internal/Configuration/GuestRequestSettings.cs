using System.ComponentModel.DataAnnotations;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;

namespace RoomTrack.GuestRequests.Application.Internal.Configuration;

/// <summary>
///     Settings of the guest requests (section <c>GuestRequests</c>): the attention time each area commits to, which
///     the guest is told when the request is registered (US-19, scenario 2).
/// </summary>
public class GuestRequestSettings
{
    public const string SectionName = "GuestRequests";

    [Range(1, 1440)] public int ReceptionMinutes { get; set; } = 15;
    [Range(1, 1440)] public int HousekeepingMinutes { get; set; } = 30;
    [Range(1, 1440)] public int MaintenanceMinutes { get; set; } = 60;

    public TimeSpan AttentionTimeOf(ServiceArea area) => TimeSpan.FromMinutes(area switch
    {
        ServiceArea.Housekeeping => HousekeepingMinutes,
        ServiceArea.Maintenance => MaintenanceMinutes,
        _ => ReceptionMinutes
    });
}
