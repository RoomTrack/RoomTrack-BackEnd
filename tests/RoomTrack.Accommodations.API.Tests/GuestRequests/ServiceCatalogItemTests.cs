using System;
using FluentAssertions;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Exceptions;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Xunit;

namespace RoomTrack.Tests.GuestRequests;

/// <summary>Rules of the service catalog of a hotel (US-33).</summary>
public class ServiceCatalogItemTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 15, 0, 0, TimeSpan.Zero);

    private static ServiceCatalogItemDetails Breakfast(decimal price = 35m, int? minutes = 30, bool available = true) =>
        new("  Desayuno a la habitación ", " Continental ", ServiceArea.Reception, price, minutes, available);

    [Fact]
    public void AService_ShouldKeepItsDetails()
    {
        var item = ServiceCatalogItem.Create(1, Breakfast(), Now);

        (item.HotelId, item.Name, item.Description, item.Area, item.Price, item.EstimatedMinutes, item.IsAvailable)
            .Should().Be((1, "Desayuno a la habitación", "Continental", ServiceArea.Reception, 35m, (int?)30, true));
        item.IsIncluded.Should().BeFalse();
        ServiceCatalogItem.Create(1, Breakfast(price: 0), Now).IsIncluded.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100000)]
    [InlineData(10.555)]
    public void AnInvalidPrice_ShouldBeRefused(double price)
    {
        var create = () => ServiceCatalogItem.Create(1, Breakfast(price: (decimal)price), Now);

        create.Should().Throw<InvalidFieldException>().Which.Violation.Code.Should().Be(GuestRequestErrorCodes.ServicePriceInvalid);
    }

    [Fact]
    public void AShortName_ShouldBeRefused()
    {
        var create = () => ServiceCatalogItem.Create(1, Breakfast() with { Name = "ab" }, Now);

        create.Should().Throw<InvalidFieldException>().Which.Violation.Code.Should().Be(GuestRequestErrorCodes.ServiceNameLength);
    }

    [Fact]
    public void AnEstimatedTimeOutOfRange_ShouldBeRefused()
    {
        var create = () => ServiceCatalogItem.Create(1, Breakfast(minutes: 0), Now);

        create.Should().Throw<InvalidFieldException>().Which.Violation.Code
            .Should().Be(GuestRequestErrorCodes.ServiceEstimatedMinutesInvalid);
    }

    [Fact]
    public void AWithdrawnService_ShouldNotBeRequested()
    {
        var item = ServiceCatalogItem.Create(1, Breakfast(available: false), Now);

        item.Invoking(i => i.EnsureAvailable()).Should().Throw<ServiceNotAvailableException>();
    }

    [Fact]
    public void ARequestForAService_ShouldKeepItsPriceWhenTheCatalogChanges()
    {
        var item = ServiceCatalogItem.Create(1, Breakfast(), Now);
        var requested = RequestedService.Of(item);

        item.Update(Breakfast(price: 40m), Now.AddDays(1));

        requested.Price.Should().Be(35m);
        item.Price.Should().Be(40m);
        item.UpdatedAt.Should().Be(Now.AddDays(1));
    }
}
