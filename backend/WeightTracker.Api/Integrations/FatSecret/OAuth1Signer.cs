using System.Security.Cryptography;
using System.Text;

namespace WeightTracker.Api.Integrations.FatSecret;

/// <summary>OAuth 1.0 HMAC-SHA1 signature (RFC 5849 section 3.4).</summary>
public static class OAuth1Signer
{
    /// <summary>RFC 3986 encoding: only letters, digits and "-._~" are left as they are.</summary>
    public static string Encode(string value) => Uri.EscapeDataString(value);

    /// <param name="parameters">All oauth_* parameters (without the signature), query and form parameters.</param>
    public static string Sign(string method, string url, IEnumerable<KeyValuePair<string, string>> parameters, string consumerSecret, string tokenSecret = "")
    {
        var normalized = string.Join('&', parameters
            .Select(p => (Key: Encode(p.Key), Value: Encode(p.Value)))
            .OrderBy(p => p.Key, StringComparer.Ordinal).ThenBy(p => p.Value, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value}"));
        var baseString = $"{method.ToUpperInvariant()}&{Encode(url)}&{Encode(normalized)}";
        var key = $"{Encode(consumerSecret)}&{Encode(tokenSecret)}";
        return Convert.ToBase64String(HMACSHA1.HashData(Encoding.ASCII.GetBytes(key), Encoding.ASCII.GetBytes(baseString)));
    }
}
