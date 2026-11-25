namespace WeightTracker.Api.Integrations.FatSecret;

public class FatSecretOptions
{
    public const string KeyVariable = "FATSECRET_CONSUMER_KEY";
    public const string SecretVariable = "FATSECRET_CONSUMER_SECRET";

    public string ConsumerKey { get; init; } = "";
    public string ConsumerSecret { get; init; } = "";
    public string AuthBaseUrl { get; init; } = "https://authentication.fatsecret.com/oauth";
    public string ApiBaseUrl { get; init; } = "https://platform.fatsecret.com/rest";

    public bool IsConfigured => ConsumerKey.Length > 0 && ConsumerSecret.Length > 0;

    public static FatSecretOptions From(IConfiguration config)
    {
        var defaults = new FatSecretOptions();
        return new FatSecretOptions
        {
            ConsumerKey = Clean(config[KeyVariable]),
            ConsumerSecret = Clean(config[SecretVariable]),
            // Only overridden to test against another server.
            AuthBaseUrl = config["FatSecret:AuthBaseUrl"] ?? defaults.AuthBaseUrl,
            ApiBaseUrl = config["FatSecret:ApiBaseUrl"] ?? defaults.ApiBaseUrl,
        };
    }

    // docker env files keep the quotes around a value.
    private static string Clean(string? value) => (value ?? "").Trim().Trim('\'', '"');
}
