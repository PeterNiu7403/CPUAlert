using WinMoe.Services;

namespace WinMoe.Models;

public sealed record PurgeArtifactCandidate(
    string Name,
    string Path,
    string Type,
    string Language,
    long SizeBytes)
{
    public string SizeText => SystemTelemetryFormatter.Bytes(SizeBytes);
}
