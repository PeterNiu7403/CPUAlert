namespace WinMoe.Models;

public sealed record LeftoverRemovalResult(
    string Path,
    bool Succeeded,
    string Message,
    long SizeBytes);
