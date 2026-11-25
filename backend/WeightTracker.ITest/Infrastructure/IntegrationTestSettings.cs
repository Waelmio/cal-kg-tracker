using Microsoft.AspNetCore.Hosting;

namespace WeightTracker.ITest.Infrastructure;

public static class IntegrationTestSettings
{
    private static readonly string KeysPath = Path.Combine(Path.GetTempPath(), "weighttracker-itest-keys");

    /// <summary>No background sync, no keys left next to a shared temp file, and never the developer's own FatSecret keys.</summary>
    public static void UseIntegrationTestSettings(this IWebHostBuilder builder)
    {
        builder.UseSetting("DATA_PROTECTION_KEYS_PATH", KeysPath);
        builder.UseSetting("Integrations:SyncIntervalHours", "0");
        builder.UseSetting("FATSECRET_CONSUMER_KEY", "");
        builder.UseSetting("FATSECRET_CONSUMER_SECRET", "");
        builder.UseSetting("PUBLIC_BASE_URL", "");
    }
}
