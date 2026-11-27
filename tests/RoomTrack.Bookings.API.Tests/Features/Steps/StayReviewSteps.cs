using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reqnroll;
using RoomTrack.Bookings.API.Infrastructure.Persistence;
using RoomTrack.Bookings.Application.Internal.CommandServices;
using RoomTrack.Bookings.Application.Internal.Configuration;
using RoomTrack.Bookings.Application.Internal.QueryServices;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Profiles.Interfaces.ACL;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Repositories;
using static RoomTrack.Tests.Bookings.BookingTestData;

namespace RoomTrack.Tests.Features.Steps;

/// <summary>
///     Steps of US-21 (evaluation of the stay) and US-26 (satisfaction by area) through the real use cases of the
///     stay reviews (<see cref="StayReviewCommandService"/>, <see cref="StayReviewQueryService"/>) and their EF Core
///     repositories, on an in-memory database. Each request runs with its own database context.
/// </summary>
[Binding]
public sealed class StayReviewSteps : IDisposable
{
    private const int PeriodDays = 30;

    private static readonly BookingRequester Guest = BookingRequester.Guest(GuestUserId, "alice@example.com");

    private static readonly Dictionary<string, SatisfactionArea> Areas = new()
    {
        ["recepción"] = SatisfactionArea.FrontDesk,
        ["limpieza"] = SatisfactionArea.Housekeeping,
        ["mantenimiento"] = SatisfactionArea.Maintenance
    };

    private readonly string _database = Guid.NewGuid().ToString();
    private readonly List<BookingsDbContext> _contexts = [];
    private readonly Clock _clock = new(new DateTimeOffset(2030, 1, 20, 15, 0, 0, TimeSpan.Zero));
    private int _bookingId;
    private int _nextGuest = 500;
    private DomainException? _error;
    private SatisfactionReport? _satisfaction;

    public void Dispose() => _contexts.ForEach(context => context.Dispose());

    // ── US-21 ───────────────────────────────────────────────────────

    [Given("que terminé mi estadía con el check-out")]
    public void GivenMyStayEnded() => _bookingId = Save(FinishedStay(_clock.Now.AddHours(-2)));

    [Given("que todavía estoy alojado en el hotel")]
    public void GivenIAmStillStaying() => _bookingId = Save(FinishedStay(_clock.Now.AddHours(-2), checkedOut: false));

    [Given("ya califiqué mi estadía")]
    public Task GivenIAlreadyRatedMyStay() => Submit(Guest, _bookingId, new StayRatings(5), null);

    [When("califico mi estadía con {int} estrellas y la limpieza con {int}")]
    public Task WhenIRateMyStay(int overall, int housekeeping) =>
        Submit(Guest, _bookingId, new StayRatings(overall, Housekeeping: housekeeping), null);

    [When("califico mi estadía con {int} estrellas y el comentario {string}")]
    public Task WhenIRateMyStayWithAComment(int overall, string comment) =>
        Submit(Guest, _bookingId, new StayRatings(overall), comment);

    [When("intento calificar mi estadía")]
    public async Task WhenITryToRateMyStay()
    {
        try { await Submit(Guest, _bookingId, new StayRatings(4), "Todo bien"); }
        catch (DomainException e) { _error = e; }
    }

    [Then("mi evaluación queda guardada con {int} estrellas en general y {int} en limpieza")]
    public async Task ThenMyReviewIsStored(int overall, int housekeeping)
    {
        var review = await Queries().Handle(new GetStayReviewByBookingIdQuery(_bookingId, Guest));
        review.Should().NotBeNull();
        (review!.OverallRating, review.HousekeepingRating, review.GuestUserId).Should().Be((overall, housekeeping, GuestUserId));
    }

    [Then("el administrador del hotel lee mi comentario {string}")]
    public async Task ThenTheAdministratorReadsMyComment(string comment)
    {
        var (from, to) = LastDays();
        var reviews = await Queries().Handle(new GetStayReviewsQuery(HotelId, from, to));
        reviews.Should().ContainSingle().Which.Comment.Should().Be(comment);
    }

    [Then("el sistema me indica que podré evaluarla después del check-out")]
    public void ThenICanOnlyRateAfterTheCheckOut()
    {
        _error.Should().BeOfType<InvalidBookingTransitionException>().Which.Code.Should().Be(BookingErrorCodes.ReviewStayNotFinished);
        StoredReviews().Should().BeEmpty();
    }

    [Then("el sistema me indica que ya la evalué")]
    public void ThenIAlreadyRatedIt()
    {
        _error.Should().BeOfType<StayReviewAlreadySubmittedException>().Which.Code.Should().Be(BookingErrorCodes.ReviewAlreadySubmitted);
        StoredReviews().Should().ContainSingle().Which.OverallRating.Should().Be(5, "the first evaluation is kept");
    }

    // ── US-26 ───────────────────────────────────────────────────────

    [Given("que los huéspedes evaluaron sus estadías:")]
    public async Task GivenTheGuestsRatedTheirStays(DataTable ratings)
    {
        foreach (var row in ratings.Rows)
            await AGuestRates(new StayRatings(int.Parse(row["general"]), Optional(row["recepción"]),
                Optional(row["limpieza"]), Optional(row["mantenimiento"])));
    }

