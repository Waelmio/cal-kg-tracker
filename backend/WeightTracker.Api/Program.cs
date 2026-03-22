using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var databaseUrl = builder.Configuration["DATABASE_URL"];
var pgHost = builder.Configuration["POSTGRES_HOST"];

var isPostgres = !string.IsNullOrEmpty(databaseUrl) || !string.IsNullOrEmpty(pgHost);

if (isPostgres)
{
    builder.Services.AddDbContext<PostgresDbContext>(options =>
    {
        if (!string.IsNullOrEmpty(databaseUrl))
            options.UseNpgsql(databaseUrl);
        else
        {
            var pgDb = builder.Configuration["POSTGRES_DB"] ?? "weighttracker";
            var pgUser = builder.Configuration["POSTGRES_USER"] ?? "postgres";
            var pgPassword = builder.Configuration["POSTGRES_PASSWORD"] ?? "";
            options.UseNpgsql($"Host={pgHost};Database={pgDb};Username={pgUser};Password={pgPassword}");
        }
    });
    builder.Services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<PostgresDbContext>());
}
else
{
    builder.Services.AddDbContext<SqliteDbContext>(options =>
    {
        var dbPath = builder.Configuration["SQL_DB_PATH"] ?? "weighttracker.db";
        options.UseSqlite($"Data Source={dbPath}");
    });
    builder.Services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<SqliteDbContext>());
}

builder.Services.AddScoped<ICalorieLogService, CalorieLogService>();
builder.Services.AddScoped<IDailyLogService, DailyLogService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<ICalorieGoalService, CalorieGoalService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<ITdeeComputationService, TdeeComputationService>();
builder.Services.AddScoped<IDataService, DataService>();

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
