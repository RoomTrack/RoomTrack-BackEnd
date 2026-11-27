using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Reqnroll;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.Commands;
using RoomTrack.GuestRequests.Domain.Model.Exceptions;
using RoomTrack.GuestRequests.Domain.Model.Queries;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.GuestRequests.Domain.Services;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Tests.Features.Support;

namespace RoomTrack.Tests.Features.Steps;

/// <summary>
///     Steps of US-33 (service catalog for the guest) through the real use cases of the catalog and of the guest
///     requests. The hotel and the guest's stay come from the steps of <see cref="OperationsSteps"/>.
/// </summary>
[Binding]
public sealed class ServiceCatalogSteps(OperationsWorld world)
{
    private static readonly Dictionary<string, ServiceArea> Areas = new()
    {
        ["recepción"] = ServiceArea.Reception,
        ["limpieza"] = ServiceArea.Housekeeping,
        ["mantenimiento"] = ServiceArea.Maintenance
    };

    private readonly Dictionary<string, int> _services = new();
    private IReadOnlyList<ServiceCatalogItem> _catalog = [];
    private GuestRequest? _request;
    private DomainException? _error;

    [Given("el hotel ofrece los servicios:")]
    public async Task GivenTheHotelOffers(DataTable services)
    {
        foreach (var row in services.Rows)
        {
            var item = await world.Request<IServiceCatalogCommandService, ServiceCatalogItem>(service => service.Handle(
                new CreateServiceCatalogItemCommand(world.HotelId, new ServiceCatalogItemDetails(row["servicio"], null,
                    Areas[row["área"]], Price(row["precio"]),
                    string.IsNullOrWhiteSpace(row["minutos"]) ? null : int.Parse(row["minutos"]),
                    IsAvailable: row["disponible"] == "sí"))));
            _services[row["servicio"]] = item.Id;
        }
    }

    [When("el huésped abre el catálogo de servicios")]
    public async Task WhenTheGuestOpensTheCatalog() =>
        _catalog = await world.Request<IServiceCatalogQueryService, IReadOnlyList<ServiceCatalogItem>>(service =>
            service.Handle(new GetServiceCatalogQuery(Stay().HotelId, OnlyAvailable: true)));

    [Then("ve {string} con precio {word} y {string} incluido")]
    public void ThenSeesThePrices(string paid, string price, string included)
    {
        _catalog.Should().ContainSingle(item => item.Name == paid).Which.Price.Should().Be(Price(price));
        _catalog.Should().ContainSingle(item => item.Name == included).Which.IsIncluded.Should().BeTrue();
    }

    [Then("no ve {string}")]
    public void ThenDoesNotSee(string name) => _catalog.Should().NotContain(item => item.Name == name);

    [Given("que el huésped solicitó el servicio {string}")]
    [When("el huésped solicita el servicio {string}")]
    public async Task WhenTheGuestRequests(string name) => _request = await RequestService(name);

    [When("el huésped intenta solicitar el servicio {string}")]
    public async Task WhenTheGuestTriesToRequest(string name)
    {
        try { _request = await RequestService(name); }
        catch (DomainException e) { _error = e; }
    }

    [When("el hotel cambia el precio de {string} a {word}")]
    public async Task WhenTheHotelChangesThePrice(string name, string price)
    {
        var current = await world.Request<IServiceCatalogQueryService, ServiceCatalogItem?>(service =>
            service.Handle(new GetServiceCatalogItemByIdQuery(_services[name])));
        await world.Request<IServiceCatalogCommandService, ServiceCatalogItem?>(service => service.Handle(
            new UpdateServiceCatalogItemCommand(current!.Id, new ServiceCatalogItemDetails(current.Name, current.Description,
                current.Area, Price(price), current.EstimatedMinutes, current.IsAvailable))));
    }

    [Then("la solicitud queda registrada como {string} con precio {word}")]
    public void ThenTheRequestIsStored(string title, string price)
    {
        var stored = StoredRequest();
        stored.Title.Should().Be(title);
        stored.ServiceItemId.Should().Be(_services[title]);
        stored.ServicePrice.Should().Be(Price(price));
    }

    [Then("llega al área de recepción con atención estimada en {int} minutos")]
    public void ThenItReachesTheFrontDesk(int minutes)
    {
        var stored = StoredRequest();
        stored.Area.Should().Be(ServiceArea.Reception);
        (stored.EstimatedAttentionBy - stored.CreatedAt).Should().Be(TimeSpan.FromMinutes(minutes));
    }

    [Then("el sistema le indica que el servicio no está disponible")]
    public void ThenTheServiceIsNotAvailable()
    {
        _error.Should().BeOfType<ServiceNotAvailableException>().Which.Code.Should().Be(GuestRequestErrorCodes.ServiceNotAvailable);
        world.Read(context => context.Set<GuestRequest>().Count()).Should().Be(0);
    }

    [Then("la solicitud conserva el precio {word}")]
    public void ThenTheRequestKeepsThePrice(string price) => StoredRequest().ServicePrice.Should().Be(Price(price));

    private RoomTrack.Bookings.Interfaces.ACL.GuestStay Stay() => world.Reservations.Current.Single();

    private Task<GuestRequest> RequestService(string name) =>
        world.Request<IGuestRequestCommandService, GuestRequest>(service => service.Handle(new SendGuestRequestCommand(
            null, null, null, new GuestRequestActor(Stay().GuestUserId!.Value, "luis@example.com", UserRoles.Guest),
            _services[name])));

    private GuestRequest StoredRequest() =>
        world.Read(context => context.Set<GuestRequest>().Single(request => request.Id == _request!.Id));

    private static decimal Price(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
