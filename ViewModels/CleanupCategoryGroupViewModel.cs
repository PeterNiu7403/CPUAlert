using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

/// <summary>
/// One expandable category section in the Clean review drawer (Mole-style tree).
/// </summary>
public partial class CleanupCategoryGroupViewModel : ObservableObject
{
    public CleanupCategoryGroupViewModel(string category, IEnumerable<CleanupPreviewItem> items)
    {
        Category = string.IsNullOrWhiteSpace(category) ? "Cleanup" : category.Trim();
        Items = new ObservableCollection<CleanupPreviewItem>(items);
        foreach (var item in Items)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CleanupPreviewItem.IsSelected))
                {
                    RefreshSelectionState();
                }
            };
        }

        RefreshSelectionState();
    }

    public string Category { get; }

    public ObservableCollection<CleanupPreviewItem> Items { get; }

    public long TotalBytes => Items.Sum(item => item.SizeBytes);

    public string SizeText => SystemTelemetryFormatter.Bytes(TotalBytes);

    public string CountText => $"{Items.Count} 项";

    public string HeaderSummary => $"{CountText} · {SizeText}";

    public string ChevronGlyph => IsExpanded ? "\uE70D" : "\uE76C";

    public Visibility ChildrenVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    private bool isExpanded = true;

    /// <summary>True / false / null (mixed) for the category checkbox.</summary>
    [ObservableProperty]
    private bool? isGroupSelected = true;

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ChevronGlyph));
        OnPropertyChanged(nameof(ChildrenVisibility));
    }

    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    public void ApplyGroupSelection(bool selected)
    {
        foreach (var item in Items)
        {
            item.IsSelected = selected;
        }

        IsGroupSelected = selected;
    }

    public void RefreshSelectionState()
    {
        if (Items.Count == 0)
        {
            IsGroupSelected = false;
            return;
        }

        var selected = Items.Count(item => item.IsSelected);
        IsGroupSelected = selected == 0
            ? false
            : selected == Items.Count
                ? true
                : null;

        OnPropertyChanged(nameof(TotalBytes));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HeaderSummary));
    }
}
