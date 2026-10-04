namespace RoomTrack.Contracts.Messaging;

/// <summary>
///     Command message: deliver an e-mail. Sent by any service through its transactional outbox and consumed by the
///     Notifications worker, which owns the connection to the mail system (Brevo API, SMTP or the log).
/// </summary>
/// <param name="To">Recipient address.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="HtmlBody">HTML body.</param>
/// <param name="TextBody">Plain-text alternative of <paramref name="HtmlBody"/>.</param>
/// <param name="SourceService">Service that asked for the e-mail (for the logs).</param>
public sealed record SendEmail(string To, string Subject, string HtmlBody, string TextBody, string SourceService);
