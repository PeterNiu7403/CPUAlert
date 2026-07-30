using MoleWindows.Services;

namespace MoleWindows.Models;

public sealed record PurgeArtifactCandidate(
    string Name,
    string Path,
    string Type,
    string Language,
    long SizeBytes)
{
    public string SizeText => SystemTelemetryFormatter.Bytes(SizeBytes);
}
