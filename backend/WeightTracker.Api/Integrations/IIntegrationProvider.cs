using WeightTracker.Api.Models;

namespace WeightTracker.Api.Integrations;

/// <summary>What a provider has for one day. Null: nothing for that field.</summary>
public record ImportedDay(DateOnly Date, int? CaloriesKcal, decimal? WeightKg);

/// <summary>The long-lived token of a connected account. Only ever stored encrypted.</summary>
public record ProviderCredentials(string Token, string Secret);

/// <summary>A connection in progress: where to send the user, and the temporary token to finish with.</summary>
public record AuthorizationRequest(string AuthorizeUrl, string RequestToken, string RequestTokenSecret);

/// <summary>A link the provider's terms require next to its data.</summary>
public record ProviderAttribution(string Text, string Url);

/// <summary>
/// One source of calories and weight. Adding a provider: implement this, add its <see cref="LogSource"/> and register it
/// in Program.cs. Connecting, syncing, the API and the UI only know this interface.
/// </summary>
public interface IIntegrationProvider
{
    /// <summary>Used in URLs and in the database. Never change it.</summary>
    string Id { get; }
    string Name { get; }
    LogSource Source { get; }

    bool IsConfigured { get; }

    /// <summary>The environment variables to set, shown when the provider is not configured.</summary>
    IReadOnlyList<string> RequiredSettings { get; }
    ProviderAttribution? Attribution { get; }

    /// <param name="callbackUrl">Where the provider sends the browser back to. Null: it shows a PIN to type instead.</param>
    Task<AuthorizationRequest> BeginAuthorizationAsync(string? callbackUrl, CancellationToken ct);
    Task<ProviderCredentials> CompleteAuthorizationAsync(string requestToken, string requestTokenSecret, string verifier, CancellationToken ct);

    /// <summary>Days with something in the period, both ends included. Days the provider has nothing for are left out.</summary>
    /// <param name="today">Weights after it are never used, so there is no need to ask for them.</param>
    Task<IReadOnlyList<ImportedDay>> FetchAsync(ProviderCredentials credentials, DateOnly from, DateOnly to, DateOnly today, CancellationToken ct);
}
