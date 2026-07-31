using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using WinMoe.Models;

namespace WinMoe.ViewModels;

/// <summary>
/// Process table row: wraps the immutable telemetry record with the display-only
/// icon state the Mole-style table needs (real app icons with initials fallback).
/// </summary>
public partial class ProcessRowViewModel : ObservableObject
{
    public ProcessRowViewModel(ProcessTelemetry process)
    {
        Process = process;
    }

    public ProcessTelemetry Process { get; }

    /// <summary>Cached PNG path resolved off the UI thread; applied on the UI thread.</summary>
    public string? IconPngPath { get; set; }

    [ObservableProperty]
    private ImageSource? iconSource;

    [ObservableProperty]
    private bool hasIcon;

    public bool HasNoIcon => !HasIcon;

    public string Name => Process.Name;

    public int ProcessId => Process.ProcessId;

    public string WorkingSetText => Process.WorkingSetText;

    public string CpuUsageText => Process.CpuUsageText;

    public double CpuBarWidth => Process.CpuBarWidth;

    public string PowerImpactText => Process.PowerImpactText;

    public string Initials => Process.Initials;

    public string PinGlyph => Process.PinGlyph;

    public bool IsPinned => Process.IsPinned;

    partial void OnHasIconChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoIcon));
    }
}
