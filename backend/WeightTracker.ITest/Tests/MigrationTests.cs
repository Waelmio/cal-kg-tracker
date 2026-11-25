using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using WeightTracker.Api.Models;

namespace WeightTracker.ITest.Tests;

/// <summary>
/// Builds a database in the pre-redesign shape, inserts old-style rows, runs the redesign migration and checks
/// the data rules of PLAN.md section 4 (same rules as LegacyUpgrade.cs).
/// </summary>
public abstract class MigrationTestsBase
{
    private const string BeforeRedesign = "20260322000001_IntegerSettingsColumns";

    protected abstract AppDbContext CreateContext();
    protected abstract string Ts(string iso);

    [Fact]
    public async Task RedesignMigration_MigratesLegacyData()
    {
        await using var db = CreateContext();
        await db.Database.GetService<IMigrator>().MigrateAsync(BeforeRedesign);

        var created = Ts("2026-11-10T08:00:00");
        var later = Ts("2026-11-11T20:00:00");

        await Exec(db, $"INSERT INTO \"CalorieGoals\" (\"TargetCalories\", \"CreatedAt\") VALUES (1950, '{Ts("2026-06-01T00:00:00")}'), (1850, '{Ts("2026-10-15T09:00:00")}')");
        await Exec(db, $"UPDATE \"UserSettings\" SET \"TdeeKcal\" = 2350 WHERE \"Id\" = 1");

        string Log(string date, string weight, string cal, string c, string u, string cheat = "false", string notes = "NULL") =>
            $"INSERT INTO \"DailyLogs\" (\"Date\", \"WeightKg\", \"CaloriesKcal\", \"Notes\", \"IsCheatDay\", \"CreatedAt\", \"UpdatedAt\") VALUES ('{date}', {weight}, {cal}, {notes}, {cheat}, '{c}', '{u}')";
        string B(bool v) => db.Database.IsNpgsql() ? (v ? "true" : "false") : (v ? "1" : "0");

        // untouched auto-filled days (created before their date, never updated, equal to target, siblings share CreatedAt)
        await Exec(db, Log("2026-11-12", "NULL", "1850", created, created, B(false)));
        await Exec(db, Log("2026-11-13", "NULL", "1850", created, created, B(false)));
        // touched later by the user: kept
        await Exec(db, Log("2026-11-11", "NULL", "2100", created, later, B(false)));
        // has a weight and was updated: kept, calories stay
        await Exec(db, Log("2026-11-14", "80.5", "1850", created, later, B(false)));
        // created on its own date: not recognizable, kept
        var own = Ts("2026-11-09T07:00:00");
        await Exec(db, Log("2026-11-09", "NULL", "1850", own, own, B(false)));
        // auto-filled day carrying a note: row stays, calories cleared
        await Exec(db, Log("2026-11-15", "NULL", "1850", created, created, B(false), "'felt hungry'"));

        await Exec(db, $"INSERT INTO \"Goals\" (\"TargetWeightKg\", \"TargetDate\", \"StartingWeightKg\", \"StartDate\", \"CreatedAt\") VALUES (78, '2027-01-01', 88, '2026-06-01', '{Ts("2026-06-01T10:00:00")}'), (77, '2027-03-01', 86, '2026-09-10', '{Ts("2026-09-10T10:00:00")}')");

        await db.Database.MigrateAsync();

        var logs = await db.DailyLogs.OrderBy(l => l.Date).ToListAsync();
        // Nothing is deleted, auto-filled looking days included.
        Assert.Equal(new[] { "2026-11-09", "2026-11-11", "2026-11-12", "2026-11-13", "2026-11-14", "2026-11-15" }, logs.Select(l => l.Date.ToString("yyyy-MM-dd")));

        Assert.All(logs.Where(l => l.Date.Day != 11), l => Assert.Equal(1850, l.CaloriesKcal));
        Assert.Equal(2100, logs[1].CaloriesKcal);
        Assert.Equal(new DateOnly(2026, 11, 11), logs[1].CaloriesSetOn);
        Assert.Equal(new DateOnly(2026, 11, 10), logs[2].CaloriesSetOn);   // date of UpdatedAt
        Assert.Equal(80.5m, logs[4].WeightKg);
        Assert.Equal(1850, logs[5].CaloriesKcal);
        Assert.Equal("felt hungry", logs[5].Notes);

        // Everything stored before the integrations was the user's.
        Assert.All(logs, l => Assert.Equal(LogSource.User, l.CaloriesSource));
        Assert.Equal(LogSource.User, logs[4].WeightSource);
        Assert.All(logs.Where(l => l.WeightKg is null), l => Assert.Null(l.WeightSource));

        var targets = await db.CalorieGoals.OrderBy(g => g.EffectiveFrom).ToListAsync();
        Assert.Equal(new DateOnly(2026, 6, 1), targets[0].EffectiveFrom);
        Assert.Equal(new DateOnly(2026, 10, 15), targets[1].EffectiveFrom);

        var goals = await db.Goals.OrderBy(g => g.CreatedAt).ToListAsync();
        Assert.Equal(new DateOnly(2026, 9, 10), goals[0].EndedOn);
        Assert.Equal(GoalEndReason.Replaced, goals[0].EndReason);
        Assert.Equal(GoalKind.Reach, goals[0].Kind);
        Assert.Null(goals[1].EndedOn);
        Assert.Null(goals[1].EndReason);

        var tdee = Assert.Single(await db.TdeeEntries.ToListAsync());
        Assert.Equal(2350, tdee.Kcal);
        Assert.Equal(new DateOnly(2026, 11, 9), tdee.EffectiveFrom);   // earliest log before clearing
        Assert.Equal(TdeeSource.Manual, tdee.Source);

        var settings = await db.UserSettings.FindAsync(1);
        Assert.Equal(2, settings!.AveragingWeeks);
    }

    private static Task Exec(AppDbContext db, string sql) => db.Database.ExecuteSqlRawAsync(sql);
}

public class MigrationTests_Sqlite : MigrationTestsBase, IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"migration-{Guid.NewGuid()}.db");

    protected override AppDbContext CreateContext() =>
        new SqliteDbContext(new DbContextOptionsBuilder<SqliteDbContext>().UseSqlite($"Data Source={_path}").Options);

    protected override string Ts(string iso) => iso + ".0000000+00:00";

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }
}

public class MigrationTests_Postgres : MigrationTestsBase, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine").WithDatabase("migration_itest").WithUsername("postgres").WithPassword("postgres").Build();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    protected override AppDbContext CreateContext() =>
        new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(_container.GetConnectionString()).Options);

    protected override string Ts(string iso) => iso + "+00";
}
