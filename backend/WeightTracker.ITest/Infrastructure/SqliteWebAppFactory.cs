using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WeightTracker.ITest.Infrastructure;

public class SqliteWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string _dbPath = null!;

    public Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"itest-{Guid.NewGuid()}.db");
        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("SQL_DB_PATH", _dbPath);
        builder.UseSetting("DATABASE_URL", "");
        builder.UseSetting("POSTGRES_HOST", "");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
