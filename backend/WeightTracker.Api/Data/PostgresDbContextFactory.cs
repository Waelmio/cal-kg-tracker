using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WeightTracker.Api.Data;

public class PostgresDbContextFactory : IDesignTimeDbContextFactory<PostgresDbContext>
{
    public PostgresDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql("Host=localhost;Database=weighttracker;Username=postgres;Password=postgres")
            .Options;
        return new PostgresDbContext(options);
    }
}
