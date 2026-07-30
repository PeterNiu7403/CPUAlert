using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public partial class ApplicationRowViewModel : ObservableObject
{
    private int _iconLoadTicket;

    public ApplicationRowViewModel(InstalledApplication application)
    {
        Application = application;
        _ = LoadIconAsync();
    }

    public InstalledApplication Application { get; }

    public string Id => Application.Id;

    public string Name => Application.Name;

    public string VersionText => string.IsNullOrWhiteSpace(Application.Version)
        ? "版本未知"
        : Application.Version;

    public string Source => Application.Source;

    public string InstallLocation => string.IsNullOrWhiteSpace(Application.InstallLocation)
        ? "无安装路径"
        : Application.InstallLocation;

    public string Initials => Application.Initials;

    public string SizeText => Application.SizeText;

    public string DetailLine
    {
        get
        {
            var size = Application.SizeBytes <= 0 ? "大小未知" : SizeText;
            return $"{VersionText} · {size} · {Source}";
        }
    }

    public string RightSummary
    {
        get
        {
            if (!IsSelected)
            {
                return SizeText;
            }

            // Mole: "8 selected · 896.4 MB + 985.7 MB" — keep compact Chinese.
            return string.IsNullOrWhiteSpace(SelectionHint)
                ? $"已选 · {SizeText}"
                : SelectionHint;
        }
    }

    public string DetailLineWithActivity
    {
        get
        {
            var version = VersionText;
            var size = Application.SizeBytes <= 0 ? "大小未知" : SizeText;
            if (string.IsNullOrWhiteSpace(ActivityText))
            {
                return $"{version} · {size}";
            }

            return $"{version} · {size} · {ActivityText}";
        }
    }

    public Microsoft.UI.Xaml.Media.SolidColorBrush ActivityBrush =>
        string.Equals(ActivityText, "使用中", StringComparison.Ordinal)
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 91, 212, 142))
            : string.IsNullOrWhiteSpace(ActivityText)
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 122, 116, 108))
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 201, 160, 112));

    public string ChevronText => IsExpanded ? "review ▴" : "review ▾";

    public Visibility DetailVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;

    public Visibility InitialsVisibility => HasIcon ? Visibility.Collapsed : Visibility.Visible;

    public Visibility IconVisibility => HasIcon ? Visibility.Visible : Visibility.Collapsed;

    public SolidColorBrush CheckBackground => IsSelected
        ? new SolidColorBrush(Color.FromArgb(255, 224, 112, 64))
        : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

    public SolidColorBrush CheckBorder => IsSelected
        ? new SolidColorBrush(Color.FromArgb(255, 224, 112, 64))
        : new SolidColorBrush(Color.FromArgb(255, 122, 116, 108));

    public Visibility CheckMarkVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private string selectionHint = string.Empty;

    [ObservableProperty]
    private string activityText = string.Empty;

    [ObservableProperty]
    private ImageSource? iconSource;

    [ObservableProperty]
    private bool hasIcon;

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ChevronText));
        OnPropertyChanged(nameof(DetailVisibility));
    }

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(RightSummary));
        OnPropertyChanged(nameof(CheckBackground));
        OnPropertyChanged(nameof(CheckBorder));
        OnPropertyChanged(nameof(CheckMarkVisibility));
    }

    partial void OnSelectionHintChanged(string value)
    {
        OnPropertyChanged(nameof(RightSummary));
    }

    partial void OnActivityTextChanged(string value)
    {
        OnPropertyChanged(nameof(DetailLineWithActivity));
        OnPropertyChanged(nameof(ActivityBrush));
    }

    partial void OnHasIconChanged(bool value)
    {
        OnPropertyChanged(nameof(InitialsVisibility));
        OnPropertyChanged(nameof(IconVisibility));
    }

    private async Task LoadIconAsync()
    {
        var ticket = Interlocked.Increment(ref _iconLoadTicket);
        var hint = Application.IconPath;
        var direct = AppIconResolver.ResolveDirectImagePath(hint);
        if (direct is not null)
        {
            try
            {
                await ApplyIconOnUiAsync(() =>
                {
                    if (ticket != _iconLoadTicket)
                    {
                        return;
                    }

                    IconSource = new BitmapImage(new Uri(direct));
                    HasIcon = true;
                }).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or UriFormatException or ArgumentException)
            {
                // fall through to thumbnail path
            }
        }

        var path = AppIconResolver.NormalizeIconPath(hint);
        if (path is null || !File.Exists(path))
        {
            return;
        }

        // Fast path: disk cache from a previous session.
        var cached = AppIconResolver.TryGetFreshCachePath(path);
        if (cached is not null)
        {
            try
            {
                await ApplyIconOnUiAsync(() =>
                {
                    if (ticket != _iconLoadTicket)
                    {
                        return;
                    }

                    IconSource = new BitmapImage(new Uri(cached));
                    HasIcon = true;
                }).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or UriFormatException or ArgumentException)
            {
                // regenerate below
            }
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var thumb = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 64, ThumbnailOptions.ResizeThumbnail);
            if (thumb is null || ticket != _iconLoadTicket)
            {
                return;
            }

            // Copy stream so SetSource can run on the UI thread after this method resumes.
            var memory = new InMemoryRandomAccessStream();
            await RandomAccessStream.CopyAsync(thumb, memory);
            memory.Seek(0);

            // Persist PNG cache for next launch (best-effort).
            try
            {
                AppIconResolver.EnsureCacheDirectory();
                var cachePath = AppIconResolver.GetCachePath(path);
                memory.Seek(0);
                using (var outStream = File.Open(cachePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    var reader = new DataReader(memory.GetInputStreamAt(0));
                    var size = (uint)memory.Size;
                    await reader.LoadAsync(size);
                    var bytes = new byte[size];
                    reader.ReadBytes(bytes);
                    await outStream.WriteAsync(bytes);
                }

                memory.Seek(0);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                memory.Seek(0);
            }

            await ApplyIconOnUiAsync(async () =>
            {
                if (ticket != _iconLoadTicket)
                {
                    return;
                }

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(memory);
                IconSource = bitmap;
                HasIcon = true;
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            // Keep initials fallback.
        }
    }

    private static Task ApplyIconOnUiAsync(Action action)
    {
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (queue is null)
        {
            // Called from background construction; find the app UI queue.
            // When unavailable (unit tests), apply inline.
            try
            {
                action();
            }
            catch
            {
                // Ignore UI-only failures in tests.
            }

            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        if (!queue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetResult();
        }

        return tcs.Task;
    }

    private static Task ApplyIconOnUiAsync(Func<Task> action)
    {
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (queue is null)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        if (!queue.TryEnqueue(async () =>
            {
                try
                {
                    await action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetResult();
        }

        return tcs.Task;
    }
}
