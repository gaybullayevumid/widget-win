using System.Runtime.InteropServices;

namespace DesktopDashboard.Core.Interop;

/// <summary>
/// Kernel-mode thunks (gdi32) used by Task Manager for GPU temperature. Vendor neutral, but only
/// drivers that implement WDDM 2.4+ adapter perf data return a temperature.
/// </summary>
internal static partial class D3dkmt
{
    public const int KMTQAITYPE_ADAPTERPERFDATA = 62;

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_OPENADAPTERFROMLUID
    {
        public LUID AdapterLuid;
        public uint hAdapter;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_CLOSEADAPTER
    {
        public uint hAdapter;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct D3DKMT_QUERYADAPTERINFO
    {
        public uint hAdapter;
        public int Type;
        public void* pPrivateDriverData;
        public uint PrivateDriverDataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_ADAPTER_PERFDATA
    {
        public uint PhysicalAdapterIndex;
        public ulong MemoryFrequency;
        public ulong MaxMemoryFrequency;
        public ulong MaxMemoryFrequencyOC;
        public ulong MemoryBandwidth;
        public ulong PCIEBandwidth;
        public uint FanRPM;
        public uint Power;

        /// <summary>Tenths of a degree Celsius; 0 when the driver does not report it.</summary>
        public uint Temperature;
        public byte PowerStateOverride;
    }

    [LibraryImport("gdi32.dll")]
    public static partial int D3DKMTOpenAdapterFromLuid(ref D3DKMT_OPENADAPTERFROMLUID open);

    [LibraryImport("gdi32.dll")]
    public static partial int D3DKMTQueryAdapterInfo(ref D3DKMT_QUERYADAPTERINFO query);

    [LibraryImport("gdi32.dll")]
    public static partial int D3DKMTCloseAdapter(ref D3DKMT_CLOSEADAPTER close);

    /// <summary>Temperature in °C, or null if the adapter cannot be opened or reports none.</summary>
    public static unsafe double? QueryTemperature(long luid)
    {
        var open = new D3DKMT_OPENADAPTERFROMLUID
        {
            AdapterLuid = new LUID { LowPart = (uint)luid, HighPart = (int)(luid >> 32) },
        };
        if (D3DKMTOpenAdapterFromLuid(ref open) != 0)
        {
            return null;
        }

        try
        {
            var perf = default(D3DKMT_ADAPTER_PERFDATA);
            var query = new D3DKMT_QUERYADAPTERINFO
            {
                hAdapter = open.hAdapter,
                Type = KMTQAITYPE_ADAPTERPERFDATA,
                pPrivateDriverData = &perf,
                PrivateDriverDataSize = (uint)sizeof(D3DKMT_ADAPTER_PERFDATA),
            };

            return D3DKMTQueryAdapterInfo(ref query) == 0 && perf.Temperature > 0
                ? perf.Temperature / 10.0
                : null;
        }
        finally
        {
            var close = new D3DKMT_CLOSEADAPTER { hAdapter = open.hAdapter };
            D3DKMTCloseAdapter(ref close);
        }
    }
}
