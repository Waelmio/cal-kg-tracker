using System.Text.Json.Serialization;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Integrations;

/// <summary>A provider and the state of its connection. Never carries a token or a secret.</summary>
public record IntegrationDto(
    string Id,
    string Name,
    bool Configured,
    IReadOnlyList<string> RequiredSettings,
    ProviderAttribution? Attribution,
    ConnectionStatus? Status,              // null: not connected
    DateTimeOffset? ConnectedAt,
    DateTimeOffset? LastSyncAt,
    SyncStatus? LastSyncStatus,
    string? LastError);

public record ConnectDto(bool UsePin = false);
public record ConnectResultDto(string AuthorizeUrl);
public record VerifyDto(string Verifier);

/// <param name="Written">New or updated values.</param>
/// <param name="Unchanged">The app already had this value.</param>
/// <param name="Kept">Different, but logged by the user: left alone.</param>
public record FieldSyncResult(int Written, int Unchanged, int Kept);
public record SyncResultDto(DateOnly From, DateOnly To, FieldSyncResult Calories, FieldSyncResult Weight);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SyncRowKind { New, Conflict }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SyncChoice { Keep, Replace }

/// <summary>One value where the app and the provider differ.</summary>
/// <param name="AppValue">Null for a new row.</param>
/// <param name="SetInAdvance">The app's value was logged before its own day.</param>
/// <param name="Imported">The app's value was written by this provider's sync.</param>
public record SyncRow(DateOnly Date, LogField Field, SyncRowKind Kind, decimal? AppValue, decimal Value, bool SetInAdvance, bool Imported, SyncChoice Suggested);

public record PreviewRequestDto(DateOnly From, DateOnly To);

/// <param name="EqualCount">Values that are the same on both sides.</param>
/// <param name="MissingCount">Values of the app the provider has nothing for. They are never touched.</param>
public record PreviewDto(Guid PreviewId, DateOnly From, DateOnly To, List<SyncRow> Rows, int EqualCount, int MissingCount);

public record SyncDecision(DateOnly Date, LogField Field, SyncChoice Choice);
public record ApplyRequestDto(Guid PreviewId, List<SyncDecision> Decisions);

/// <param name="Changed">Skipped, because the day changed after the preview.</param>
public record ApplyResultDto(int Added, int Replaced, int Kept, int Changed);
