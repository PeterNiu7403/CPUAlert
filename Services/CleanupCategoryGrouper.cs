using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Groups flat clean-preview rows into Mole-style category sections (header + children).
/// </summary>
public static class CleanupCategoryGrouper
{
    public sealed record CategoryGroup(
        string Category,
        IReadOnlyList<CleanupPreviewItem> Items,
        long TotalBytes,
        int SelectedCount,
        int DeletableSelectedCount);

    public static IReadOnlyList<CategoryGroup> Group(IEnumerable<CleanupPreviewItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .GroupBy(
                item => string.IsNullOrWhiteSpace(item.Category) ? "Cleanup" : item.Category.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var list = group.ToArray();
                var selected = list.Where(item => item.IsSelected).ToArray();
                var deletableSelected = selected.Count(item =>
                    OperationPlanValidator.IsConcreteDeletablePath(item.Path));
                return new CategoryGroup(
                    group.Key,
                    list,
                    list.Sum(item => item.SizeBytes),
                    selected.Length,
                    deletableSelected);
            })
            .OrderByDescending(group => group.TotalBytes)
            .ThenBy(group => group.Category, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
