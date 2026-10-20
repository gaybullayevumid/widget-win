using System.Runtime.InteropServices;

namespace DesktopDashboard.Core.Interop;

/// <summary>Minimal Performance Data Helper (pdh.dll) bindings.</summary>
internal static partial class Pdh
{
    public const uint PDH_FMT_LARGE = 0x00000400;
    public const uint PDH_FMT_DOUBLE = 0x00000200;
    public const uint PDH_FMT_NOCAP100 = 0x00008000;
    public const uint PDH_CSTATUS_VALID_DATA = 0x00000000;
    public const uint PDH_CSTATUS_NEW_DATA = 0x00000001;
    public const uint PDH_MORE_DATA = 0x800007D2;
    public const uint PDH_NO_DATA = 0x800007D5;
    public const uint PDH_INVALID_DATA = 0xC0000BC6;

    [StructLayout(LayoutKind.Explicit)]
    public struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double DoubleValue;
        [FieldOffset(8)] public long LargeValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_FMT_COUNTERVALUE_ITEM
    {
        public nint Name;
        public PDH_FMT_COUNTERVALUE Value;
    }

    [LibraryImport("pdh.dll", EntryPoint = "PdhOpenQueryW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhOpenQuery(string? dataSource, nint userData, out nint query);

    [LibraryImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhAddEnglishCounter(nint query, string counterPath, nint userData, out nint counter);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhCollectQueryData(nint query);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhGetFormattedCounterValue(nint counter, uint format, out uint type, out PDH_FMT_COUNTERVALUE value);

    [LibraryImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW")]
    public static partial uint PdhGetFormattedCounterArray(nint counter, uint format, ref uint bufferSize, out uint itemCount, nint itemBuffer);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhCloseQuery(nint query);
}

/// <summary>A PDH query that owns its counters. Not thread-safe; use from one thread.</summary>
internal sealed class PdhQuery : IDisposable
{
    private nint _handle;

    private PdhQuery(nint handle) => _handle = handle;

    public static PdhQuery Open()
    {
        Check(Pdh.PdhOpenQuery(null, 0, out var handle), "PdhOpenQuery");
        return new PdhQuery(handle);
    }

    /// <summary>Adds a counter by its English path, which works on any display language.</summary>
    public nint AddCounter(string englishPath)
    {
        Check(Pdh.PdhAddEnglishCounter(_handle, englishPath, 0, out var counter), $"PdhAddEnglishCounter({englishPath})");
        return counter;
    }

    /// <summary>Returns false when there is nothing to collect yet (e.g. no matching instances).</summary>
    public bool Collect()
    {
        var status = Pdh.PdhCollectQueryData(_handle);
        if (status == Pdh.PDH_NO_DATA)
        {
            return false;
        }

        Check(status, "PdhCollectQueryData");
        return true;
    }

    /// <summary>The counter value, or null if PDH has no valid value yet (rate counters need two samples).</summary>
    public static double? GetDouble(nint counter, bool allowAbove100 = false)
    {
        var format = Pdh.PDH_FMT_DOUBLE | (allowAbove100 ? Pdh.PDH_FMT_NOCAP100 : 0);
        var status = Pdh.PdhGetFormattedCounterValue(counter, format, out _, out var value);
        return status == 0 && IsValid(value.CStatus) ? value.DoubleValue : null;
    }

    /// <summary>All instances of a wildcard counter. Instances without valid data are skipped.</summary>
    public static unsafe List<(string Instance, double Value)> GetArray(nint counter, uint format)
    {
        var results = new List<(string, double)>();
        uint size = 0;
        var status = Pdh.PdhGetFormattedCounterArray(counter, format, ref size, out _, 0);
        if (status != Pdh.PDH_MORE_DATA || size == 0)
        {
            return results;
        }

        var buffer = NativeMemory.Alloc(size);
        try
        {
            status = Pdh.PdhGetFormattedCounterArray(counter, format, ref size, out var count, (nint)buffer);
            if (status != 0)
            {
                return results;
            }

            var items = (Pdh.PDH_FMT_COUNTERVALUE_ITEM*)buffer;
            for (var i = 0; i < count; i++)
            {
                if (!IsValid(items[i].Value.CStatus))
                {
                    continue;
                }

                var name = Marshal.PtrToStringUni(items[i].Name) ?? "";
                var value = (format & Pdh.PDH_FMT_LARGE) != 0 ? items[i].Value.LargeValue : items[i].Value.DoubleValue;
                results.Add((name, value));
            }
        }
        finally
        {
            NativeMemory.Free(buffer);
        }

        return results;
    }

    private static bool IsValid(uint cstatus) => cstatus is Pdh.PDH_CSTATUS_VALID_DATA or Pdh.PDH_CSTATUS_NEW_DATA;

    private static void Check(uint status, string operation)
    {
        if (status != 0)
        {
            throw new PdhException(operation, status);
        }
    }

    public void Dispose()
    {
        if (_handle != 0)
        {
            Pdh.PdhCloseQuery(_handle);
            _handle = 0;
        }
    }
}

public sealed class PdhException(string operation, uint status)
    : Exception($"{operation} failed with PDH status 0x{status:X8}")
{
    public uint Status { get; } = status;
}
