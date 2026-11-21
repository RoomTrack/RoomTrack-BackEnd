using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomTrack.Analytics.Domain.Model.Aggregates;
using RoomTrack.Analytics.Domain.Model.Queries;
using RoomTrack.Analytics.Domain.Services;
using RoomTrack.Analytics.Interfaces.REST;
using RoomTrack.Analytics.Interfaces.REST.Resources;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.Authorization;
using Xunit;

namespace RoomTrack.Analytics.API.Tests.Interfaces;

/// <summary>Scope of the metrics by role (D2) and the resilience lab when it is not configured.</summary>
public class AnalyticsControllerTests
{
    private readonly RecordingQueryService _queries = new();

    [Fact]
    public async Task AHotelAdmin_ShouldOnlyGetTheirOwnHotel_EvenWhenAskingForAnother()
    {
        var controller = ControllerFor(UserRoles.Admin, hotelId: 3);

        var result = await controller.GetMonthlyPerformance(hotelId: 99);

        _queries.Received.Should().Be(new GetMonthlyPerformanceQuery(3, WholeChain: false));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<PerformanceMetricsResource>()
            .Which.TotalRevenue.Should().Be(1234.5m);
    }

    [Fact]
    public async Task AChainAdmin_ShouldGetTheHotelTheyAskFor()
    {
        await ControllerFor(UserRoles.ChainAdmin).GetMonthlyPerformance(hotelId: 5);

        _queries.Received.Should().Be(new GetMonthlyPerformanceQuery(5, WholeChain: false));
    }

    [Fact]
    public async Task AChainAdmin_WithoutHotel_ShouldGetTheWholeChain()
    {
        await ControllerFor(UserRoles.ChainAdmin).GetMonthlyPerformance(hotelId: null);

        _queries.Received.Should().Be(new GetMonthlyPerformanceQuery(null, WholeChain: true));
    }

    [Fact]
    public async Task TheCacheLab_WithoutRedis_ShouldAnswer503()
    {
        var controller = ControllerFor(UserRoles.ChainAdmin);

        var write = await controller.CacheData("hello");
        var read = await controller.GetCache();

        write.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        read.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    private AnalyticsController ControllerFor(string role, int? hotelId = null)
    {
        List<Claim> claims =
        [
            new(IamClaimTypes.UserId, "1"),
            new(IamClaimTypes.Username, "admin@roomtrack.pe"),
            new(IamClaimTypes.Role, role)
        ];
        if (hotelId is { } id) claims.Add(new Claim(IamClaimTypes.HotelId, id.ToString()));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", IamClaimTypes.Username, IamClaimTypes.Role));
        return new AnalyticsController(_queries)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
        };
    }

    private sealed class RecordingQueryService : IAnalyticsQueryService
    {
        public GetMonthlyPerformanceQuery? Received { get; private set; }

        public Task<PerformanceMetrics> Handle(GetMonthlyPerformanceQuery query)
        {
            Received = query;
            return Task.FromResult(new PerformanceMetrics { TotalRevenue = 1234.5m, TotalBookings = 4 });
        }
    }
}
