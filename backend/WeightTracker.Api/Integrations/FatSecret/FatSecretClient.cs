using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WeightTracker.Api.Integrations.FatSecret;

/// <summary>The calls to FatSecret: OAuth 1.0 in three steps, and the monthly calories and weights.</summary>
public class FatSecretClient(HttpClient http, FatSecretOptions options, TimeProvider clock)
{
    private static readonly DateOnly Epoch = new(1970, 1, 1);

    public async Task<(string Token, string Secret)> RequestTokenAsync(string callback, CancellationToken ct) =>
        ParseToken(await AuthAsync(HttpMethod.Post, "request_token", new() { ["oauth_callback"] = callback }, null, ct));

    public string AuthorizeUrl(string requestToken) =>
        $"{options.AuthBaseUrl}/authorize?oauth_token={OAuth1Signer.Encode(requestToken)}";

    public async Task<(string Token, string Secret)> AccessTokenAsync(string requestToken, string requestSecret, string verifier, CancellationToken ct) =>
        ParseToken(await AuthAsync(HttpMethod.Get, "access_token", new() { ["oauth_verifier"] = verifier }, new(requestToken, requestSecret), ct));

    /// <summary>Calories per day of the month the date is in.</summary>
    public Task<Dictionary<DateOnly, decimal>> GetCaloriesAsync(DateOnly month, ProviderCredentials credentials, CancellationToken ct) =>
        GetMonthAsync("food-entries/month/v2", "calories", month, credentials, ct);

    /// <summary>Weight in kg per day of the month the date is in.</summary>
    public Task<Dictionary<DateOnly, decimal>> GetWeightsAsync(DateOnly month, ProviderCredentials credentials, CancellationToken ct) =>
        GetMonthAsync("weight/month/v2", "weight_kg", month, credentials, ct);

    private async Task<Dictionary<DateOnly, decimal>> GetMonthAsync(string path, string field, DateOnly month, ProviderCredentials credentials, CancellationToken ct)
    {
        var date = (month.DayNumber - Epoch.DayNumber).ToString(CultureInfo.InvariantCulture);
        var (status, body) = await SendAsync(HttpMethod.Get, $"{options.ApiBaseUrl}/{path}", new() { ["date"] = date, ["format"] = "json" }, credentials, ct);
        return ParseMonth(body, field, status);
    }

    /// <summary>
    /// A single day comes as an object instead of an array, numbers come as strings, dates as days since 1970,
    /// and days (or whole months) without entries are left out.
    /// </summary>
    internal static Dictionary<DateOnly, decimal> ParseMonth(string body, string field, int status = 200)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch (JsonException) { throw new IntegrationException($"FatSecret sent an answer that could not be read (HTTP {status})."); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error)) throw Error(error, status);
            if (status is < 200 or > 299 || root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("month", out var month))
                throw new IntegrationException($"FatSecret sent an unexpected answer (HTTP {status}).");

            var result = new Dictionary<DateOnly, decimal>();
            if (month.ValueKind != JsonValueKind.Object || !month.TryGetProperty("day", out var days)) return result;

            var list = days.ValueKind == JsonValueKind.Array ? days.EnumerateArray().ToList() : [days];
            foreach (var day in list)
            {
                if (Number(day, "date_int") is not { } dateInt || Number(day, field) is not { } value) continue;
                result[Epoch.AddDays((int)dateInt)] = value;
            }
            return result;
        }
    }

    private static decimal? Number(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var e)) return null;
        var text = e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText();
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static IntegrationException Error(JsonElement error, int status)
    {
        var code = (int?)Number(error, "code");
        var message = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var m) ? m.ToString() : $"HTTP {status}";
        return code switch
        {
            5 or 8 => new IntegrationException($"FatSecret refused the request: {message}. Check {FatSecretOptions.KeyVariable} and {FatSecretOptions.SecretVariable}: they must be the OAuth 1.0 Consumer Key and Secret."),
            6 => new IntegrationException($"FatSecret refused the request: {message}. Check the clock of the server."),
            9 => new NeedsReconnectException("FatSecret no longer accepts the connection. Connect again."),
            12 => new IntegrationException($"FatSecret refused the request: {message}. Try again later."),
            21 => new IntegrationException($"FatSecret refused this server's IP address ({message}). Add its public IP under \"Manage API Keys\" on platform.fatsecret.com."),
            _ => new IntegrationException($"FatSecret error {code}: {message}"),
        };
    }

    private async Task<string> AuthAsync(HttpMethod method, string path, Dictionary<string, string> extra, ProviderCredentials? credentials, CancellationToken ct)
    {
        var (status, body) = await SendAsync(method, $"{options.AuthBaseUrl}/{path}", extra, credentials, ct);
        if (status is >= 200 and <= 299) return body;
        // Plain text, such as "Invalid signature".
        var reason = body.Trim() is { Length: > 0 and <= 200 } text && !text.StartsWith('<') ? text : $"HTTP {status}";
        throw new IntegrationException($"FatSecret refused the connection: {reason}. Check {FatSecretOptions.KeyVariable} and {FatSecretOptions.SecretVariable} (the OAuth 1.0 Consumer Key and Secret), then try again.");
    }

    private static (string, string) ParseToken(string body)
    {
        var values = QueryHelpers.ParseQuery(body);
        if (values.TryGetValue("oauth_token", out var token) && values.TryGetValue("oauth_token_secret", out var secret)
            && !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(secret))
            return (token!, secret!);
        throw new IntegrationException("FatSecret sent an answer without a token.");
    }

    private async Task<(int Status, string Body)> SendAsync(HttpMethod method, string url, Dictionary<string, string> extra, ProviderCredentials? credentials, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>(extra)
        {
            ["oauth_consumer_key"] = options.ConsumerKey,
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["oauth_nonce"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)),
            ["oauth_version"] = "1.0",
        };
        if (credentials is not null) parameters["oauth_token"] = credentials.Token;
        parameters["oauth_signature"] = OAuth1Signer.Sign(method.Method, url, parameters, options.ConsumerSecret, credentials?.Secret ?? "");

        var encoded = string.Join('&', parameters.Select(p => $"{OAuth1Signer.Encode(p.Key)}={OAuth1Signer.Encode(p.Value)}"));
        using var request = method == HttpMethod.Get
            ? new HttpRequestMessage(method, $"{url}?{encoded}")
            : new HttpRequestMessage(method, url) { Content = new StringContent(encoded, null, "application/x-www-form-urlencoded") };
        try
        {
            using var response = await http.SendAsync(request, ct);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
        }
        catch (HttpRequestException e) { throw new IntegrationException("FatSecret can't be reached.", inner: e); }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested) { throw new IntegrationException("FatSecret took too long to answer.", inner: e); }
    }
}
