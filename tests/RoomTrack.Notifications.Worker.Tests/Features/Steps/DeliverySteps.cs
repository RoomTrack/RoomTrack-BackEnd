using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Reqnroll;
using RoomTrack.Contracts.Messaging;
using RoomTrack.Notifications.Worker.Email;
using RoomTrack.Notifications.Worker.Email.Transport;
using RoomTrack.Notifications.Worker.Push;
using RoomTrack.Notifications.Worker.Push.Transport;

namespace RoomTrack.Notifications.Worker.Tests.Features.Steps;

/// <summary>
///     Steps of the delivery of push notifications and e-mails (US-22) through the real consumers of the worker, with a
///     simulated provider and a consume context that records what the worker publishes.
/// </summary>
[Binding]
public sealed class DeliverySteps
{
    private readonly FakeProvider _provider = new();
    private DeliverPushNotification? _push;
    private SendEmail? _email;
    private RecordingContext<DeliverPushNotification>? _pushContext;
    private Exception? _thrown;

    [Given("que se me asigna una tarea y mi celular está registrado")]
    public void GivenATaskIsAssigned() =>
        _push = Push(["token-rosa"], PushCategories.CleaningTaskAssigned, "Tarea asignada", "Habitación 204", urgent: false);

    [Given("que una tarea asignada cambia a urgente")]
    public void GivenATaskBecomesUrgent() =>
        _push = Push(["token-rosa"], PushCategories.PriorityChanged, "¡Limpieza urgente!", "Habitación 204: prioridad Normal → Urgent",
            urgent: true);

    [Given("una notificación para un celular activo y otro donde se desinstaló la app")]
    public void GivenAnUninstalledDevice()
    {
        _provider.Rejected.Add("token-viejo");
        _push = Push(["token-rosa", "token-viejo"], PushCategories.IncidentAssigned, "Nueva incidencia", "Ducha con fuga", urgent: false);
    }

    [Given("una notificación para dos celulares y el proveedor solo alcanza a uno")]
    public void GivenAProviderOutageForOneDevice()
    {
        _provider.Failed.Add("token-ana");
        _push = Push(["token-rosa", "token-ana"], PushCategories.Message, "Canal de Housekeeping", "Hoy llega un grupo", urgent: false);
    }

    [Given("un correo para una dirección que el servidor rechaza para siempre")]
    public void GivenAPermanentlyRejectedEmail() =>
        _email = new SendEmail("no-existe@dominio-invalido.pe", "Tu comprobante B001-00000012", "<p>Hola</p>", "Hola", "Bookings");

    [When("esto ocurre")]
    [When("ocurre el cambio")]
    [When("el worker la entrega")]
    public async Task WhenTheWorkerDelivers()
    {
        _pushContext = new RecordingContext<DeliverPushNotification>(_push!);
        try
        {
            await new DeliverPushNotificationConsumer(_provider, TimeProvider.System, NullLogger<DeliverPushNotificationConsumer>.Instance)
                .Consume(_pushContext.Proxy);
        }
        catch (Exception e) { _thrown = e; }
    }

    [When("el worker lo procesa")]
    public async Task WhenTheWorkerProcessesTheEmail()
    {
        var context = new RecordingContext<SendEmail>(_email!);
        try
        {
            await new SendEmailConsumer(new RejectingEmailTransport(), NullLogger<SendEmailConsumer>.Instance).Consume(context.Proxy);
        }
        catch (Exception e) { _thrown = e; }
    }

    [Then("recibo una notificación push inmediata")]
    public void ThenIGetAPush()
    {
        var delivery = _provider.Deliveries.Should().ContainSingle().Subject;
        delivery.DeviceTokens.Should().Equal("token-rosa");
        delivery.Category.Should().Be(PushCategories.CleaningTaskAssigned);
        delivery.Urgent.Should().BeFalse();
        _pushContext!.Published.Should().BeEmpty();
    }

    [Then("recibo una notificación destacada")]
    public void ThenIGetAHighlightedPush()
    {
        var delivery = _provider.Deliveries.Should().ContainSingle().Subject;
        delivery.Urgent.Should().BeTrue("urgent notifications go with high priority");
        delivery.Title.Should().Be("¡Limpieza urgente!");
    }

    [Then("se avisa a Identity que olvide el dispositivo desinstalado")]
    public void ThenIdentityForgetsTheDevice()
    {
        _thrown.Should().BeNull();
        _pushContext!.Published.OfType<PushDeviceTokensRejected>().Should().ContainSingle()
            .Which.DeviceTokens.Should().Equal("token-viejo");
    }

    [Then("se reintenta solo el dispositivo pendiente, sin duplicar la notificación")]
    public void ThenOnlyThePendingDeviceIsRetried()
    {
        _thrown.Should().BeNull("one device already got it: the message is not thrown back");
        _pushContext!.Published.OfType<DeliverPushNotification>().Should().ContainSingle()
            .Which.DeviceTokens.Should().Equal("token-ana");
    }

    [Then("no se reintenta")]
    public void ThenItIsNotRetried() => _thrown.Should().BeNull("a permanent rejection is logged and dropped, not retried");

    private static DeliverPushNotification Push(IReadOnlyList<string> tokens, string category, string title, string body, bool urgent) =>
        new(tokens, category, title, body, new Dictionary<string, string> { ["screen"] = "task", ["id"] = "7" }, urgent, "Accommodations");

    /// <summary>A push provider that accepts every token except the ones marked rejected or failed.</summary>
    private sealed class FakeProvider : IPushTransport
    {
        public List<string> Rejected { get; } = [];
        public List<string> Failed { get; } = [];
        public List<PushDelivery> Deliveries { get; } = [];

        public Task<PushDeliveryResult> DeliverAsync(PushDelivery delivery, CancellationToken cancellationToken)
        {
            Deliveries.Add(delivery);
            return Task.FromResult(new PushDeliveryResult(
                delivery.DeviceTokens.Except(Rejected).Except(Failed).ToList(),
                delivery.DeviceTokens.Intersect(Rejected).ToList(),
                delivery.DeviceTokens.Intersect(Failed).ToList()));
        }
    }

    private sealed class RejectingEmailTransport : IEmailTransport
    {
        public Task DeliverAsync(EmailMessage message, CancellationToken cancellationToken) =>
            throw new EmailDeliveryException("550 5.1.1 Recipient address rejected", isPermanent: true);
    }

    /// <summary>A consume context carrying the message and recording what the consumer publishes.</summary>
    private sealed class RecordingContext<T> where T : class
    {
        public RecordingContext(T message)
        {
            Proxy = DispatchProxy.Create<ConsumeContext<T>, ContextProxy>();
            var proxy = (ContextProxy)(object)Proxy;
            proxy.Message = message;
            proxy.Published = Published;
        }

        public ConsumeContext<T> Proxy { get; }
        public List<object> Published { get; } = [];
    }

    public class ContextProxy : DispatchProxy
    {
        public object Message { get; set; } = null!;
        public List<object> Published { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_Message":
                    return Message;
                case "get_CancellationToken":
                    return CancellationToken.None;
                case "Publish" when args is { Length: > 0 } && args[0] is { } published:
                    Published.Add(published);
                    return Task.CompletedTask;
                case "GetPayload" or "TryGetPayload" or "get_Headers":
                    throw new NotSupportedException(targetMethod.Name);
                default:
                    return targetMethod?.ReturnType == typeof(Task) ? Task.CompletedTask
                        : targetMethod?.ReturnType.IsValueType == true && targetMethod.ReturnType != typeof(void)
                            ? Activator.CreateInstance(targetMethod.ReturnType) : null;
            }
        }
    }
}
