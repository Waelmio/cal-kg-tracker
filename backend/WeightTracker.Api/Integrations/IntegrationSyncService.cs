using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WeightTracker.Api.Data;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Integrations;

/// <summary>One sync at a time: an automatic one, a manual one and a reviewed one must not overlap.</summary>
public sealed class SyncLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> AcquireAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        return new Releaser(_semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}

/// <summary>
/// Brings a provider's calories and weights into the daily logs. Everything that decides what may be written is in
/// <see cref="SyncRules"/>, and everything is written through <see cref="IDailyLogService"/>.
/// </summary>
public class IntegrationSyncService(
    AppDbContext db,
    IntegrationService integrations,
    IDailyLogService logs,
    SyncLock syncLock,
    IMemoryCache cache,
    IOptions<IntegrationOptions> options,
    TimeProvider clock)
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(30);
    private sealed record CachedPreview(string ProviderId, List<SyncRow> Rows);

    /// <summary>The automatic rules over the last months. Nothing is written when anything fails.</summary>
    public async Task<SyncResultDto> SyncAsync(string id, DateOnly today, CancellationToken ct)
    {
        var provider = integrations.Provider(id);
        using var _ = await syncLock.AcquireAsync(ct);
        var connection = await ConnectionAsync(provider, ct);
        try
        {
            var (from, to) = SyncRules.Window(today, options.Value.SyncMonths);
            var days = await FetchAsync(provider, connection, from, today, ct);     // nothing to import after today

            // Disconnected, or another account connected, in the meantime: the user doesn't want these anymore.
            if ((await ConnectionAsync(provider, ct)).AccessToken != connection.AccessToken)
                throw new IntegrationException("The connection changed during the sync. Sync again.", StatusCodes.Status409Conflict);

            var stored = await LogsAsync(from, today, ct);
            var (writes, calories, weight) = SyncRules.Plan(days, stored, provider.Source, today, options.Value.ImportTodayCalories);
            await logs.WriteFieldsAsync(writes, today);
            await UpdateAsync(connection, c =>
            {
                c.LastSyncAt = clock.GetUtcNow();
                c.LastSyncStatus = SyncStatus.Succeeded;
                c.LastError = null;
            });
            return new SyncResultDto(from, to, calories, weight);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await UpdateAsync(connection, c =>
            {
                c.LastSyncAt = clock.GetUtcNow();
                c.LastSyncStatus = SyncStatus.Failed;
                c.LastError = e is IntegrationException ? e.Message : "Something went wrong. The server's log says more.";
            });
            throw;
        }
    }

    /// <summary>The differences over a period. Writes nothing.</summary>
    public async Task<PreviewDto> PreviewAsync(string id, DateOnly from, DateOnly to, DateOnly today, CancellationToken ct)
    {
        var provider = integrations.Provider(id);
        if (to > today) to = today;
        if (from > to) throw new IntegrationException("The start date must be before the end date, and not in the future.", StatusCodes.Status400BadRequest);
        if (to > from.AddMonths(12)) throw new IntegrationException("The period can be up to 12 months.", StatusCodes.Status400BadRequest);

        var days = await FetchAsync(provider, await ConnectionAsync(provider, ct), from, to, ct);
        var (rows, equal, missing) = SyncRules.Compare(days, await LogsAsync(from, to, ct), provider.Source);

        var previewId = Guid.NewGuid();
        cache.Set(PreviewKey(previewId), new CachedPreview(provider.Id, rows), PreviewLifetime);
        return new PreviewDto(previewId, from, to, rows, equal, missing);
    }

    /// <summary>
    /// Writes what the user decided, all at once. Values come from the preview, never from the client. Rows without a
    /// decision are kept, and a row whose day changed since the preview is skipped.
    /// </summary>
    public async Task<ApplyResultDto> ApplyAsync(string id, ApplyRequestDto request, DateOnly today, CancellationToken ct)
    {
        var provider = integrations.Provider(id);
        using var _ = await syncLock.AcquireAsync(ct);
        if (!cache.TryGetValue(PreviewKey(request.PreviewId), out CachedPreview? preview) || preview!.ProviderId != id)
            throw new IntegrationException("This comparison has expired. Compare again.", StatusCodes.Status410Gone);

        var choices = request.Decisions.GroupBy(d => (d.Date, d.Field)).ToDictionary(g => g.Key, g => g.Last().Choice);
        var stored = preview.Rows.Count == 0
            ? []
            : await LogsAsync(preview.Rows.Min(r => r.Date), preview.Rows.Max(r => r.Date), ct);

        int added = 0, replaced = 0, kept = 0, changed = 0;
        var writes = new List<LogWrite>();
        foreach (var row in preview.Rows)
        {
            var log = stored.GetValueOrDefault(row.Date);
            if (SyncRules.AppValue(log, row.Field) != row.AppValue)
            {
                changed++;
                continue;
            }
            if (choices.GetValueOrDefault((row.Date, row.Field), SyncChoice.Keep) == SyncChoice.Replace)
            {
                writes.Add(new LogWrite(row.Date, row.Field, row.Value, provider.Source));
                if (row.Kind == SyncRowKind.New) added++; else replaced++;
                continue;
            }
            kept++;
            // Now the user's own value, so the next automatic sync leaves it alone. Values that are theirs already stay as they are.
            if (row.AppValue is { } value && SyncRules.IsOpen(log, row.Field, provider.Source))
                writes.Add(new LogWrite(row.Date, row.Field, value, LogSource.User));
        }

        await logs.WriteFieldsAsync(writes, today);
        cache.Remove(PreviewKey(request.PreviewId));
        return new ApplyResultDto(added, replaced, kept, changed);
    }

    private async Task<IntegrationConnection> ConnectionAsync(IIntegrationProvider provider, CancellationToken ct)
    {
        IntegrationService.EnsureConfigured(provider);
        var connection = await db.IntegrationConnections.AsNoTracking().FirstOrDefaultAsync(c => c.Provider == provider.Id, ct)
            ?? throw new IntegrationException($"{provider.Name} is not connected.", StatusCodes.Status409Conflict);
        if (connection.Status == ConnectionStatus.NeedsReconnect)
            throw new NeedsReconnectException($"{provider.Name} needs to be connected again.");
        return connection;
    }

    private async Task<IReadOnlyList<ImportedDay>> FetchAsync(IIntegrationProvider provider, IntegrationConnection connection, DateOnly from, DateOnly to, CancellationToken ct)
    {
        try
        {
            return await provider.FetchAsync(integrations.Decrypt(connection), from, to, ct);
        }
        catch (NeedsReconnectException e)
        {
            await UpdateAsync(connection, c =>
            {
                c.Status = ConnectionStatus.NeedsReconnect;
                c.LastError = e.Message;
            });
            throw;
        }
    }

    private async Task<Dictionary<DateOnly, DailyLog>> LogsAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.DailyLogs.AsNoTracking().Where(l => l.Date >= from && l.Date <= to).ToDictionaryAsync(l => l.Date, ct);

    /// <summary>Not when the account was disconnected or connected again in the meantime: that news is about the old one.</summary>
    private async Task UpdateAsync(IntegrationConnection used, Action<IntegrationConnection> change)
    {
        db.ChangeTracker.Clear();     // whatever failed must not be saved along
        var current = await db.IntegrationConnections.FirstOrDefaultAsync(c => c.Id == used.Id);
        if (current is null || current.AccessToken != used.AccessToken) return;
        change(current);
        await db.SaveChangesAsync();
    }

    private static string PreviewKey(Guid id) => $"integration:preview:{id}";
}
