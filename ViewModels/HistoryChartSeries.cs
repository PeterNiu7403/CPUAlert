using Microsoft.UI.Xaml.Media;

namespace WinMoe.ViewModels;

public sealed class HistoryChartSeries
{
    public HistoryChartSeries(string latestText, string averageText, PointCollection points, PointCollection? areaPoints = null)
    {
        LatestText = latestText;
        AverageText = averageText;
        Points = points;
        AreaPoints = areaPoints ?? [];
    }

    public string LatestText { get; }

    public string AverageText { get; }

    public PointCollection Points { get; }

    /// <summary>Closed polygon (line points + baseline corners) for a Mole-style area fill.</summary>
    public PointCollection AreaPoints { get; }

    public static HistoryChartSeries Empty(string latestText, string averageText)
    {
        return new HistoryChartSeries(latestText, averageText, []);
    }
}
