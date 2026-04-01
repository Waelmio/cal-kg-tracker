namespace WeightTracker.ITest.Infrastructure;

public static class DbFixture
{
    public static async Task ResetDatabaseAsync(AppDbContext db)
    {
        db.DailyLogs.RemoveRange(db.DailyLogs);
        db.Goals.RemoveRange(db.Goals);
        db.CalorieGoals.RemoveRange(db.CalorieGoals);

        // UserSettings row Id=1 must always exist (seeded by migration HasData).
        // Reset it to defaults rather than deleting.
        var settings = await db.UserSettings.FindAsync(1);
        if (settings is not null)
        {
            settings.HeightCm = null;
            settings.PreferredUnit = "kg";
            settings.TdeeKcal = null;
        }

        await db.SaveChangesAsync();
    }
}
