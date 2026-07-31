using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Reads optional hardware sensors (temperatures, fan speeds) from whichever
/// providers the machine supports (brand WMI, GPU vendor APIs, ACPI thermal
/// zones, storage temperature). Implementations must never throw: an
/// unsupported machine simply returns <see cref="HardwareSensorSample.Unavailable"/>.
/// </summary>
public interface IHardwareSensorService
{
    HardwareSensorSample Capture();
}
