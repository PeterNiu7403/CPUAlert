using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public partial class ActivityViewModel : ViewModelBase
{
    private readonly IOperationHistoryService _operationHistoryService;

    public ActivityViewModel(IOperationHistoryService operationHistoryService)
    {
        _operationHistoryService = operationHistoryService;
        HistoryPath = _operationHistoryService.HistoryFilePath;
    }

    public ObservableCollection<OperationHistoryEntry> Entries { get; } = new();

    [ObservableProperty]
    private string summary = "尚未载入活动";

    [ObservableProperty]
    private string historyPath = string.Empty;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var entries = await _operationHistoryService.ReadRecentAsync(50);
        RunOnUiThread(() =>
        {
            Entries.Clear();
            foreach (var entry in entries)
            {
                Entries.Add(entry);
            }

            Summary = entries.Count == 0 ? "尚无操作记录" : $"最近 {entries.Count} 次操作";
        });
    }
}
