using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinMoe.Models;

/// <summary>
/// The unified result envelope that molewindows-cli (<c>molewindows.exe</c>) writes to stdout
/// for every command. Success and failure share ONE shape — branch on <see cref="Ok"/>,
/// then read <see cref="Data"/> (success) or <see cref="Error"/> (failure).
/// Stable structured result contract used by the optional Windows conductor.
/// </summary>
public sealed record WinMoeEnvelope
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("command")]
    public string? Command { get; init; }

    [JsonPropertyName("molewindows_cli")]
    public string? WinMoeCli { get; init; }

    /// <summary>The engine payload on success (absent/undefined on failure).</summary>
    [JsonPropertyName("data")]
    public JsonElement Data { get; init; }

    /// <summary>The structured error on failure (null on success): kind + message + platform.</summary>
    [JsonPropertyName("error")]
    public WinMoeError? Error { get; init; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parse <c>molewindows.exe</c> stdout into an envelope.
    /// Throws <see cref="JsonException"/> on non-JSON / empty output.
    /// </summary>
    public static WinMoeEnvelope Parse(string stdout)
        => JsonSerializer.Deserialize<WinMoeEnvelope>(stdout, Options)
           ?? throw new JsonException("empty molewindows envelope");
}

/// <summary>
/// The structured error payload on a failure envelope (molewindows-cli#4): a
/// machine-readable <see cref="Kind"/> (permission_denied / unsupported / not_found /
/// process_failed / error), the human <see cref="Message"/>, and the
/// <see cref="Platform"/> it occurred on.
/// </summary>
public sealed record WinMoeError
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("platform")]
    public string? Platform { get; init; }
}
