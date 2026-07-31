namespace WinMoe.Services;

/// <summary>
/// Formats the battery card's Mole-style surface: a design-capacity health badge
/// ("健康 96%") and a footer of small segments ("⚡65W · 7 次循环 · 预计剩余 2 小时").
/// Every input is nullable; missing data simply drops its segment — never fabricates.
/// </summary>
public static class BatteryDetailFormatter
{
    /// <summary>
    /// Design-capacity health in whole percent (clamped 1..100), or null when either
    /// capacity is unknown or the pair is implausible (full far above design).
    /// </summary>
    public static int? ComputeHealthPercent(long? designMwh, long? fullMwh)
    {
        if (designMwh is not > 0 || fullMwh is not > 0)
        {
            return null;
        }

        // New packs can drift a few percent above design; far above is firmware garbage.
        if (fullMwh.Value > designMwh.Value * 1.2)
        {
            return null;
        }

        var percent = (int)Math.Round(fullMwh.Value * 100.0 / designMwh.Value);
        return Math.Clamp(percent, 1, 100);
    }

    public static string BuildBadgeText(int? healthPercent, bool hasBattery)
    {
        if (!hasBattery)
        {
            return string.Empty;
        }

        return healthPercent is { } percent ? $"健康 {percent}%" : "健康";
    }

    public static string BuildFooterText(int? rateMw, int? cycleCount, string localizedStatus, string remainingText)
    {
        var segments = new List<string>(3);
        if (rateMw is { } rate && rate != 0)
        {
            // mW → whole W; sub-watt readings are dropped rather than shown as "0W".
            var watts = (int)Math.Round(Math.Abs(rate / 1000.0), MidpointRounding.AwayFromZero);
            if (watts > 0)
            {
                segments.Add($"⚡{watts}W");
            }
        }

        if (cycleCount is > 0)
        {
            segments.Add($"{cycleCount.Value} 次循环");
        }

        if (!string.IsNullOrWhiteSpace(remainingText))
        {
            segments.Add(remainingText);
        }

        // localizedStatus already sits next to the big percentage on the card, so it is
        // context for the caller, not a footer segment; all-empty input stays empty.
        _ = localizedStatus;
        return string.Join(" · ", segments);
    }
}
