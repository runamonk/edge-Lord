using System.Runtime.InteropServices;

namespace EdgeLord;

// Reads another process's command line straight from Windows; asking WMI is too slow to beat Edge.
internal static class CommandLine
{
    private const uint QueryLimitedInformation = 0x1000;
    private const int ProcessCommandLineInformation = 60;

    public static string? Read(int processId)
    {
        var process = OpenProcess(QueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero) return null;
        try
        {
            // Fails once the process is on its way out, which is how an Edge that handed its link to an open Edge looks.
            NtQueryInformationProcess(process, ProcessCommandLineInformation, IntPtr.Zero, 0, out var size);
            if (size <= 0) return null;
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (NtQueryInformationProcess(process, ProcessCommandLineInformation, buffer, size, out _) != 0) return null;
                // A UNICODE_STRING: length in bytes, then a pointer to the text, which sits later in the same buffer.
                var length = Marshal.ReadInt16(buffer);
                var text = Marshal.ReadIntPtr(buffer, IntPtr.Size);
                return Marshal.PtrToStringUni(text, length / 2);
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { CloseHandle(process); }
    }

    // Cheaper than Process.GetProcessById, which matters when checking every new window.
    public static bool IsEdge(int processId)
    {
        var process = OpenProcess(QueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero) return false;
        try
        {
            var path = new char[1024];
            var length = path.Length;
            return QueryFullProcessImageName(process, 0, path, ref length)
                && Path.GetFileName(new string(path, 0, length)).Equals("msedge.exe", StringComparison.OrdinalIgnoreCase);
        }
        finally { CloseHandle(process); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, [Out] char[] path, ref int length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr process, int infoClass, IntPtr info, int infoLength, out int returnLength);
}
