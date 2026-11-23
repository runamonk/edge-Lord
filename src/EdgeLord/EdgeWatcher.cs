using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace EdgeLord;

// Watches msedge.exe starting. Links go to the default browser; an Edge started without one gets closed,
// because an Edge left running swallows links before they can be redirected.
internal sealed class EdgeWatcher(Settings settings) : IDisposable
{
    private enum EdgeState { Closing, Asking, Allowed }

    private readonly Lock gate = new();
    private readonly CloseLimit closeLimit = new();
    private ManagementEventWatcher? watcher;
    private EdgeState state = EdgeState.Closing;
    private Timer? allowedTimer;
    // Each Edge process is handled once, whichever of the trace or the window hook sees it first.
    private readonly Dictionary<int, DateTime> seen = [];
    private IntPtr windowHook;
    private WinEventProc? windowHookProc;

    // The link, and whether Edge was closed in time (false means Edge probably opened it too).
    public event Action<string, bool>? Redirected;

    // Edge keeps coming back; the user decides with KeepClosing or LetEdgeRun.
    public event Action? AskAboutEdge;

    public bool IsRunning => watcher != null;

    // True when Windows tells us about each new process as it starts (admin only), rather than us polling.
    public bool IsInstant { get; private set; }

    public void Start()
    {
        if (watcher != null) return;
        try
        {
            watcher = Watch(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName = 'msedge.exe'"), OnEdgeStartTrace);
            IsInstant = true;
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            // Polling misses an Edge that hands the link to an already open Edge, which takes about 35ms.
            watcher = Watch(new WqlEventQuery("__InstanceCreationEvent", TimeSpan.FromMilliseconds(500),
                "TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = 'msedge.exe'"), OnEdgeFound);
            IsInstant = false;
        }
        // Edge makes its window before showing it, and this hears about it straight away, unlike WMI.
        windowHookProc = OnWindowCreated;
        windowHook = SetWinEventHook(EventObjectCreate, EventObjectCreate, IntPtr.Zero, windowHookProc, 0, 0, WinEventOutOfContext | WinEventSkipOwnProcess);
        Log.Write(IsInstant ? "Watching for Edge." : "Watching for Edge by polling; run as admin to catch it every time.");
        // An Edge that was already open would take links just the same. Its command line may still hold
        // the link it was first opened with, long since handled, so it only gets closed, never redirected.
        if (!EdgeIsDefault())
            foreach (var processId in OpenEdgeBrowsers())
            {
                lock (gate) seen[processId] = DateTime.UtcNow;
                EdgeStartedWithoutLink(processId);
            }
    }

    public void Stop()
    {
        if (watcher == null) return;
        watcher.Stop();
        watcher.Dispose();
        watcher = null;
        if (windowHook != IntPtr.Zero) UnhookWinEvent(windowHook);
        windowHook = IntPtr.Zero;
        windowHookProc = null;
        lock (gate)
        {
            EndAllowed();
            seen.Clear();
        }
    }

    public void Dispose() => Stop();

    public void KeepClosing()
    {
        lock (gate) state = EdgeState.Closing;
        foreach (var processId in OpenEdgeBrowsers()) TryClose(processId);
        Log.Write("Told to keep closing Edge.");
    }

    // Until Edge is closed for good, then back to closing it.
    public void LetEdgeRun()
    {
        lock (gate)
        {
            state = EdgeState.Allowed;
            allowedTimer ??= new Timer(_ => CheckEdgeGone(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }
        Log.Write("Letting Edge run until it's closed.");
    }

    private void CheckEdgeGone()
    {
        if (Process.GetProcessesByName("msedge").Length > 0) return;
        lock (gate) EndAllowed();
        Log.Write("Edge closed; closing it again from now on.");
    }

    private void EndAllowed()
    {
        allowedTimer?.Dispose();
        allowedTimer = null;
        state = EdgeState.Closing;
    }

    private static ManagementEventWatcher Watch(WqlEventQuery query, EventArrivedEventHandler handler)
    {
        var started = new ManagementEventWatcher(query);
        started.EventArrived += handler;
        try { started.Start(); }
        catch { started.Dispose(); throw; }
        return started;
    }

    private void OnEdgeStartTrace(object sender, EventArrivedEventArgs e)
    {
        var processId = Convert.ToInt32(e.NewEvent["ProcessID"]);
        var commandLine = CommandLine.Read(processId);
        // Too late to know what it was opening; worth knowing how often that happens.
        // Not for an Edge the window hook already closed, or the helpers that die with it.
        if (commandLine == null && !WasSeen(processId) && !WasSeen(Convert.ToInt32(e.NewEvent["ParentProcessID"])))
            Log.Write("Missed an Edge start; it had already quit, most likely handing its link to an open Edge.");
        Handle(processId, commandLine);
    }

    private void OnEdgeFound(object sender, EventArrivedEventArgs e)
    {
        using var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
        Handle(Convert.ToInt32(process["ProcessId"]), process["CommandLine"] as string);
    }

    // Runs on the UI thread, for every new window anywhere, so it bails out early.
    private void OnWindowCreated(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time)
    {
        if (objectId != ObjectIdWindow || childId != 0) return;
        GetWindowThreadProcessId(window, out var processId);
        if (processId == 0 || WasSeen(processId) || !CommandLine.IsEdge(processId)) return;
        Handle(processId, CommandLine.Read(processId));
    }

    private bool WasSeen(int processId)
    {
        lock (gate) return seen.ContainsKey(processId);
    }

    private void Handle(int processId, string? commandLine)
    {
        lock (gate)
        {
            if (!seen.TryAdd(processId, DateTime.UtcNow)) return;
            // Process ids get reused, so only remember them for a while.
            if (seen.Count > 500)
                foreach (var old in seen.Where(p => DateTime.UtcNow - p.Value > TimeSpan.FromMinutes(1)).Select(p => p.Key).ToList())
                    seen.Remove(old);
        }
        try
        {
            // Edge's own helper processes come and go with the browser; leave them be.
            if (commandLine == null || IsHelper(commandLine)) return;
            var url = EdgeLaunch.FindUrl(commandLine);
            if (EdgeIsDefault())
            {
                if (url != null) Log.Write($"Skipped, Edge is the default browser: {url}");
                return;
            }
            if (url == null)
            {
                EdgeStartedWithoutLink(processId);
                return;
            }
            var closed = TryClose(processId);
            // Through Explorer, so the browser runs as the normal user even when this app is elevated.
            Process.Start("explorer.exe", $"\"{new Uri(url).AbsoluteUri}\"");
            Log.Write($"{(closed ? "Redirected" : "Redirected, but Edge got there first")}: {url}");
            Redirected?.Invoke(url, closed);
        }
        catch (Exception ex)
        {
            Log.Write($"Failed: {ex.Message}");
        }
    }

    private void EdgeStartedWithoutLink(int processId)
    {
        if (!settings.CloseEdge) return;
        lock (gate)
        {
            if (state != EdgeState.Closing) return;
            if (!closeLimit.Reached(DateTime.UtcNow, settings.AskAfterCloses, TimeSpan.FromMinutes(settings.AskWithinMinutes)))
            {
                if (TryClose(processId)) Log.Write("Closed Edge; it started without a link.");
                return;
            }
            state = EdgeState.Asking;
        }
        Log.Write("Edge keeps starting; asking before closing it again.");
        AskAboutEdge?.Invoke();
    }

    private static bool IsHelper(string commandLine) => commandLine.Contains("--type=", StringComparison.OrdinalIgnoreCase);

    private static List<int> OpenEdgeBrowsers()
    {
        var found = new List<int>();
        foreach (var edge in Process.GetProcessesByName("msedge"))
        {
            using (edge)
                if (CommandLine.Read(edge.Id) is { } commandLine && !IsHelper(commandLine)) found.Add(edge.Id);
        }
        return found;
    }

    // Only that one process and its children, so other Edge windows and WebView2 apps are left alone.
    private static bool TryClose(int processId)
    {
        try
        {
            using var edge = Process.GetProcessById(processId);
            if (!edge.ProcessName.Equals("msedge", StringComparison.OrdinalIgnoreCase)) return false;
            edge.Kill(entireProcessTree: true);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already gone: it handed the link to a running Edge and quit.
            return false;
        }
    }

    private const uint EventObjectCreate = 0x8000;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint WinEventSkipOwnProcess = 0x0002;
    private const int ObjectIdWindow = 0;

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc proc, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

    // Reading only. Opening the link with Edge as the default would just start Edge again, forever.
    private static bool EdgeIsDefault()
    {
        // Newer Windows 11 keeps the real choice in UserChoiceLatest\ProgId and leaves a stale UserChoice behind.
        foreach (var name in new[] { @"UserChoiceLatest\ProgId", "UserChoiceLatest", "UserChoice" })
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\{name}");
            if (key?.GetValue("ProgId") is string progId)
                return progId.StartsWith("MSEdge", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}

// Counts closes in a sliding window, so a burst of them means stop and ask.
internal sealed class CloseLimit
{
    private readonly Queue<DateTime> closes = new();

    // True instead of counting another close once the limit is hit; starts the count over.
    public bool Reached(DateTime now, int limit, TimeSpan window)
    {
        while (closes.Count > 0 && now - closes.Peek() > window) closes.Dequeue();
        if (closes.Count >= limit)
        {
            closes.Clear();
            return true;
        }
        closes.Enqueue(now);
        return false;
    }
}
