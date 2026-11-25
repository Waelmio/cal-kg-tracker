using WeightTracker.Api.Models;

namespace WeightTracker.Api.Integrations.FatSecret;

public class FatSecretProvider(FatSecretClient client, FatSecretOptions options) : IIntegrationProvider
{
    public string Id => "fatsecret";
    public string Name => "FatSecret";
    public LogSource Source => LogSource.FatSecret;
    public bool IsConfigured => options.IsConfigured;
    public IReadOnlyList<string> RequiredSettings => [FatSecretOptions.KeyVariable, FatSecretOptions.SecretVariable];

    // Required by the terms of the free tier.
    public ProviderAttribution Attribution => new("Powered by fatsecret", "https://platform.fatsecret.com");

    public async Task<AuthorizationRequest> BeginAuthorizationAsync(string? callbackUrl, CancellationToken ct)
    {
        var (token, secret) = await client.RequestTokenAsync(callbackUrl ?? "oob", ct);
        return new AuthorizationRequest(client.AuthorizeUrl(token), token, secret);
    }

    public async Task<ProviderCredentials> CompleteAuthorizationAsync(string requestToken, string requestTokenSecret, string verifier, CancellationToken ct)
    {
        var (token, secret) = await client.AccessTokenAsync(requestToken, requestTokenSecret, verifier, ct);
        return new ProviderCredentials(token, secret);
    }

    public async Task<IReadOnlyList<ImportedDay>> FetchAsync(ProviderCredentials credentials, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var calories = new Dictionary<DateOnly, decimal>();
        var weights = new Dictionary<DateOnly, decimal>();
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            foreach (var (date, value) in await client.GetCaloriesAsync(month, credentials, ct)) calories[date] = value;
            foreach (var (date, value) in await client.GetWeightsAsync(month, credentials, ct)) weights[date] = value;
        }

        return calories.Keys.Union(weights.Keys)
            .Where(d => d >= from && d <= to)
            .Order()
            .Select(d => new ImportedDay(
                d,
                // Same limits as a value logged by hand.
                calories.TryGetValue(d, out var kcal) && kcal is >= 0 and <= 30000 ? (int)Math.Round(kcal, MidpointRounding.AwayFromZero) : null,
                weights.TryGetValue(d, out var kg) && kg is >= 1 and <= 700 ? Math.Round(kg, 2, MidpointRounding.AwayFromZero) : null))
            .Where(d => d.CaloriesKcal is not null || d.WeightKg is not null)
            .ToList();
    }
}
