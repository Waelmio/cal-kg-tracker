using Microsoft.EntityFrameworkCore;

namespace WeightTracker.Api.Data;

public class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : AppDbContext(options);
