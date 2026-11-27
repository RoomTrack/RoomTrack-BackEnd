using System;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Interfaces.ASP.Csv;
using RoomTrack.StaffPerformance.Domain.Model.Aggregates;
using RoomTrack.StaffPerformance.Domain.Model.Exceptions;
using Xunit;

namespace RoomTrack.Tests.StaffPerformance;

/// <summary>Rules of the scheduled delivery of the reports and of their CSV export (US-34).</summary>
public class ReportScheduleTests
{
    /// <summary>Thursday 10/01/2030, 15:00 UTC.</summary>
    private static readonly DateTimeOffset Thursday = new(2030, 1, 10, 15, 0, 0, TimeSpan.Zero);

    private static ReportScheduleSettings Weekly(params string[] recipients) => new(ReportFrequency.Weekly, recipients);

    [Fact]
    public void AWeeklySchedule_ShouldRunOnTheNextMonday_WithThePrevious7Days()
    {
        var schedule = ReportSchedule.Create(1, Weekly("gerencia@hotel.pe"), 1, Thursday);

        schedule.NextRunAt.Should().Be(new DateTimeOffset(2030, 1, 14, 13, 0, 0, TimeSpan.Zero));
        schedule.DuePeriod.Should().Be((new DateTimeOffset(2030, 1, 7, 13, 0, 0, TimeSpan.Zero), schedule.NextRunAt));
        schedule.IsDue(schedule.NextRunAt.AddMinutes(-1)).Should().BeFalse();
        schedule.IsDue(schedule.NextRunAt).Should().BeTrue();
    }

    [Fact]
    public void AMonthlySchedule_ShouldRunOnThe1st_WithThePreviousMonth()
    {
        var schedule = ReportSchedule.Create(1, new ReportScheduleSettings(ReportFrequency.Monthly, ["gerencia@hotel.pe"]), 1, Thursday);

        schedule.NextRunAt.Should().Be(new DateTimeOffset(2030, 2, 1, 13, 0, 0, TimeSpan.Zero));
        schedule.DuePeriod.From.Should().Be(new DateTimeOffset(2030, 1, 1, 13, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void AfterSending_ShouldSkipTheMissedPeriods()
    {
        var schedule = ReportSchedule.Create(1, Weekly("gerencia@hotel.pe"), 1, Thursday);
        var threeWeeksLate = schedule.NextRunAt.AddDays(15);

        schedule.MarkSent(threeWeeksLate);

        schedule.NextRunAt.Should().Be(new DateTimeOffset(2030, 2, 4, 13, 0, 0, TimeSpan.Zero));
        schedule.LastSentAt.Should().Be(threeWeeksLate);
    }

    [Fact]
    public void Recipients_ShouldBeNormalizedAndWithoutDuplicates()
    {
        var schedule = ReportSchedule.Create(1, Weekly(" Gerencia@Hotel.pe ", "gerencia@hotel.pe", "ops@hotel.pe"), 1, Thursday);

        schedule.Recipients.Should().Equal("gerencia@hotel.pe", "ops@hotel.pe");
    }

    [Theory]
    [InlineData(StaffPerformanceErrorCodes.RecipientsRequired)]
    [InlineData(StaffPerformanceErrorCodes.RecipientInvalid, "no-es-un-correo")]
    [InlineData(StaffPerformanceErrorCodes.RecipientInvalid, "a@b.pe,c@d.pe")]
    public void InvalidRecipients_ShouldBeRefused(string code, params string[] recipients)
    {
        var create = () => ReportSchedule.Create(1, Weekly(recipients), 1, Thursday);

        create.Should().Throw<InvalidFieldException>().Which.Violation.Code.Should().Be(code);
    }

    [Fact]
    public void MoreThan10Recipients_ShouldBeRefused()
    {
        var recipients = new string[ReportSchedule.MaxRecipients + 1];
        for (var i = 0; i < recipients.Length; i++) recipients[i] = $"persona{i}@hotel.pe";

        var create = () => ReportSchedule.Create(1, Weekly(recipients), 1, Thursday);

        create.Should().Throw<InvalidFieldException>().Which.Violation.Code.Should().Be(StaffPerformanceErrorCodes.TooManyRecipients);
    }

    [Fact]
    public void ASchedule_ShouldIncludeAtLeastOneReport()
    {
        var create = () => ReportSchedule.Create(1, Weekly("gerencia@hotel.pe") with
        {
            IncludeProductivity = false, IncludeServiceTimes = false
        }, 1, Thursday);

        create.Should().Throw<InvalidFieldException>().Which.Violation.Code.Should().Be(StaffPerformanceErrorCodes.NoReportSelected);
    }

    [Fact]
    public void APausedSchedule_ShouldNotBeDue_AndWhenReactivatedNotSendWhatItMissed()
    {
        var schedule = ReportSchedule.Create(1, Weekly("gerencia@hotel.pe") with { IsActive = false }, 1, Thursday);
        schedule.IsDue(Thursday.AddDays(30)).Should().BeFalse();

        schedule.Update(Weekly("gerencia@hotel.pe"), Thursday.AddDays(30));

        schedule.NextRunAt.Should().BeAfter(Thursday.AddDays(30));
    }

    [Fact]
    public void Csv_ShouldQuoteSpecialCharacters_AndNeutralizeFormulas()
    {
        var csv = CsvFile.Write(["comentario", "nota", "promedio"],
        [
            ["Bien, pero \"lento\"", 4, 4.5],
            ["=HYPERLINK(\"http://x\")", null, 2.25m]
        ]);

        csv.Should().Be("comentario,nota,promedio\r\n" +
                        "\"Bien, pero \"\"lento\"\"\",4,4.5\r\n" +
                        "\"'=HYPERLINK(\"\"http://x\"\")\",,2.25\r\n");
    }

    [Fact]
    public void CsvDownload_ShouldBeUtf8WithBom()
    {
        var file = CsvFile.Download("reporte.csv", ["área"], [["Recepción"]]);

        file.Should().BeOfType<FileContentResult>().Which.FileDownloadName.Should().Be("reporte.csv");
        file.FileContents[..3].Should().Equal(0xEF, 0xBB, 0xBF);
        Encoding.UTF8.GetString(file.FileContents[3..]).Should().Be("área\r\nRecepción\r\n");
    }
}