    [Given("que un huésped evaluó su estadía hace {int} días con {int} estrella(s)")]
    public async Task GivenAGuestRatedDaysAgo(int daysAgo, int overall)
    {
        var now = _clock.Now;
        _clock.Now = now.AddDays(-daysAgo);
        await AGuestRates(new StayRatings(overall, overall, overall, overall));
        _clock.Now = now;
    }

    [When("consulto la satisfacción por área")]
    public async Task WhenIQueryTheSatisfaction()
    {
        var (from, to) = LastDays();
        _satisfaction = await Queries().Handle(new GetStaySatisfactionQuery(HotelId, from, to));
    }

    [Then("la satisfacción es:")]
    public void ThenTheSatisfactionIs(DataTable expected)
    {
        foreach (var row in expected.Rows)
        {
            var score = row["área"] == "general"
                ? _satisfaction!.Overall
                : _satisfaction!.Areas.Single(area => area.Area == Areas[row["área"]]).Score;
            score.Count.Should().Be(int.Parse(row["evaluaciones"]), row["área"]);
            score.Average.Should().Be(double.Parse(row["promedio"], CultureInfo.InvariantCulture), row["área"]);
            score.SatisfiedPercent.Should().Be(double.Parse(row["satisfechos"], CultureInfo.InvariantCulture), row["área"]);
        }
    }

    [Then("la satisfacción general tiene {int} evaluación(es) con promedio {int}")]
    public void ThenTheOverallSatisfactionHas(int count, int average)
    {
        _satisfaction!.Overall.Count.Should().Be(count);
        _satisfaction.Overall.Average.Should().Be(average);
    }

    [Then("cada área aparece sin evaluaciones ni promedio")]
    public void ThenEveryAreaHasNoRatings()
    {
        _satisfaction!.Overall.Count.Should().Be(0);
        _satisfaction.Areas.Select(area => area.Area).Should().Equal(Enum.GetValues<SatisfactionArea>());
        _satisfaction.Areas.Should().OnlyContain(area => area.Score.Count == 0 && area.Score.Average == null);
    }

    // ── Support ─────────────────────────────────────────────────────

    private static int? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : int.Parse(value);

    /// <summary>A new guest finishes a stay an hour ago and evaluates it now.</summary>
    private async Task AGuestRates(StayRatings ratings)
    {
        var guestUserId = _nextGuest++;
        var bookingId = Save(FinishedStay(_clock.Now.AddHours(-1), guestUserId));
        await Submit(BookingRequester.Guest(guestUserId, $"guest{guestUserId}@example.com"), bookingId, ratings, null);
    }

    /// <summary>The last 30 days, read a minute after the last change (the end is exclusive).</summary>
    private (DateTimeOffset From, DateTimeOffset To) LastDays()
    {
        _clock.Now = _clock.Now.AddMinutes(1);
        return (_clock.Now.AddDays(-PeriodDays), _clock.Now);
    }

    private Task<StayReview> Submit(BookingRequester guest, int bookingId, StayRatings ratings, string? comment)
    {
        var context = NewContext();
        var service = new StayReviewCommandService(new BookingRepository(context), new StayReviewRepository(context),
            new NoGuestProfiles(), new HotelCalendar(Options.Create(new BookingPolicySettings()), _clock),
            new ContextUnitOfWork(context));
        return service.Handle(new SubmitStayReviewCommand(bookingId, guest, ratings, comment));
    }

    private StayReviewQueryService Queries()
    {
        var context = NewContext();
        return new StayReviewQueryService(new BookingRepository(context), new StayReviewRepository(context), new NoGuestProfiles());
    }

    private List<StayReview> StoredReviews() => NewContext().Set<StayReview>().AsNoTracking().ToList();

    private int Save(Booking booking)
    {
        var context = NewContext();
        context.Add(booking);
        context.SaveChanges();
        return booking.Id;
    }

    private BookingsDbContext NewContext()
    {
        var context = new BookingsDbContext(new DbContextOptionsBuilder<BookingsDbContext>().UseInMemoryDatabase(_database).Options);
        _contexts.Add(context);
        return context;
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Saves on <c>CompleteAsync</c>; the in-memory provider has no transactions.</summary>
    private sealed class ContextUnitOfWork(BookingsDbContext context) : IUnitOfWork
    {
        public Task CompleteAsync() => context.SaveChangesAsync();
        public Task ExecuteInTransactionAsync(Func<Task> work) => work();
    }

    /// <summary>The guests of these scenarios booked with their account (no guest profile).</summary>
    private sealed class NoGuestProfiles : IGuestProfilesContextFacade
    {
        public Task<Guid?> CreateGuestProfileAsync(string firstName, string lastName, string phone, string? email = null, int? userId = null) =>
            Task.FromResult<Guid?>(null);
        public Task<Guid?> FetchGuestProfileIdByUserIdAsync(int userId) => Task.FromResult<Guid?>(null);
        public Task<Guid?> FetchGuestProfileIdByEmailAsync(string email) => Task.FromResult<Guid?>(null);
        public Task<bool> LinkGuestProfileToUserAsync(Guid guestProfileId, int userId, string email) => Task.FromResult(false);
    }
}
