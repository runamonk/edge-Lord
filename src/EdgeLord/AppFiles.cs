using System.Text.Json;

namespace EdgeLord;

internal static class AppFiles
{
    public static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdgeLord");
}

internal sealed class Settings
{
    private static readonly string FilePath = Path.Combine(AppFiles.Folder, "settings.json");

    public bool Enabled { get; set; } = true;

    // Close an Edge that starts without a link; once it's been closed this often this quickly, ask first.
    public bool CloseEdge { get; set; } = true;
    public int AskAfterCloses { get; set; } = 3;
    public int AskWithinMinutes { get; set; } = 2;

    // Checks GitHub on startup, at most once every this many days (0 is every start).
    public bool CheckForUpdates { get; set; } = true;
    public int UpdateEveryDays { get; set; }
    public DateTime? LastUpdateCheckUtc { get; set; }

    public static Settings Read()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppFiles.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Write($"Couldn't save settings: {ex.Message}"); }
    }
}

internal static class Log
{
    public static readonly string FilePath = Path.Combine(AppFiles.Folder, "redirects.log");
    private static readonly Lock Gate = new();

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(AppFiles.Folder);
                // Start over rather than grow forever.
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1_000_000) File.Delete(FilePath);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}

// A shortcut in the Startup folder, so starting with Windows needs no registry change.
internal static class StartWithWindows
{
    private static readonly string ShortcutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "edge Lord.lnk");

    public static bool IsOn => File.Exists(ShortcutPath);

    public static void Set(bool on)
    {
        if (!on)
        {
            File.Delete(ShortcutPath);
            return;
        }
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Can't find the app's own path.");
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        var shortcut = shell.CreateShortcut(ShortcutPath);
        shortcut.TargetPath = exe;
        shortcut.WorkingDirectory = Path.GetDirectoryName(exe);
        shortcut.Save();
    }
}
