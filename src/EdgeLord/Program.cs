using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Avalonia;

namespace EdgeLord;

class Program
{
    // Two copies would open every link twice.
    private static readonly string InstanceName = "EdgeLord-" + Environment.UserName;

    [STAThread]
    public static void Main(string[] args)
    {
        if (RestartedAsAdmin(args)) return;
        using var running = new Mutex(false, InstanceName, out var first);
        if (!first) return;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    // Admin gets instant word of Edge starting; without it some links slip through. Carries on unelevated if refused.
    private static bool RestartedAsAdmin(string[] args)
    {
        if (args.Contains("--no-admin")) return false;
        using (var identity = WindowsIdentity.GetCurrent())
            if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return false;
        // Under the debugger the process is dotnet.exe, which can't be restarted this way.
        var exe = Environment.ProcessPath;
        if (exe == null || !Path.GetFileName(exe).Equals("EdgeLord.exe", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            Process.Start(new ProcessStartInfo(exe, "--no-admin") { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch (Win32Exception) { return false; }
    }
}
