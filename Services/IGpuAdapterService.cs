using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>Per-adapter GPU enumeration + 3D engine utilization (dGPU/iGPU split).</summary>
public interface IGpuAdapterService
{
    /// <summary>Adapters with current 3D utilization; empty when the counters are unavailable.</summary>
    IReadOnlyList<GpuAdapterTelemetry> CaptureAdapters();
}
