using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RoomTrack.Notifications.Worker.Push.Transport;
using Xunit;

namespace RoomTrack.Notifications.Worker.Tests.Push;

/// <summary>Firebase Cloud Messaging HTTP v1 against a fake server: how each answer is classified.</summary>
public class FcmPushTransportTests
{
    private readonly FakeServer _server = new();

    private FcmPushTransport Transport()
    {
        using var rsa = RSA.Create(2048);
        var key = GoogleServiceAccountKey.Parse(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["client_email"] = "worker@roomtrack.iam.gserviceaccount.com",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["token_uri"] = "https://oauth2.test/token",
            ["project_id"] = "roomtrack"
        }));
        var client = new HttpClient(_server) { BaseAddress = new Uri("https://fcm.test/") };
        return new FcmPushTransport(client, new GoogleAccessTokenProvider(client, key, FcmPushTransport.Scope, TimeProvider.System),
            key.ProjectId!, NullLogger<FcmPushTransport>.Instance);
    }

    private static PushDelivery Delivery(params string[] tokens) =>
        new(tokens, "priority_changed", "¡Limpieza urgente!", "Habitación 101", new Dictionary<string, string> { ["id"] = "7" }, Urgent: true);

    [Fact]
    public async Task EachToken_ShouldBeClassifiedByTheAnswerOfFcm()
    {
        _server.Answers["ok"] = (HttpStatusCode.OK, "{}");
        _server.Answers["gone"] = (HttpStatusCode.NotFound, """{"error":{"status":"NOT_FOUND"}}""");
        _server.Answers["bad"] = (HttpStatusCode.BadRequest, """{"error":{"status":"INVALID_ARGUMENT"}}""");
        _server.Answers["busy"] = (HttpStatusCode.ServiceUnavailable, "{}");

        var result = await Transport().DeliverAsync(Delivery("ok", "gone", "bad", "busy"), CancellationToken.None);

        result.Delivered.Should().Equal("ok");
        result.Rejected.Should().Equal("gone", "bad");
        result.Failed.Should().Equal("busy");
        _server.TokenRequests.Should().Be(1, "the access token is reused for every device");
    }

    [Fact]
    public async Task AnUrgentNotification_ShouldGoWithHighPriority_AndTheDataOfTheApp()
    {
        _server.Answers["ok"] = (HttpStatusCode.OK, "{}");

        await Transport().DeliverAsync(Delivery("ok"), CancellationToken.None);

        using var sent = JsonDocument.Parse(_server.Bodies.Single());
        var message = sent.RootElement.GetProperty("message");
        message.GetProperty("token").GetString().Should().Be("ok");
        message.GetProperty("android").GetProperty("priority").GetString().Should().Be("HIGH");
        message.GetProperty("data").GetProperty("id").GetString().Should().Be("7");
        message.GetProperty("data").GetProperty("category").GetString().Should().Be("priority_changed");
        _server.Paths.Should().Contain("/v1/projects/roomtrack/messages:send");
    }

    [Fact]
    public async Task RefusedCredentials_ShouldFailTheWholeDelivery()
    {
        _server.Answers["ok"] = (HttpStatusCode.Forbidden, """{"error":{"status":"PERMISSION_DENIED"}}""");

        var act = () => Transport().DeliverAsync(Delivery("ok"), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class FakeServer : HttpMessageHandler
    {
        public Dictionary<string, (HttpStatusCode Status, string Body)> Answers { get; } = [];
        public List<string> Bodies { get; } = [];
        public List<string> Paths { get; } = [];
        public int TokenRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "oauth2.test")
            {
                TokenRequests++;
                var form = await request.Content!.ReadAsStringAsync(cancellationToken);
                form.Should().Contain("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer");
                return Json(HttpStatusCode.OK, """{"access_token":"access","expires_in":3600}""");
            }

            request.Headers.Authorization!.Parameter.Should().Be("access");
            Paths.Add(request.RequestUri.AbsolutePath);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);
            var token = JsonDocument.Parse(body).RootElement.GetProperty("message").GetProperty("token").GetString()!;
            var (status, answer) = Answers[token];
            return Json(status, answer);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
