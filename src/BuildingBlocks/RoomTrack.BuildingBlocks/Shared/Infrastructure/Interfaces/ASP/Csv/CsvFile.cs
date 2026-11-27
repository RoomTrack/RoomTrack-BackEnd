using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace RoomTrack.Shared.Infrastructure.Interfaces.ASP.Csv;

/// <summary>
///     Builds a CSV download (RFC 4180, comma separated, UTF-8 with BOM so Excel shows the accents). Numbers and dates
///     are written in the invariant culture (decimal point, ISO 8601).
/// </summary>
/// <remarks>
///     A text cell that starts with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab or carriage return is prefixed with an
///     apostrophe, so a spreadsheet never runs it as a formula (CSV injection): some cells hold text written by guests.
/// </remarks>
public static class CsvFile
{
    public const string ContentType = "text/csv; charset=utf-8";

    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>The CSV text of <paramref name="header"/> and <paramref name="rows"/>, one line per row.</summary>
    public static string Write(IEnumerable<string> header, IEnumerable<IEnumerable<object?>> rows)
    {
        var csv = new StringBuilder();
        AppendLine(csv, header);
        foreach (var row in rows) AppendLine(csv, row);
        return csv.ToString();
    }

    /// <summary>A file download named <paramref name="fileName"/> with the CSV of <paramref name="header"/> and <paramref name="rows"/>.</summary>
    public static FileContentResult Download(string fileName, IEnumerable<string> header, IEnumerable<IEnumerable<object?>> rows)
    {
        var text = Write(header, rows);
        var bytes = Utf8WithBom.GetPreamble().Concat(Utf8WithBom.GetBytes(text)).ToArray();
        return new FileContentResult(bytes, ContentType) { FileDownloadName = fileName };
    }

    private static void AppendLine(StringBuilder csv, IEnumerable<object?> cells)
    {
        var first = true;
        foreach (var cell in cells)
        {
            if (!first) csv.Append(',');
            csv.Append(Escape(Format(cell)));
            first = false;
        }
        csv.Append("\r\n");
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        string text => Neutralize(text),
        DateTimeOffset instant => instant.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture),
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => Neutralize(value.ToString() ?? string.Empty)
    };

    private static string Neutralize(string text) =>
        text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
}
