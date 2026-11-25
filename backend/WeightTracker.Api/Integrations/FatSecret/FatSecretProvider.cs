using WeightTracker.Api.Models;

namespace WeightTracker.Api.Integrations.FatSecret;

public class FatSecretProvider(FatSecretClient client, FatSecretOptions options, TimeProvider clock) : IIntegrationProvider
{
    // FatSecret refuses too many calls in a short time, without saying how many. About 30 in a row were too many.
    private const int MaxCallsInARow = 12;
    private static readonly TimeSpan CallSpacing = TimeSpan.FromSeconds(1);

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

    public async Task<IReadOnlyList<ImportedDay>> FetchAsync(ProviderCredentials credentials, DateOnly from, DateOnly to, DateOnly today, CancellationToken ct)
    {
        var calories = new Dictionary<DateOnly, decimal>();
        var weights = new Dictionary<DateOnly, decimal>();
        var calls = new List<Func<Task>>();
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            var first = month;
            calls.Add(async () =>
            {
                foreach (var (date, value) in await client.GetCaloriesAsync(first, credentials, ct)) calories[date] = value;
            });
            if (first > today) continue;     // no weight in a month to come
            calls.Add(async () =>
            {
                foreach (var (date, value) in await client.GetWeightsAsync(first, credentials, ct)) weights[date] = value;
            });
        }

        for (var i = 0; i < calls.Count; i++)
        {
            if (i > 0 && calls.Count > MaxCallsInARow) await Task.Delay(CallSpacing, clock, ct);
            await calls[i]();
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
