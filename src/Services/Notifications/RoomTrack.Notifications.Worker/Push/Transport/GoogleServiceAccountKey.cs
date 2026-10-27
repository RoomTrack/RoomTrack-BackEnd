using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoomTrack.Notifications.Worker.Push.Transport;

/// <summary>The fields of the JSON key of a Google service account that sign its access tokens.</summary>
public sealed record GoogleServiceAccountKey(string ClientEmail, string PrivateKeyPem, string TokenUri, string? ProjectId)
{
    public static GoogleServiceAccountKey Parse(string json)
    {
        var key = JsonSerializer.Deserialize<KeyFile>(json)
                  ?? throw new FormatException("The service account key is empty.");
        if (string.IsNullOrWhiteSpace(key.ClientEmail) || string.IsNullOrWhiteSpace(key.PrivateKey))
            throw new FormatException("The service account key needs client_email and private_key.");
        // Fails now (at startup) rather than on the first notification.
        using (var rsa = RSA.Create()) rsa.ImportFromPem(key.PrivateKey);
        return new GoogleServiceAccountKey(key.ClientEmail, key.PrivateKey,
            string.IsNullOrWhiteSpace(key.TokenUri) ? "https://oauth2.googleapis.com/token" : key.TokenUri, key.ProjectId);
    }

    private sealed record KeyFile(
        [property: JsonPropertyName("client_email")] string? ClientEmail,
        [property: JsonPropertyName("private_key")] string? PrivateKey,
        [property: JsonPropertyName("token_uri")] string? TokenUri,
        [property: JsonPropertyName("project_id")] string? ProjectId);
}

/// <summary>
///     OAuth 2.0 access tokens of a service account (the "JWT bearer" grant): a JWT signed with the key is exchanged
///     for an access token, reused until a few minutes before it expires.
/// </summary>
public sealed class GoogleAccessTokenProvider(HttpClient httpClient, GoogleServiceAccountKey key, string scope, TimeProvider timeProvider)
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && timeProvider.GetUtcNow() < _expiresAt - RefreshMargin) return _accessToken;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            if (_accessToken is not null && now < _expiresAt - RefreshMargin) return _accessToken;

            using var response = await httpClient.PostAsync(key.TokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = SignedAssertion(now)
            }), cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"The Google token endpoint answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}",
                    null, response.StatusCode);

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                        ?? throw new HttpRequestException("The Google token endpoint answered without a token.");
            _accessToken = token.AccessToken;
            _expiresAt = now.AddSeconds(token.ExpiresIn);
            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private string SignedAssertion(DateTimeOffset now)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = key.ClientEmail,
            scope,
            aud = key.TokenUri,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddHours(1).ToUnixTimeSeconds()
        }));
        using var rsa = RSA.Create();
        rsa.ImportFromPem(key.PrivateKeyPem);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{claims}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
