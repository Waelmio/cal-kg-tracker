using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeightTracker.Api.Data;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Integrations;

/// <summary>Syncs every connected provider every few hours, counted from its last sync: a restart alone makes no calls. Never takes the app down.</summary>
public class IntegrationSyncBackgroundService(
    IServiceScopeFactory scopes,
    IOptions<IntegrationOptions> options,
    TimeProvider clock,
    ILogger<IntegrationSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private TimeSpan Interval => TimeSpan.FromHours(options.Value.SyncIntervalHours);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.SyncIntervalHours <= 0)
        {
            logger.LogInformation("Automatic sync is off (Integrations:SyncIntervalHours).");
            return;
        }

        try
        {
            await Task.Delay(await FirstDelayAsync(stoppingToken), clock, stoppingToken);
            using var timer = new PeriodicTimer(Interval, clock);
            do await RunAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    /// <summary>Until the first sync is due. Shortly after startup when a provider was never synced or its last sync failed.</summary>
    internal async Task<TimeSpan> FirstDelayAsync(CancellationToken ct)
    {
        try
        {
            var now = clock.GetUtcNow();
            var delay = (await ConnectedAsync(ct))
                .Select(c => c is { LastSyncStatus: SyncStatus.Succeeded, LastSyncAt: { } last } ? last + Interval - now : TimeSpan.Zero)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Min();
            if (delay > Interval) delay = Interval;     // the clock was set back
            return delay < StartupDelay ? StartupDelay : delay;
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogError(e, "Reading the last sync failed.");
            return StartupDelay;
        }
    }

    internal async Task RunAsync(CancellationToken ct)
    {
        try
        {
            foreach (var connection in await ConnectedAsync(ct)) await SyncAsync(connection.Provider, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogError(e, "Automatic sync failed.");
        }
    }

    private async Task<List<IntegrationConnection>> ConnectedAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configured = scope.ServiceProvider.GetServices<IIntegrationProvider>().Where(p => p.IsConfigured).Select(p => p.Id).ToList();
        return await db.IntegrationConnections.AsNoTracking()
            .Where(c => c.Status == ConnectionStatus.Connected && configured.Contains(c.Provider))
            .ToListAsync(ct);
    }

    private async Task SyncAsync(string id, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            // Nobody's browser is here to tell the date: the server's time zone decides (TZ in Docker).
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var result = await scope.ServiceProvider.GetRequiredService<IntegrationSyncService>().SyncAsync(id, today, ct);
            logger.LogInformation("Synced {Provider}: {Calories} calories and {Weights} weights written.", id, result.Calories.Written, result.Weight.Written);
        }
        catch (IntegrationException e)
        {
            logger.LogWarning("Syncing {Provider} failed: {Error}", id, e.Message);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogError(e, "Syncing {Provider} failed.", id);
        }
    }
}
