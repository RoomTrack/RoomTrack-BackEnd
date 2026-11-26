using System;
using System.Linq;
using FluentAssertions;
using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.Events;
using RoomTrack.Payments.Domain.Model.Exceptions;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Xunit;

namespace RoomTrack.Tests.Payments.Domain;

/// <summary>Rules of the folio of a stay (US-15/16) and of the receipts (US-17).</summary>
public class GuestFolioTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 12, 20, 0, 0, TimeSpan.Zero);

    private static GuestFolio Folio() => GuestFolio.Open(42, 1, "RT-7K2M9Q", "Ana Torres", "ana@correo.pe", Now);

    [Fact]
    public void ACharge_ShouldOpenABalance_AndBlockTheCheckOut()
    {
        var folio = Folio();
        folio.PostCharge("Minibar", FolioChargeCategory.Minibar, 18.5m, 7, Now);

        folio.Balance.Should().Be(18.5m);
        folio.Status.Should().Be(FolioStatus.Open);
        var checkOut = () => folio.EnsureSettledForCheckOut();
        checkOut.Should().Throw<FolioBalancePendingException>().Which.Parameters.Should().ContainKeys("balance", "charges");
    }

    [Fact]
    public void PayingTheWholeBalance_ShouldSettleTheFolio()
    {
        var folio = Folio();
        folio.PostCharge("Lavandería", FolioChargeCategory.Laundry, 30m, 7, Now);

        folio.Pay([new FolioPaymentPart(10m, PaymentMethod.Cash, null), new FolioPaymentPart(20m, PaymentMethod.Yape, "OP-1")], null, Now);

        folio.Balance.Should().Be(0);
        folio.Status.Should().Be(FolioStatus.Settled);
        folio.DomainEvents.OfType<FolioSettledEvent>().Should().ContainSingle();
    }

    [Fact]
    public void APaymentAboveTheBalance_ShouldBeRefused()
    {
        var folio = Folio();
        folio.PostCharge("Spa", FolioChargeCategory.Spa, 50m, 7, Now);

        var pay = () => folio.Pay([new FolioPaymentPart(60m, PaymentMethod.Cash, null)], null, Now);

        pay.Should().Throw<FolioOverpaymentException>();
        folio.Payments.Should().BeEmpty();
    }

    [Fact]
    public void ANonCashPart_ShouldNeedItsOperationNumber()
    {
        var folio = Folio();
        folio.PostCharge("Spa", FolioChargeCategory.Spa, 50m, 7, Now);

        var pay = () => folio.Pay([new FolioPaymentPart(50m, PaymentMethod.Plin, null)], null, Now);

        pay.Should().Throw<InvalidFieldException>().Which.Field.Should().Be("operationNumber");
    }

    [Fact]
    public void TheCheckOut_ShouldReleaseTheDeposit_AndCloseTheFolio()
    {
        var folio = Folio();
        folio.HoldDeposit(200m, PaymentMethod.CardAtFrontDesk, "POS-1", Now);

        folio.CloseForCheckOut(Now.AddDays(2));

        folio.Status.Should().Be(FolioStatus.Closed);
        folio.DepositStatus.Should().Be(DepositStatus.Released);
        folio.DomainEvents.OfType<DepositReleasedEvent>().Should().ContainSingle().Which.Amount.Should().Be(200m);
        var charge = () => folio.PostCharge("Tarde", FolioChargeCategory.Other, 5m, 7, Now);
        charge.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(FolioErrorCodes.Closed);
    }

    [Fact]
    public void OnlyOneDeposit_ShouldBeHeld()
    {
        var folio = Folio();
        folio.HoldDeposit(200m, PaymentMethod.Cash, null, Now);

        var again = () => folio.HoldDeposit(100m, PaymentMethod.Cash, null, Now);

        again.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(FolioErrorCodes.DepositAlreadyHeld);
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("2012345678")]
    [InlineData("ABCDEFGHIJK")]
    public void AnInvalidRuc_ShouldBeRefused(string ruc)
    {
        var create = () => InvoiceData.Create(ruc, "Empresa S.A.C.", null);

        create.Should().Throw<InvalidFieldsException>().Which.Violations.Should().ContainSingle(v => v.Field == "ruc");
    }

    [Fact]
    public void AReceipt_ShouldBreakDownTheIgv_AndUseTheSeriesOfItsType()
    {
        var boleta = Receipt.Issue(1, 42, "RT-7K2M9Q", ReceiptSource.FolioPayment, 7, 118m, PaymentMethod.Cash, "Consumos",
            "Ana Torres", "ana@correo.pe", null, 12, Now);
        var factura = Receipt.Issue(1, 42, "RT-7K2M9Q", ReceiptSource.FolioPayment, 8, 59m, PaymentMethod.Yape, "Consumos",
            "Ana Torres", "ana@correo.pe", InvoiceData.Create("20123456789", "Inversiones Andinas S.A.C.", null), 1, Now);

        boleta.FullNumber.Should().Be("B001-00000012");
        boleta.Subtotal.Should().Be(100m);
        boleta.Igv.Should().Be(18m);
        factura.FullNumber.Should().Be("F001-00000001");
        factura.CustomerDocument.Should().Be("20123456789");
        factura.CustomerName.Should().Be("Inversiones Andinas S.A.C.");
    }
}
