using System.Security.Cryptography;
using System.Text;

namespace WinMoe.Models;

public enum OperationRisk
{
    Low,
    Medium,
    High
}

public sealed record OperationPlanItem(
    string Id,
    string Title,
    string TargetPath,
    long SizeBytes,
    OperationRisk Risk,
    bool IsSelected = false);

public sealed record OperationPlan(
    string Id,
    string Kind,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Fingerprint,
    bool RequiresElevation,
    IReadOnlyList<OperationPlanItem> Items)
{
    public static OperationPlan Create(
        string kind,
        IEnumerable<OperationPlanItem> items,
        DateTimeOffset createdAtUtc,
        TimeSpan lifetime,
        bool requiresElevation = false)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Plan kind is required.", nameof(kind));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Plan lifetime must be positive.");
        }

        var materializedItems = items?.ToArray() ?? throw new ArgumentNullException(nameof(items));
        return new OperationPlan(
            Guid.NewGuid().ToString("N"),
            kind.Trim().ToLowerInvariant(),
            createdAtUtc,
            createdAtUtc.Add(lifetime),
            ComputeFingerprint(kind, materializedItems),
            requiresElevation,
            materializedItems);
    }

    public static string ComputeFingerprint(string kind, IEnumerable<OperationPlanItem> items)
    {
        var canonical = new StringBuilder(kind.Trim().ToLowerInvariant());
        foreach (var item in items.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            canonical
                .Append('\n')
                .Append(item.Id.Trim())
                .Append('|')
                .Append(NormalizePath(item.TargetPath))
                .Append('|')
                .Append(item.SizeBytes)
                .Append('|')
                .Append(item.Risk);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string NormalizePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path?.Trim() ?? string.Empty);
        if (expanded.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return Path
                .GetFullPath(expanded)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return expanded.ToUpperInvariant();
        }
    }
}

public enum BackendEventType
{
    Started,
    Progress,
    Item,
    Warning,
    Completed,
    Failed
}

public sealed record BackendEvent(
    BackendEventType Type,
    string OperationId,
    DateTimeOffset TimestampUtc,
    string Message,
    int? Completed = null,
    int? Total = null,
    string? ItemId = null,
    string? Code = null,
    bool Recoverable = false);
