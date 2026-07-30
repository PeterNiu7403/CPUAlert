namespace MoleWindows.Models;

public sealed record HistoryRangeDefinition(
    string Key,
    string Label,
    TimeSpan Window);
