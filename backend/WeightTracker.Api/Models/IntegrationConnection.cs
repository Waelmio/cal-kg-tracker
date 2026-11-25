namespace WeightTracker.Api.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ConnectionStatus { Connected, NeedsReconnect }

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum SyncStatus { Succeeded, Failed }

/// <summary>The user's account at an integration provider. Never part of an export.</summary>
public class IntegrationConnection
{
    public int Id { get; set; }

    /// <summary>The provider's id, e.g. "fatsecret".</summary>
    public required string Provider { get; set; }

    /// <summary>Encrypted (ASP.NET Data Protection). Never returned by any endpoint.</summary>
    public required string AccessToken { get; set; }
    public required string AccessTokenSecret { get; set; }

    public ConnectionStatus Status { get; set; } = ConnectionStatus.Connected;
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The last attempt, successful or not.</summary>
    public DateTimeOffset? LastSyncAt { get; set; }
    public SyncStatus? LastSyncStatus { get; set; }
    public string? LastError { get; set; }
}
