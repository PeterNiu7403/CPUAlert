namespace WinMoe.Services;

public interface IMoleEngineProbe
{
    IEnumerable<string> GetCandidatePaths();

    string? FindOnPath(string command);

    string ResolvePowerShellHost();
}
