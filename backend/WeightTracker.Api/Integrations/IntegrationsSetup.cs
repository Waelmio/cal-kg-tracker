using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WeightTracker.Api.Integrations.FatSecret;

namespace WeightTracker.Api.Integrations;

public static class IntegrationsSetup
{
    /// <param name="keysPath">Where the keys that encrypt the tokens are kept. Must survive a rebuild of the container.</param>
    public static void AddIntegrations(this IServiceCollection services, IConfiguration config, string keysPath)
    {
        services.Configure<IntegrationOptions>(config.GetSection("Integrations"));
        services.AddDataProtection().SetApplicationName("WeightTracker").PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        services.AddMemoryCache();
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton(FatSecretOptions.From(config));
        services.AddHttpClient<FatSecretClient>(c => c.Timeout = TimeSpan.FromSeconds(30))
            .RemoveAllLoggers();      // the URLs carry tokens and signatures
        services.AddTransient<IIntegrationProvider, FatSecretProvider>();

        services.AddSingleton<SyncLock>();
        services.AddScoped<IntegrationService>();
        services.AddScoped<IntegrationSyncService>();
        services.AddHostedService<IntegrationSyncBackgroundService>();
    }
}
