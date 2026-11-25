using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeightTracker.Api.Data;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Integrations;

/// <summary>Syncs every connected provider shortly after startup, then every few hours. Never takes the app down.</summary>
public class IntegrationSyncBackgroundService(
    IServiceScopeFactory scopes,
    IOptions<IntegrationOptions> options,
    TimeProvider clock,
    ILogger<IntegrationSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.SyncIntervalHours <= 0)
        {
            logger.LogInformation("Automatic sync is off (Integrations:SyncIntervalHours).");
            return;
        }

        try
        {
            await Task.Delay(StartupDelay, clock, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(options.Value.SyncIntervalHours), clock);
            do await RunAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    internal async Task RunAsync(CancellationToken ct)
    {
        try
        {
            List<string> connected;
            using (var scope = scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var configured = scope.ServiceProvider.GetServices<IIntegrationProvider>().Where(p => p.IsConfigured).Select(p => p.Id).ToList();
                connected = await db.IntegrationConnections
                    .Where(c => c.Status == ConnectionStatus.Connected && configured.Contains(c.Provider))
                    .Select(c => c.Provider).ToListAsync(ct);
            }
            foreach (var id in connected) await SyncAsync(id, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogError(e, "Automatic sync failed.");
        }
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
