using WinMoe.Models;

namespace WinMoe.Services;

public sealed class OperationPlanValidator : IOperationPlanValidator
{
    public OperationPlanValidationResult ValidateForApply(
        OperationPlan plan,
        IReadOnlyList<OperationPlanItem> currentItems,
        bool userConfirmed,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(currentItems);

        if (!userConfirmed)
        {
            return Reject(
                OperationPlanValidationCode.ConfirmationRequired,
                "The user must explicitly confirm this plan.");
        }

        if (nowUtc > plan.ExpiresAtUtc)
        {
            return Reject(
                OperationPlanValidationCode.Expired,
                "The plan expired and must be scanned again.");
        }

        var selectedItems = plan.Items.Where(item => item.IsSelected).ToArray();
        if (selectedItems.Length == 0)
        {
            return Reject(
                OperationPlanValidationCode.EmptySelection,
                "The plan does not contain a selected item.");
        }

        var currentFingerprint = OperationPlan.ComputeFingerprint(plan.Kind, currentItems);
        if (!string.Equals(plan.Fingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            return Reject(
                OperationPlanValidationCode.ContentChanged,
                "The targets changed after preview; create a new plan.");
        }

        var unsafeTarget = selectedItems.FirstOrDefault(item => IsUnsafeTarget(item.TargetPath));
        if (unsafeTarget is not null)
        {
            return Reject(
                OperationPlanValidationCode.UnsafeTarget,
                $"Blocked unsafe target: {unsafeTarget.TargetPath}");
        }

        return new OperationPlanValidationResult(
            OperationPlanValidationCode.Valid,
            "The plan is current and explicitly confirmed.");
    }

    public static bool IsConcreteDeletablePath(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            if (!Path.IsPathFullyQualified(expanded) || IsUnsafeTarget(expanded))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(expanded);
            return File.Exists(fullPath) || Directory.Exists(fullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return false;
        }
    }

    internal static bool IsUnsafeTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        try
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(path.Trim());
            if (expandedPath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return true;
            }

            if (!Path.IsPathFullyQualified(expandedPath))
            {
                return true;
            }

            var fullPath = Path.GetFullPath(expandedPath);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return true;
            }

            var normalizedFullPath = TrimTrailingSeparators(fullPath);
            var normalizedRoot = TrimTrailingSeparators(root);
            if (string.Equals(normalizedFullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var windowsPath = Environment
                .GetFolderPath(Environment.SpecialFolder.Windows)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return windowsPath.Length > 0 &&
                   (string.Equals(normalizedFullPath, windowsPath, StringComparison.OrdinalIgnoreCase) ||
                    normalizedFullPath.StartsWith(windowsPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return true;
        }
    }

    private static string TrimTrailingSeparators(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 0 ? path : trimmed;
    }

    private static OperationPlanValidationResult Reject(
        OperationPlanValidationCode code,
        string message)
    {
        return new OperationPlanValidationResult(code, message);
    }
}
