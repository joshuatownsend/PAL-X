using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Pal.Ingestion.Blg;

[SupportedOSPlatform("windows")]
internal static class PdhInterop
{
    // PDH status codes
    internal const int PDH_NO_MORE_DATA = unchecked((int)0x800007D5);
    internal const int PDH_MORE_DATA = unchecked((int)0x800007D0);
    internal const int PDH_INSUFFICIENT_BUFFER = unchecked((int)0x800007D2);
    internal const int PDH_NO_DATA = unchecked((int)0xC0000BEB);

    // PDH format flags
    internal const int PDH_FMT_DOUBLE = 0x00000200;
    internal const int PDH_FMT_NOCAP100 = 0x00008000;

    // PDH_CSTATUS values that indicate a valid sample
    internal const uint PDH_CSTATUS_VALID_DATA = 0x00000000;
    internal const uint PDH_CSTATUS_NEW_DATA = 0x00000001;

    // PDH_FMT_COUNTERVALUE — x64 layout: CStatus at offset 0, doubleValue at offset 8.
    // The 4-byte padding between CStatus and the 8-byte union is required by x64 ABI alignment.
    // If this code ever runs as x86, offset 8 shifts to 4 — but .NET 8 on Windows is x64-only.
    [StructLayout(LayoutKind.Explicit)]
    internal struct PdhFmtCountervalue
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double doubleValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    internal static extern int PdhBindInputDataSourceW(out IntPtr phDataSource, string szLogFileNameList);

    [DllImport("pdh.dll")]
    internal static extern int PdhCloseLog(IntPtr hLog, int dwFlags);

    [DllImport("pdh.dll")]
    internal static extern int PdhOpenQueryH(IntPtr hDataSource, IntPtr dwUserData, out IntPtr phQuery);

    [DllImport("pdh.dll")]
    internal static extern int PdhCloseQuery(IntPtr hQuery);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    internal static extern int PdhAddCounterW(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

    [DllImport("pdh.dll")]
    internal static extern int PdhCollectQueryDataWithTime(IntPtr hQuery, out long pllTimeStamp);

    [DllImport("pdh.dll")]
    internal static extern int PdhGetFormattedCounterValue(
        IntPtr hCounter, int dwFormat, out int lpdwType, out PdhFmtCountervalue pValue);

    // Two-call probe pattern: pass null buffer to get required size (returns PDH_INSUFFICIENT_BUFFER), then fill.
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    internal static extern int PdhExpandWildCardPathHW(
        IntPtr hDataSource,
        string szWildCardPath,
        char[]? mszExpandedPathList,
        ref int pcchPathListLength,
        int dwFlags);

    internal static void ThrowIfFailed(int hr, string operation)
    {
        if (hr != 0)
            throw new InvalidOperationException($"PDH '{operation}' failed: 0x{hr:X8}");
    }
}
