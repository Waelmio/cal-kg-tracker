using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Integrations;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace WeightTracker.UTest.Integrations;

public class SyncTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 24);      // a Tuesday

    private sealed class FakeProvider : IIntegrationProvider
    {
        public string Id => "fatsecret";
        public string Name => "FatSecret";
        public LogSource Source => LogSource.FatSecret;
        public bool IsConfigured { get; set; } = true;
        public IReadOnlyList<string> RequiredSettings => ["A_KEY", "A_SECRET"];
        public ProviderAttribution? Attribution => null;

        public Dictionary<DateOnly, (int? Kcal, decimal? Kg)> Days { get; } = [];
        public Func<Task>? OnFetch { get; set; }
        public List<(DateOnly From, DateOnly To)> Fetched { get; } = [];
        public ProviderCredentials? UsedCredentials { get; private set; }
        public string? Callback { get; private set; }

        public Task<AuthorizationRequest> BeginAuthorizationAsync(string? callbackUrl, CancellationToken ct)
        {
            Callback = callbackUrl;
            return Task.FromResult(new AuthorizationRequest("https://authorize", "rt", "rts"));
        }

        public Task<ProviderCredentials> CompleteAuthorizationAsync(string requestToken, string requestTokenSecret, string verifier, CancellationToken ct) =>
            verifier == "1234" && (requestToken, requestTokenSecret) == ("rt", "rts")
                ? Task.FromResult(new ProviderCredentials("secret-token", "secret-secret"))
                : throw new IntegrationException("Invalid verifier");

        public async Task<IReadOnlyList<ImportedDay>> FetchAsync(ProviderCredentials credentials, DateOnly from, DateOnly to, CancellationToken ct)
        {
            UsedCredentials = credentials;
            Fetched.Add((from, to));
            if (OnFetch is not null) await OnFetch();
            return Days.Where(d => d.Key >= from && d.Key <= to).OrderBy(d => d.Key).Select(d => new ImportedDay(d.Key, d.Value.Kcal, d.Value.Kg)).ToList();
        }
    }

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly FakeProvider _provider = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly IntegrationOptions _options = new();
    private readonly DailyLogService _logs;
    private readonly IntegrationService _integrations;
    private readonly IntegrationSyncService _sync;

    public SyncTests()
    {
        _logs = new DailyLogService(_db);
        _integrations = new IntegrationService(_db, [_provider], _protection, _cache, NullLogger<IntegrationService>.Instance);
        _sync = new IntegrationSyncService(_db, _integrations, _logs, new SyncLock(), _cache, MsOptions.Create(_options), TimeProvider.System);
    }

    public void Dispose() => _db.Dispose();

    private async Task ConnectAsync()
    {
        await _integrations.ConnectAsync("fatsecret", "http://app", false, default);
        Assert.Equal(CallbackOutcome.Connected, (await _integrations.CompleteCallbackAsync("fatsecret", "rt", "1234", default)).Outcome);
    }

    private static DateOnly D(string monthDay) => DateOnly.Parse($"2026-{monthDay}");
    private void Remote(string day, int? kcal = null, decimal? kg = null) => _provider.Days[D(day)] = (kcal, kg);

    /// <summary>The user saves a day in the app.</summary>
    private Task<DailyLogDto?> User(string day, int? kcal = null, decimal? kg = null, string? notes = null, bool cheat = false, DateOnly? on = null) =>
        _logs.UpsertAsync(new UpsertDailyLogDto($"2026-{day}", kg, kcal, notes, cheat), on ?? Today);

    private Task<SyncResultDto> Sync(DateOnly? today = null) => _sync.SyncAsync("fatsecret", today ?? Today, default);
    private Task<DailyLog?> Log(string day) { var date = D(day); return _db.DailyLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Date == date); }
    private Task<IntegrationConnection> Connection() => _db.IntegrationConnections.AsNoTracking().SingleAsync();

    // ── Window ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2026-11-23", "2026-08-29", "2026-11-29")]   // Monday
    [InlineData("2026-11-29", "2026-08-29", "2026-11-29")]   // Sunday
    [InlineData("2026-05-25", "2026-02-28", "2026-05-31")]
    public void Window_EndsWithTheCurrentWeek_ThreeMonthsBack(string today, string from, string to) =>
        Assert.Equal((DateOnly.Parse(from), DateOnly.Parse(to)), SyncRules.Window(DateOnly.Parse(today), 3));

    // ── Automatic rules ───────────────────────────────────────────────────────

    [Fact]
    public async Task Sync_FillsEmptyDays_AndMarksThemAsImported()
    {
        await ConnectAsync();
        Remote("11-20", 1850, 82.35m);

        var result = await Sync();

        var log = (await Log("11-20"))!;
        Assert.Equal((1850, 82.35m, LogSource.FatSecret, LogSource.FatSecret, Today),
            (log.CaloriesKcal, log.WeightKg, log.CaloriesSource, log.WeightSource, log.CaloriesSetOn));
        Assert.Equal((new FieldSyncResult(1, 0, 0), new FieldSyncResult(1, 0, 0)), (result.Calories, result.Weight));
        Assert.Equal((D("08-29"), D("11-29")), Assert.Single(_provider.Fetched));
        Assert.Equal(new ProviderCredentials("secret-token", "secret-secret"), _provider.UsedCredentials);
    }

    [Fact]
    public async Task Sync_NeverOverwritesWhatTheUserLogged()
    {
        await ConnectAsync();
        await User("11-20", 2000, 83m, "note", cheat: true, on: D("11-20"));
        Remote("11-20", 1850, 82m);

        var result = await Sync();

        var log = (await Log("11-20"))!;
        Assert.Equal((2000, 83m, "note", true), (log.CaloriesKcal, log.WeightKg, log.Notes, log.IsCheatDay));
        Assert.Equal((1, 1), (result.Calories.Kept, result.Weight.Kept));
    }

    [Fact]
    public async Task Sync_ZeroKcalLoggedByTheUser_IsAValue()
    {
        await ConnectAsync();
        await User("11-20", 0, on: D("11-20"));
        Remote("11-20", 1850);
        await Sync();
        Assert.Equal(0, (await Log("11-20"))!.CaloriesKcal);
    }

    [Fact]
    public async Task Sync_ReplacesCaloriesSetInAdvance_OnlyOnceThereIsARealValue()
    {
        await ConnectAsync();
        await User("11-21", 2500, notes: "party", cheat: true, on: D("11-17"));

        await Sync();
        Assert.Equal(2500, (await Log("11-21"))!.CaloriesKcal);

        Remote("11-21", 2780);
        await Sync();
        var log = (await Log("11-21"))!;
        Assert.Equal((2780, "party", true, LogSource.FatSecret, Today), (log.CaloriesKcal, log.Notes, log.IsCheatDay, log.CaloriesSource, log.CaloriesSetOn));
    }

    [Fact]
    public async Task Sync_UpdatesWhatItWroteItself()
    {
        await ConnectAsync();
        Remote("11-24", 600, 82m);
        await Sync();
        Remote("11-24", 1900, 81.8m);
        await Sync();
        var log = (await Log("11-24"))!;
        Assert.Equal((1900, 81.8m), (log.CaloriesKcal, log.WeightKg));
    }

    [Fact]
    public async Task Sync_AUserEditProtectsAnImportedValue_ButSavingOnlyAWeightOrANoteDoesNot()
    {
        await ConnectAsync();
        Remote("11-22", 1500);
        Remote("11-23", 1500);
        await Sync();
        await User("11-22", 1700);                              // edits the calories
        await User("11-23", 1500, 82m, "weighed in");           // leaves them alone

        Remote("11-22", 1600);
        Remote("11-23", 1600, 81m);
        await Sync();

        Assert.Equal(1700, (await Log("11-22"))!.CaloriesKcal);
        var other = (await Log("11-23"))!;
        Assert.Equal((1600, 82m, "weighed in"), (other.CaloriesKcal, other.WeightKg, other.Notes));
    }

    [Fact]
    public async Task Sync_ADayMissingAtTheProvider_NeverDeletesAnything()
    {
        await ConnectAsync();
        Remote("11-20", 1850, 82m);
        await Sync();
        _provider.Days.Clear();
        await Sync();
        var log = (await Log("11-20"))!;
        Assert.Equal((1850, 82m), (log.CaloriesKcal, log.WeightKg));
    }

    [Fact]
    public async Task Sync_Again_TouchesNoRow()
    {
        await ConnectAsync();
        Remote("11-20", 1850, 82m);
        await Sync();
        var before = (await Log("11-20"))!;

        var result = await Sync(Today.AddDays(1));

        var after = (await Log("11-20"))!;
        Assert.Equal((before.UpdatedAt, before.CaloriesSetOn), (after.UpdatedAt, after.CaloriesSetOn));
        Assert.Equal((new FieldSyncResult(0, 1, 0), new FieldSyncResult(0, 1, 0)), (result.Calories, result.Weight));
        Assert.Equal(1, await _db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task Sync_FieldsAreIndependent_AndEmptyOnesCreateNothing()
    {
        await ConnectAsync();
        await User("11-19", 1900, on: D("11-19"));
        await User("11-20", kg: 83m, on: D("11-20"));
        Remote("11-19", null, 82m);
        Remote("11-20", 1700, 81m);
        Remote("11-21");

        await Sync();

        var a = (await Log("11-19"))!;
        var b = (await Log("11-20"))!;
        Assert.Equal((1900, 82m), (a.CaloriesKcal, a.WeightKg));
        Assert.Equal((1700, 83m), (b.CaloriesKcal, b.WeightKg));
        Assert.Null(await Log("11-21"));
    }

    [Fact]
    public async Task Sync_ImportsTheRestOfTheWeek_AsLoggedAhead()
    {
        await ConnectAsync();
        await User("11-26", 2500, notes: "party", cheat: true);     // logged ahead
        await User("11-27", 2200);                                  // logged ahead, not at the provider
        Remote("11-25", 1700, 82m);                                 // only planned in the app
        Remote("11-26", 1900);
        Remote("11-29", 2000);
        Remote("11-30", 2100);                                      // next week

        var result = await Sync();

        var planned = (await Log("11-25"))!;
        Assert.Equal((1700, null, LogSource.FatSecret, true), (planned.CaloriesKcal, planned.WeightKg, planned.CaloriesSource, SyncRules.SetInAdvance(planned)));
        var ahead = (await Log("11-26"))!;
        Assert.Equal((1900, "party", true, LogSource.FatSecret, true), (ahead.CaloriesKcal, ahead.Notes, ahead.IsCheatDay, ahead.CaloriesSource, SyncRules.SetInAdvance(ahead)));
        Assert.Equal(2200, (await Log("11-27"))!.CaloriesKcal);
        Assert.Equal(2000, (await Log("11-29"))!.CaloriesKcal);
        Assert.Null(await Log("11-30"));
        Assert.Equal((new FieldSyncResult(3, 0, 0), new FieldSyncResult(0, 0, 0)), (result.Calories, result.Weight));
    }

    [Fact]
    public async Task Sync_ALoggedAheadDay_FollowsTheProvider_UntilTheUserLogsItOnItsDay()
    {
        await ConnectAsync();
        Remote("11-26", 600);
        await Sync();
        await User("11-26", 2500);                                  // still ahead: the provider wins
        Remote("11-26", 1400);
        await Sync(D("11-25"));
        Assert.Equal(1400, (await Log("11-26"))!.CaloriesKcal);

        await User("11-26", 2300, on: D("11-26"));
        Remote("11-26", 1800);
        await Sync(D("11-26"));
        Assert.Equal(2300, (await Log("11-26"))!.CaloriesKcal);
    }

    [Theory]
    [InlineData(true, 700)]
    [InlineData(false, null)]
    public async Task Sync_CaloriesOfToday_AreOptional(bool import, int? expected)
    {
        _options.ImportTodayCalories = import;
        await ConnectAsync();
        Remote("11-24", 700, 82m);
        Remote("11-23", 1800);
        Remote("11-25", 700);
        await Sync();
        Assert.Equal(expected, (await Log("11-25"))?.CaloriesKcal);
        var log = (await Log("11-24"))!;
        Assert.Equal((expected, 82m), (log.CaloriesKcal, log.WeightKg));
        Assert.Equal(1800, (await Log("11-23"))!.CaloriesKcal);
    }

    // ── Connection state ──────────────────────────────────────────────────────

    [Fact]
    public async Task Sync_RecordsSuccessAndFailure_AndAFailureWritesNothing()
    {
        await ConnectAsync();
        Remote("11-20", 1850);
        _provider.OnFetch = () => throw new IntegrationException("FatSecret can't be reached.");
        await Assert.ThrowsAsync<IntegrationException>(() => Sync());

        var c = await Connection();
        Assert.Equal((ConnectionStatus.Connected, SyncStatus.Failed, "FatSecret can't be reached."), (c.Status, c.LastSyncStatus, c.LastError));
        Assert.NotNull(c.LastSyncAt);
        Assert.Empty(_db.DailyLogs);

        _provider.OnFetch = null;
        await Sync();
        c = await Connection();
        Assert.Equal((SyncStatus.Succeeded, (string?)null), (c.LastSyncStatus, c.LastError));
    }

    [Fact]
    public async Task Sync_AnUnexpectedError_IsRecordedWithoutItsDetails()
    {
        await ConnectAsync();
        _provider.OnFetch = () => throw new InvalidOperationException("token=abc");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sync());
        Assert.DoesNotContain("abc", (await Connection()).LastError);
    }

    [Fact]
    public async Task Sync_ARefusedToken_NeedsReconnect_AndIsNotTriedAgain()
    {
        await ConnectAsync();
        _provider.OnFetch = () => throw new NeedsReconnectException("Connect again.");
        await Assert.ThrowsAsync<NeedsReconnectException>(() => Sync());
        await Assert.ThrowsAsync<NeedsReconnectException>(() => Sync());

        Assert.Single(_provider.Fetched);
        Assert.Equal(ConnectionStatus.NeedsReconnect, (await Connection()).Status);

        _provider.OnFetch = null;
        await ConnectAsync();
        await Sync();
        Assert.Equal(ConnectionStatus.Connected, (await Connection()).Status);
    }

    [Fact]
    public async Task Sync_LostEncryptionKeys_NeedsReconnect()
    {
        await ConnectAsync();
        var other = new IntegrationService(_db, [_provider], new EphemeralDataProtectionProvider(), _cache, NullLogger<IntegrationService>.Instance);
        var sync = new IntegrationSyncService(_db, other, _logs, new SyncLock(), _cache, MsOptions.Create(_options), TimeProvider.System);
        await Assert.ThrowsAsync<NeedsReconnectException>(() => sync.SyncAsync("fatsecret", Today, default));
        Assert.Equal(ConnectionStatus.NeedsReconnect, (await Connection()).Status);
        Assert.Empty(_provider.Fetched);
    }

    [Fact]
    public async Task Sync_DisconnectedWhileFetching_WritesNothing()
    {
        await ConnectAsync();
        Remote("11-20", 1850);
        _provider.OnFetch = () => _integrations.DisconnectAsync("fatsecret", default);
        await Assert.ThrowsAsync<IntegrationException>(() => Sync());
        Assert.Empty(_db.DailyLogs);
        Assert.Empty(_db.IntegrationConnections);
    }

    [Fact]
    public async Task Sync_AnotherConnectionWhileFetching_WritesNothing_AndKeepsTheNewOne()
    {
        await ConnectAsync();
        Remote("11-20", 1850);
        _provider.OnFetch = () => ConnectAsync();
        await Assert.ThrowsAsync<IntegrationException>(() => Sync());
        Assert.Empty(_db.DailyLogs);
        Assert.Null((await Connection()).LastSyncStatus);
    }

    [Fact]
    public async Task Sync_NotConnectedOrNotConfigured_IsAConflict()
    {
        Assert.Equal(409, (await Assert.ThrowsAsync<IntegrationException>(() => Sync())).Status);
        _provider.IsConfigured = false;
        Assert.Contains("A_KEY and A_SECRET", (await Assert.ThrowsAsync<IntegrationException>(() => Sync())).Message);
        Assert.Equal(404, (await Assert.ThrowsAsync<IntegrationException>(() => _sync.SyncAsync("other", Today, default))).Status);
    }

    // ── Connecting ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Connect_StoresEncryptedTokens_AndTheListNeverShowsThem()
    {
        await ConnectAsync();
        Assert.Equal("http://app/api/integrations/fatsecret/callback", _provider.Callback);
        var c = await Connection();
        Assert.DoesNotContain("secret", c.AccessToken + c.AccessTokenSecret);

        var json = System.Text.Json.JsonSerializer.Serialize(await _integrations.GetAllAsync());
        Assert.DoesNotContain("Token", json);
        Assert.DoesNotContain(c.AccessToken, json);
    }

    [Fact]
    public async Task Callback_UnknownDeniedOrFailed_ConnectsNothing()
    {
        Assert.Equal((CallbackOutcome.Expired, null), await _integrations.CompleteCallbackAsync("fatsecret", "rt", "1234", default));
        await _integrations.ConnectAsync("fatsecret", "http://app", false, default);
        Assert.Equal((CallbackOutcome.Denied, "http://app"), await _integrations.CompleteCallbackAsync("fatsecret", "rt", null, default));
        await _integrations.ConnectAsync("fatsecret", "http://app", false, default);
        Assert.Equal((CallbackOutcome.Failed, "http://app"), await _integrations.CompleteCallbackAsync("fatsecret", "rt", "wrong", default));
        Assert.Equal(CallbackOutcome.Expired, (await _integrations.CompleteCallbackAsync("fatsecret", "rt", "1234", default)).Outcome);   // one use
        Assert.Empty(_db.IntegrationConnections);
    }

    [Fact]
    public async Task Pin_CanBeTypedAgainAfterAMistake()
    {
        Assert.Equal(410, (await Assert.ThrowsAsync<IntegrationException>(() => _integrations.VerifyPinAsync("fatsecret", "1234", default))).Status);
        await _integrations.ConnectAsync("fatsecret", "http://app", true, default);
        Assert.Null(_provider.Callback);
        await Assert.ThrowsAsync<IntegrationException>(() => _integrations.VerifyPinAsync("fatsecret", "1243", default));
        await _integrations.VerifyPinAsync("fatsecret", " 1234 ", default);
        Assert.Equal(ConnectionStatus.Connected, (await Connection()).Status);
    }

    [Fact]
    public async Task Disconnect_KeepsImportedValues()
    {
        await ConnectAsync();
        Remote("11-20", 1850);
        await Sync();
        Assert.True(await _integrations.DisconnectAsync("fatsecret", default));
        Assert.False(await _integrations.DisconnectAsync("fatsecret", default));
        Assert.Equal(1850, (await Log("11-20"))!.CaloriesKcal);
    }

    [Fact]
    public async Task ClearingAllData_DisconnectsToo_AndExportsNeverCarryTheConnection()
    {
        await ConnectAsync();
        var data = new DataService(_db);
        var exported = System.Text.Json.JsonSerializer.Serialize(await data.ExportAsync());
        Assert.DoesNotContain((await Connection()).AccessToken, exported);

        await data.ImportAsync(await data.ExportAsync());
        Assert.Single(_db.IntegrationConnections);     // restoring a backup keeps the connection
        await data.ClearAsync();
        Assert.Empty(_db.IntegrationConnections);
    }

    // ── Reviewing a period ────────────────────────────────────────────────────

    private Task<PreviewDto> Preview(string from = "11-01", string to = "11-24") => _sync.PreviewAsync("fatsecret", D(from), D(to), Today, default);
    private Task<ApplyResultDto> Apply(PreviewDto preview, params (string Day, LogField Field, SyncChoice Choice)[] decisions) =>
        _sync.ApplyAsync("fatsecret", new ApplyRequestDto(preview.PreviewId, decisions.Select(d => new SyncDecision(D(d.Day), d.Field, d.Choice)).ToList()), Today, default);

    [Fact]
    public async Task Preview_ListsTheDifferences_AndWritesNothing()
    {
        await ConnectAsync();
        Remote("11-04", 1500);
        await Sync(D("11-04"));
        Remote("11-04", 1600);                                   // corrected in the diary since
        await User("11-05", 2000, 82.34m, on: D("11-05"));
        Remote("11-05", 1900, 82.3m);                            // the weight only differs by rounding
        await User("11-06", 2500, on: D("11-02"));
        Remote("11-06", 2400);
        Remote("11-07", 1800, 81.9m);
        await User("11-08", 2000, 82m, on: D("11-08"));          // not at the provider
        await User("11-09", 1700, on: D("11-09"));
        Remote("11-09", 1700);                                   // the same
        var before = await _db.DailyLogs.AsNoTracking().OrderBy(l => l.Date).Select(l => new { l.Date, l.CaloriesKcal, l.WeightKg, l.UpdatedAt }).ToListAsync();

        var preview = await Preview();

        Assert.Equal(
            [
                new SyncRow(D("11-04"), LogField.Calories, SyncRowKind.Conflict, 1500, 1600, false, true, SyncChoice.Replace),
                new SyncRow(D("11-05"), LogField.Calories, SyncRowKind.Conflict, 2000, 1900, false, false, SyncChoice.Keep),
                new SyncRow(D("11-06"), LogField.Calories, SyncRowKind.Conflict, 2500, 2400, true, false, SyncChoice.Replace),
                new SyncRow(D("11-07"), LogField.Calories, SyncRowKind.New, null, 1800, false, false, SyncChoice.Replace),
                new SyncRow(D("11-07"), LogField.Weight, SyncRowKind.New, null, 81.9m, false, false, SyncChoice.Replace),
            ], preview.Rows);
        Assert.Equal((2, 2), (preview.EqualCount, preview.MissingCount));
        Assert.Equal(before, await _db.DailyLogs.AsNoTracking().OrderBy(l => l.Date).Select(l => new { l.Date, l.CaloriesKcal, l.WeightKg, l.UpdatedAt }).ToListAsync());
    }

    [Fact]
    public async Task Preview_IsCappedAtToday_AndAt12Months()
    {
        await ConnectAsync();
        Assert.Equal(Today, (await Preview(to: "12-31")).To);
        await _sync.PreviewAsync("fatsecret", Today.AddMonths(-12), Today, Today, default);
        Assert.Equal(400, (await Assert.ThrowsAsync<IntegrationException>(() => _sync.PreviewAsync("fatsecret", Today.AddMonths(-12).AddDays(-1), Today, Today, default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<IntegrationException>(() => Preview("11-25", "11-30"))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<IntegrationException>(() => Preview("11-10", "11-01"))).Status);
    }

    [Fact]
    public async Task Apply_WritesOnlyWhatWasReplaced()
    {
        await ConnectAsync();
        await User("11-05", 2000, notes: "note", on: D("11-05"));
        await User("11-06", 2100, on: D("11-06"));
        Remote("11-05", 1900);
        Remote("11-06", 1950);
        Remote("11-07", 1800, 81.9m);

        var result = await Apply(await Preview(),
            ("11-05", LogField.Calories, SyncChoice.Replace), ("11-06", LogField.Calories, SyncChoice.Keep),
            ("11-07", LogField.Calories, SyncChoice.Replace), ("11-07", LogField.Weight, SyncChoice.Keep));

        Assert.Equal(new ApplyResultDto(Added: 1, Replaced: 1, Kept: 2, Changed: 0), result);
        var a = (await Log("11-05"))!;
        Assert.Equal((1900, "note", LogSource.FatSecret), (a.CaloriesKcal, a.Notes, a.CaloriesSource));
        var b = (await Log("11-06"))!;
        Assert.Equal((2100, D("11-06"), LogSource.User), (b.CaloriesKcal, b.CaloriesSetOn, b.CaloriesSource));     // theirs already: not even touched
        var c = (await Log("11-07"))!;
        Assert.Equal((1800, (decimal?)null), (c.CaloriesKcal, c.WeightKg));
    }

    [Fact]
    public async Task Apply_KeptValuesSurviveTheNextAutomaticSync()
    {
        await ConnectAsync();
        Remote("11-04", 1500, 82m);
        await Sync(D("11-04"));                                  // imported
        await User("11-06", 2500, on: D("11-02"));               // set in advance
        Remote("11-04", 1600, 81m);
        Remote("11-06", 2400);

        var result = await Apply(await Preview(),
            ("11-04", LogField.Calories, SyncChoice.Keep), ("11-04", LogField.Weight, SyncChoice.Keep), ("11-06", LogField.Calories, SyncChoice.Keep));
        Assert.Equal(3, result.Kept);
        await Sync();

        var a = (await Log("11-04"))!;
        Assert.Equal((1500, 82m, LogSource.User, LogSource.User), (a.CaloriesKcal, a.WeightKg, a.CaloriesSource, a.WeightSource));
        var b = (await Log("11-06"))!;
        Assert.Equal((2500, Today), (b.CaloriesKcal, b.CaloriesSetOn));
    }

    [Fact]
    public async Task Apply_ReplacedValuesSurviveToo_AndFollowTheDiary()
    {
        await ConnectAsync();
        await User("11-05", 2000, on: D("11-05"));
        Remote("11-05", 1900);
        await Apply(await Preview(), ("11-05", LogField.Calories, SyncChoice.Replace));
        await Sync();
        Assert.Equal(1900, (await Log("11-05"))!.CaloriesKcal);
        Remote("11-05", 1950);
        await Sync();
        Assert.Equal(1950, (await Log("11-05"))!.CaloriesKcal);
    }

    [Fact]
    public async Task Apply_RowsWithoutADecisionAreKept()
    {
        await ConnectAsync();
        Remote("11-07", 1800);
        Assert.Equal(new ApplyResultDto(0, 0, 1, 0), await Apply(await Preview()));
        Assert.Null(await Log("11-07"));
    }

    [Fact]
    public async Task Apply_SkipsADayThatChangedSinceThePreview()
    {
        await ConnectAsync();
        await User("11-05", 2000, on: D("11-05"));
        Remote("11-05", 1900);
        Remote("11-07", 1800);
        var preview = await Preview();
        await User("11-05", 2050);
        await User("11-07", 1750);

        var result = await Apply(preview, ("11-05", LogField.Calories, SyncChoice.Replace), ("11-07", LogField.Calories, SyncChoice.Replace));

        Assert.Equal(new ApplyResultDto(0, 0, 0, 2), result);
        Assert.Equal((2050, 1750), ((await Log("11-05"))!.CaloriesKcal, (await Log("11-07"))!.CaloriesKcal));
    }

    [Fact]
    public async Task Apply_UsesThePreviewOnce_AndOnlyItsOwnRows()
    {
        await ConnectAsync();
        Remote("11-07", 1800);
        var preview = await Preview();
        Remote("11-07", 9999);       // what the user saw is what is written
        await Apply(preview, ("11-07", LogField.Calories, SyncChoice.Replace), ("11-08", LogField.Calories, SyncChoice.Replace));
        Assert.Equal(1800, (await Log("11-07"))!.CaloriesKcal);
        Assert.Null(await Log("11-08"));
        Assert.Equal(410, (await Assert.ThrowsAsync<IntegrationException>(() => Apply(preview))).Status);
    }
}
