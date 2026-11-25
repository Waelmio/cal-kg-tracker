using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WeightTracker.Api.Data;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Integrations;

public enum CallbackOutcome { Connected, Denied, Expired, Failed }

/// <summary>Connecting and disconnecting accounts. Tokens are encrypted before they reach the database.</summary>
public class IntegrationService(
    AppDbContext db,
    IEnumerable<IIntegrationProvider> providers,
    IDataProtectionProvider protection,
    IMemoryCache cache,
    ILogger<IntegrationService> logger)
{
    // A connection in progress lives in memory: after a restart the user just starts again.
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(15);
    private sealed record Pending(AuthorizationRequest Request, string ReturnBase);

    private readonly IDataProtector _protector = protection.CreateProtector("WeightTracker.Integrations.Tokens.v1");

    public IIntegrationProvider Provider(string id) =>
        providers.FirstOrDefault(p => p.Id == id) ?? throw new IntegrationException($"There is no integration called {id}.", StatusCodes.Status404NotFound);

    public async Task<List<IntegrationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var connections = await db.IntegrationConnections.AsNoTracking().ToDictionaryAsync(c => c.Provider, ct);
        return providers.Select(p =>
        {
            var c = connections.GetValueOrDefault(p.Id);
            return new IntegrationDto(p.Id, p.Name, p.IsConfigured, p.RequiredSettings, p.Attribution,
                c?.Status, c?.ConnectedAt, c?.LastSyncAt, c?.LastSyncStatus, c?.LastError);
        }).ToList();
    }

    /// <param name="baseUrl">The address the user's browser reaches the app at.</param>
    public async Task<string> ConnectAsync(string id, string baseUrl, bool usePin, CancellationToken ct)
    {
        var provider = Provider(id);
        EnsureConfigured(provider);
        var request = await provider.BeginAuthorizationAsync(usePin ? null : $"{baseUrl}/api/integrations/{id}/callback", ct);
        cache.Set(usePin ? PinKey(id) : CallbackKey(id, request.RequestToken), new Pending(request, baseUrl), PendingLifetime);
        return request.AuthorizeUrl;
    }

    /// <summary>The provider sent the browser back. <c>ReturnBase</c> is where the connection was started, null if unknown.</summary>
    public async Task<(CallbackOutcome Outcome, string? ReturnBase)> CompleteCallbackAsync(string id, string? requestToken, string? verifier, CancellationToken ct)
    {
        var provider = Provider(id);
        var key = CallbackKey(id, requestToken ?? "");
        if (!cache.TryGetValue(key, out Pending? pending)) return (CallbackOutcome.Expired, null);
        cache.Remove(key);
        if (string.IsNullOrEmpty(verifier)) return (CallbackOutcome.Denied, pending!.ReturnBase);

        try
        {
            await CompleteAsync(provider, pending!, verifier, ct);
            return (CallbackOutcome.Connected, pending!.ReturnBase);
        }
        catch (IntegrationException e)
        {
            logger.LogWarning("Connecting {Provider} failed: {Error}", provider.Name, e.Message);
            return (CallbackOutcome.Failed, pending!.ReturnBase);
        }
    }

    public async Task VerifyPinAsync(string id, string pin, CancellationToken ct)
    {
        var provider = Provider(id);
        if (!cache.TryGetValue(PinKey(id), out Pending? pending))
            throw new IntegrationException("This code has expired. Ask for a new one.", StatusCodes.Status410Gone);
        await CompleteAsync(provider, pending!, pin.Trim(), ct);   // a mistyped code can be tried again
        cache.Remove(PinKey(id));
    }

    /// <summary>Forgets the token. Imported values stay.</summary>
    public async Task<bool> DisconnectAsync(string id, CancellationToken ct)
    {
        Provider(id);
        var connection = await db.IntegrationConnections.FirstOrDefaultAsync(c => c.Provider == id, ct);
        if (connection is null) return false;
        db.IntegrationConnections.Remove(connection);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public ProviderCredentials Decrypt(IntegrationConnection connection)
    {
        try
        {
            return new ProviderCredentials(_protector.Unprotect(connection.AccessToken), _protector.Unprotect(connection.AccessTokenSecret));
        }
        catch (CryptographicException)
        {
            throw new NeedsReconnectException("The stored connection can't be read: its encryption keys are gone. Connect again, and keep the keys folder on a volume.");
        }
    }

    public static void EnsureConfigured(IIntegrationProvider provider)
    {
        if (!provider.IsConfigured)
            throw new IntegrationException($"{provider.Name} is not set up: {string.Join(" and ", provider.RequiredSettings)} must be set on the server.", StatusCodes.Status409Conflict);
    }

    private async Task CompleteAsync(IIntegrationProvider provider, Pending pending, string verifier, CancellationToken ct)
    {
        var credentials = await provider.CompleteAuthorizationAsync(pending.Request.RequestToken, pending.Request.RequestTokenSecret, verifier, ct);
        var token = _protector.Protect(credentials.Token);
        var secret = _protector.Protect(credentials.Secret);

        // Connecting again replaces the previous connection, whatever state it was in.
        var connection = await db.IntegrationConnections.FirstOrDefaultAsync(c => c.Provider == provider.Id, ct);
        if (connection is null)
        {
            connection = new IntegrationConnection { Provider = provider.Id, AccessToken = token, AccessTokenSecret = secret };
            db.IntegrationConnections.Add(connection);
        }
        connection.AccessToken = token;
        connection.AccessTokenSecret = secret;
        connection.Status = ConnectionStatus.Connected;
        connection.ConnectedAt = DateTimeOffset.UtcNow;
        connection.LastSyncStatus = null;
        connection.LastSyncAt = null;
        connection.LastError = null;
        await db.SaveChangesAsync(ct);
    }

    private static string PinKey(string id) => $"integration:{id}:pin";
    private static string CallbackKey(string id, string requestToken) => $"integration:{id}:callback:{requestToken}";
}
