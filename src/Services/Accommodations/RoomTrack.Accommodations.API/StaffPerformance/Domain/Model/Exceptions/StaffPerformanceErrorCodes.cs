namespace RoomTrack.StaffPerformance.Domain.Model.Exceptions;

/// <summary>Stable error codes of the StaffPerformance context.</summary>
public static class StaffPerformanceErrorCodes
{
    public const string InvalidPeriod = "staff_performance.invalid_period";
    public const string RecipientsRequired = "report_schedule.recipients_required";
    public const string TooManyRecipients = "report_schedule.too_many_recipients";
    public const string RecipientInvalid = "report_schedule.recipient_invalid";
    public const string NoReportSelected = "report_schedule.no_report_selected";
}
