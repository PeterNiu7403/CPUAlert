using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

/// <summary>Pure parsing/conversion helpers of the universal sensor probes.</summary>
public sealed class UniversalSensorProbeTests
{
    [Theory]
    [InlineData(2982, 25.05)]   // 0.1 K path: 298.2 K ≈ 25 °C
    [InlineData(3000, 26.85)]   // 0.1 K path
    [InlineData(250, 25.0)]     // 0.1 °C path
    [InlineData(900, 90.0)]     // 0.1 °C path, hot NVMe under load
    [InlineData(-300, -30.0)]   // sub-zero ambient (0.1 °C path)
    public void DecodeTenthDegree_PlausibleValues_Decode(short raw, double expectedCelsius)
    {
        var celsius = StorageTemperatureSensorProbe.DecodeTenthDegree(raw);

        Assert.NotNull(celsius);
        Assert.Equal(expectedCelsius, celsius.Value, precision: 2);
    }

    [Theory]
    [InlineData(unchecked((short)0x8000))]  // STORAGE_TEMPERATURE_VALUE_NOT_REPORTED
    [InlineData(20000)]                      // 2000 K / 2000 °C — impossible
    [InlineData(-5000)]                      // -500 °C — impossible
    public void DecodeTenthDegree_ImplausibleValues_Rejected(short raw)
    {
        Assert.Null(StorageTemperatureSensorProbe.DecodeTenthDegree(raw));
    }

    [Fact]
    public void KelvinToCelsius_Converts()
    {
        Assert.Equal(26.85, ThermalZoneSensorProbe.KelvinToCelsius(300.0), precision: 2);
        Assert.Equal(0.0, ThermalZoneSensorProbe.KelvinToCelsius(273.15), precision: 2);
    }

    [Fact]
    public void NvapiThermalSettingsVersion_MatchesStructLayout()
    {
        // NV_GPU_THERMAL_SETTINGS_V2 = version(4) + count(4) + sensor[3](20 each) = 68 bytes.
        Assert.Equal(68, NvidiaGpuSensorProbe.ThermalSettingsSize);
        Assert.Equal((uint)68 | (2u << 16), NvidiaGpuSensorProbe.ThermalSettingsV2Version);
    }
}
