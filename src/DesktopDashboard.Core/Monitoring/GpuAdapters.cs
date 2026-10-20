using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dxgi;

namespace DesktopDashboard.Core.Monitoring;

public sealed record GpuAdapterInfo(string Name, long Luid, ulong DedicatedVideoMemory)
{
    private const ulong IntegratedThreshold = 512UL * 1024 * 1024;

    /// <summary>
    /// Integrated GPUs get only a small BIOS carve-out as "dedicated" memory and otherwise use
    /// shared system RAM, so dedicated-VRAM figures are meaningless for them.
    /// </summary>
    public bool IsIntegrated => DedicatedVideoMemory < IntegratedThreshold;

    /// <summary>The LUID as it appears in GPU performance counter instance names.</summary>
    public string CounterLuid => $"luid_0x{(uint)(Luid >> 32):x8}_0x{(uint)Luid:x8}";
}

public static class GpuAdapters
{
    private const uint DxgiAdapterFlagSoftware = 2;

    /// <summary>Hardware adapters reported by DXGI (software renderers such as WARP are excluded).</summary>
    public static IReadOnlyList<GpuAdapterInfo> Enumerate()
    {
        var adapters = new List<GpuAdapterInfo>();
        PInvoke.CreateDXGIFactory1<IDXGIFactory1>(out var factory).ThrowOnFailure();
        try
        {
            for (uint i = 0; ; i++)
            {
                var hr = factory.EnumAdapters1(i, out var adapter);
                if (hr == HRESULT.DXGI_ERROR_NOT_FOUND)
                {
                    break;
                }

                hr.ThrowOnFailure();
                try
                {
                    var desc = adapter.GetDesc1();
                    if (((uint)desc.Flags & DxgiAdapterFlagSoftware) != 0)
                    {
                        continue;
                    }

                    var name = desc.Description.ToString().TrimEnd('\0').Trim();
                    var luid = ((long)desc.AdapterLuid.HighPart << 32) | desc.AdapterLuid.LowPart;
                    adapters.Add(new GpuAdapterInfo(name, luid, desc.DedicatedVideoMemory));
                }
                finally
                {
                    Marshal.ReleaseComObject(adapter);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }

        return adapters;
    }

    /// <summary>The adapter matching <paramref name="preferredName"/>, otherwise the one with the most dedicated memory.</summary>
    public static GpuAdapterInfo? Select(IReadOnlyList<GpuAdapterInfo> adapters, string? preferredName) =>
        adapters.FirstOrDefault(a => string.Equals(a.Name, preferredName, StringComparison.OrdinalIgnoreCase))
        ?? adapters.MaxBy(a => a.DedicatedVideoMemory);
}
