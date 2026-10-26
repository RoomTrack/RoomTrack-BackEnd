using System.ComponentModel.DataAnnotations;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Notifications.Domain.Model.Aggregates;
using RoomTrack.Notifications.Domain.Model.Commands;
using RoomTrack.Notifications.Domain.Model.Queries;
using RoomTrack.Notifications.Domain.Model.ValueObjects;
using RoomTrack.Notifications.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomTrack.Contracts.Messaging;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Notifications.Interfaces.REST;

/// <summary>The token the push provider gave the app on this device.</summary>
public record RegisterPushDeviceResource([Required] string? Token, [Required] PushPlatform? Platform);

public record PushDeviceResource(int Id, PushPlatform Platform, DateTimeOffset RegisteredAt, DateTimeOffset LastRegisteredAt);

/// <summary>The kinds of notification the user does not want (the rest stay on).</summary>
public record UpdateNotificationPreferencesResource([Required] IReadOnlyList<string>? DisabledCategories);

public record NotificationCategoryResource(string Category, bool Enabled);

public record NotificationPreferencesResource(IReadOnlyList<NotificationCategoryResource> Categories);

/// <summary>
///     Push notifications of the signed-in user (US-22): the devices where they get them and the kinds they want.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/users/me")]
[Produces("application/json")]
[SwaggerTag("Push notifications: devices and preferences of the signed-in user")]
public class PushNotificationsController(
    IPushNotificationCommandService commandService,
    IPushNotificationQueryService queryService) : ControllerBase
{
    /// <summary>
    ///     Registers the device token of the app. Call it after every sign-in and whenever the provider gives a new
    ///     token; registering the same token again is harmless (and moves it to this user if someone else used the device).
    /// </summary>
    [HttpPost("push-devices")]
    [SwaggerOperation(Summary = "Register this device for push notifications", OperationId = "RegisterPushDevice")]
    [ProducesResponseType(typeof(PushDeviceResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterPushDeviceResource resource)
    {
        var device = await commandService.Handle(new RegisterPushDeviceCommand(User.GetUserId(), resource.Token!, resource.Platform!.Value));
        return Ok(ToResource(device));
    }

    [HttpGet("push-devices")]
    [SwaggerOperation(Summary = "List my devices with push notifications", OperationId = "GetMyPushDevices")]
    [ProducesResponseType(typeof(IEnumerable<PushDeviceResource>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevices() =>
        Ok((await queryService.Handle(new GetPushDevicesOfUserQuery(User.GetUserId()))).Select(ToResource));

    /// <summary>Stops the notifications on one of my devices (call it when signing out).</summary>
    [HttpDelete("push-devices/{deviceId:int}")]
    [SwaggerOperation(Summary = "Unregister one of my devices", OperationId = "UnregisterPushDevice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnregisterDevice(int deviceId) =>
        await commandService.Handle(new UnregisterPushDeviceCommand(User.GetUserId(), deviceId)) ? NoContent() : NotFound();

    [HttpGet("notification-preferences")]
    [SwaggerOperation(Summary = "Get the kinds of notification I receive", OperationId = "GetMyNotificationPreferences")]
    [ProducesResponseType(typeof(NotificationPreferencesResource), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreferences() =>
        Ok(ToResource(await queryService.Handle(new GetNotificationPreferencesQuery(User.GetUserId()))));

    /// <summary>Turns off the kinds of notification listed (and on every other one).</summary>
    [HttpPut("notification-preferences")]
    [SwaggerOperation(Summary = "Choose the kinds of notification I receive", OperationId = "UpdateMyNotificationPreferences")]
    [ProducesResponseType(typeof(NotificationPreferencesResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateNotificationPreferencesResource resource) =>
        Ok(ToResource(await commandService.Handle(
            new UpdateNotificationPreferencesCommand(User.GetUserId(), resource.DisabledCategories!))));

    private static PushDeviceResource ToResource(PushDevice device) =>
        new(device.Id, device.Platform, device.RegisteredAt, device.LastRegisteredAt);

    private static NotificationPreferencesResource ToResource(NotificationPreferences preferences) =>
        new(PushCategories.All.Select(category => new NotificationCategoryResource(category, preferences.IsEnabled(category))).ToList());
}
