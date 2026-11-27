using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;

/// <summary>
///     Builds the JSON example of a body from its schema, for the operations without a hand-written
///     <see cref="OpenApiRequestExampleAttribute"/>/<see cref="OpenApiResponseExampleAttribute"/>: every property gets
///     the <c>&lt;example&gt;</c> of its XML documentation when it has one, the first value of its enum, or a value of its
///     type and format named after the property (an <c>email</c> looks like an e-mail, a <c>checkInDate</c> like a date).
///     So every documented request and response shows a complete body.
/// </summary>
public static class SchemaExamples
{
    private const int MaxDepth = 6;

    /// <summary>The example of <paramref name="schema"/>, or null when it has no shape to show (e.g. a file).</summary>
    public static IOpenApiAny? Build(OpenApiSchema? schema, SchemaRepository repository) =>
        schema is null ? null : Build(schema, repository, propertyName: null, depth: 0, []);

    private static IOpenApiAny? Build(OpenApiSchema schema, SchemaRepository repository, string? propertyName, int depth,
        HashSet<string> visiting)
    {
        if (schema.Reference?.Id is { } referenceId)
        {
            // A type that contains itself is shown once (null below it).
            if (!repository.Schemas.TryGetValue(referenceId, out var referenced) || !visiting.Add(referenceId)) return new OpenApiNull();
            try { return Build(referenced, repository, propertyName, depth, visiting); }
            finally { visiting.Remove(referenceId); }
        }

        if (schema.Example is not null) return schema.Example;
        if (schema.Enum is { Count: > 0 }) return schema.Enum[0];
        if (schema.AllOf is { Count: > 0 }) return Build(schema.AllOf[0], repository, propertyName, depth, visiting);
        if (schema.OneOf is { Count: > 0 }) return Build(schema.OneOf[0], repository, propertyName, depth, visiting);
        if (schema.AnyOf is { Count: > 0 }) return Build(schema.AnyOf[0], repository, propertyName, depth, visiting);
        if (depth > MaxDepth) return new OpenApiNull();

        switch (schema.Type)
        {
            case "array":
                var array = new OpenApiArray();
                if (schema.Items is not null && Build(schema.Items, repository, Singular(propertyName), depth + 1, visiting) is { } item)
                    array.Add(item);
                return array;

            case "object" or null when schema.Properties is { Count: > 0 }:
                var body = new OpenApiObject();
                foreach (var (name, property) in schema.Properties)
                    body[name] = Build(property, repository, name, depth + 1, visiting) ?? new OpenApiNull();
                return body;

            case "object" when schema.AdditionalProperties is { } values:
                // A dictionary: one entry, keyed as the property suggests.
                return new OpenApiObject
                {
                    [KeyOf(propertyName)] = Build(values, repository, propertyName, depth + 1, visiting) ?? new OpenApiNull()
                };

            case "object":
                return new OpenApiObject();

            case "string":
                return schema.Format == "binary" ? null : new OpenApiString(TextFor(schema.Format, propertyName));

            case "integer":
                return new OpenApiInteger(IntegerFor(propertyName));

            case "number":
                return new OpenApiDouble(NumberFor(propertyName));

            case "boolean":
                return new OpenApiBoolean(true);

            default:
                return null;
        }
    }

    private static string TextFor(string? format, string? name)
    {
        var key = (name ?? string.Empty).ToLowerInvariant();
        switch (format)
        {
            case "date-time":
                return "2026-11-24T20:00:00+00:00";
            case "date":
                return key.Contains("out") ? "2026-11-26" : "2026-11-24";
            case "time":
                return key.Contains("end") || key.Contains("out") ? "15:00:00" : "07:00:00";
            case "uuid":
                return "3fa85f64-5717-4562-b3fc-2c963f66afa6";
            case "email":
                return "ana.torres@example.com";
            case "uri":
                return "https://roomtrack.pe/recurso";
        }

        // Most specific first: "yapeNumber" is a phone, "roomNumber" a room.
        if (key.Contains("email")) return "ana.torres@example.com";
        if (key.Contains("phone") || key.Contains("yape") || key.Contains("plin")) return "987654321";
        if (key.Contains("url") || key.Contains("link")) return "https://roomtrack.pe/recurso";
        if (key == "tokentype") return "Bearer";
        if (key.Contains("token")) return "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI0MiJ9.c2lnbmF0dXJl";
        if (key.Contains("password")) return "Clave-Segura-2026";
        if (key.Contains("bookingcode") || key == "code") return "RT-7K2M9Q";
        if (key.Contains("cci")) return "00219300234567801214";
        if (key.Contains("bankaccount")) return "193-2345678-0-12";
        if (key.Contains("bankname")) return "BCP";
        if (key.Contains("accountholder")) return "Hotel Miraflores SAC";
        if (key.Contains("operationnumber")) return "00123456";
        if (key.Contains("documentnumber")) return "45678912";
        if (key.Contains("roomnumber") || key == "number") return "204";
        if (key.Contains("firstname")) return "Ana";
        if (key.Contains("lastname")) return "Torres";
        if (key.Contains("hotelname")) return "Hotel Miraflores";
        if (key.Contains("roomtypename")) return "Doble";
        if (key.Contains("name")) return "Ana Torres";
        if (key.Contains("number")) return "204";
        if (key.Contains("role")) return "Reception";
        if (key.Contains("fromstatus")) return "Cleaning";
        if (key.Contains("tostatus")) return "Available";
        if (key.Contains("bookingstatus")) return "Confirmed";
        if (key.Contains("checkinstage")) return "InProgress";
        if (key.Contains("status")) return "Active";
        if (key.Contains("priority")) return "Normal";
        if (key.Contains("area")) return "Housekeeping";
        if (key == "method" || key.EndsWith("method", StringComparison.Ordinal)) return "Yape";
        if (key == "kind") return "Escalation";
        if (key == "action") return "Created";
        if (key.Contains("category")) return "Minibar";
        if (key.Contains("position")) return "Recepcionista";
        if (key.Contains("label")) return "Cambiar sábanas y toallas";
        if (key.Contains("shift") || key == "period") return "Morning";
        if (key == "scope") return "Hotel";
        if (key.Contains("recipient")) return "ana.torres@example.com";
        if (key.StartsWith("amenit", StringComparison.Ordinal)) return "WiFi";
        if (key.Contains("ruc") || key.Contains("customerdocument")) return "20601234567";
        if (key.Contains("recoverycode")) return "7F3K-9Q2M";
        if (key.Contains("disabledcategor")) return "message";
        if (key.Contains("concept")) return "Minibar";
        if (key.Contains("source")) return "Google";
        if (key.Contains("accommodationtype")) return "Hotel";
        if (key.Contains("roomsrange")) return "11-50";
        if (key == "outcome") return "Resolved";
        if (key == "secret") return "JBSWY3DPEHPK3PXP";
        if (key.Contains("otpauth")) return "otpauth://totp/RoomTrack:ana.torres%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=RoomTrack";
        if (key == "issuer") return "RoomTrack";
        if (key == "algorithm") return "SHA1";
        if (key == "instance") return "/api/v1/bookings/42";
        if (key == "type") return "Standard";
        if (key.Contains("currency")) return "PEN";
        if (key.Contains("country") || key.Contains("nationality")) return "PE";
        if (key.Contains("city")) return "Lima";
        if (key.Contains("postalcode")) return "15074";
        if (key.Contains("street")) return "Av. Larco 345";
        if (key.Contains("address") || key.Contains("location")) return "Av. Larco 345, Miraflores, Lima";
        if (key.Contains("date") || key.EndsWith("at", StringComparison.Ordinal)) return "2026-11-24T20:00:00+00:00";
        if (key == "body" || key.Contains("message")) return "Hola, ¿podrían traer dos toallas extra a la habitación 204?";
        if (key.Contains("comment")) return "Muy buena atención en recepción.";
        if (key.Contains("note")) return "Huésped llega a las 15:00, preparar cama adicional.";
        if (key.Contains("description") || key.Contains("summary")) return "Aire acondicionado no enfría";
        if (key.Contains("detail") || key.Contains("reason")) return "El huésped pidió cambiar la fecha de llegada.";
        if (key.Contains("title")) return "Cambio de toallas";
        return "texto";
    }

    private static int IntegerFor(string? name)
    {
        var key = (name ?? string.Empty).ToLowerInvariant();
        if (key.Contains("hotelid")) return 1;
        if (key.Contains("roomid")) return 101;
        if (key.Contains("bookingid")) return 42;
        if (key.Contains("userid") || key.EndsWith("byuserid", StringComparison.Ordinal)) return 10;
        if (key.Contains("night")) return 2;
        if (key.Contains("rating")) return 5;
        if (key.Contains("page") && !key.Contains("size")) return 1;
        if (key.Contains("size") || key.Contains("limit")) return 20;
        if (key.Contains("minutes")) return 30;
        if (key.Contains("hours")) return 24;
        if (key.Contains("count") || key.Contains("total")) return 3;
        return 1;
    }

    private static double NumberFor(string? name)
    {
        var key = (name ?? string.Empty).ToLowerInvariant();
        if (key.Contains("rate") || key.Contains("percent") || key.Contains("ratio")) return 0.85;
        if (key.Contains("rating") || key.Contains("average")) return 4.5;
        if (key.Contains("total")) return 360.00;
        return 180.00;
    }

    private static string KeyOf(string? name) =>
        (name ?? string.Empty).ToLowerInvariant() switch
        {
            var key when key.Contains("room") => "101",
            var key when key.Contains("error") => "field",
            _ => "clave"
        };

    private static string? Singular(string? name) =>
        name is { Length: > 1 } && name.EndsWith('s') ? name[..^1] : name;
}
