using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;

namespace EdgeLord;

public partial class App : Application
{
    private readonly Settings settings = Settings.Read();
    private readonly EdgeWatcher watcher;
    private readonly WindowIcon onIcon = LoadIcon("EdgeLord.ico");
    private readonly WindowIcon offIcon = LoadIcon("EdgeLord-off.ico");
    private TrayIcon tray = null!;
    private NativeMenuItem enabledItem = null!;
    private string lastRedirect = "";
    private SettingsWindow? settingsWindow;

    public App() => watcher = new EdgeWatcher(settings);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // No windows; the tray icon is the whole app.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            BuildTray();
            watcher.Redirected += (url, closed) => Dispatcher.UIThread.Post(() =>
            {
                lastRedirect = (closed ? "Last: " : "Last (Edge also opened it): ") + url;
                ShowState();
            });
            watcher.AskAboutEdge += () => Dispatcher.UIThread.Post(AskAboutEdge);
            SetEnabled(settings.Enabled);
            InitializeUpdates();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void BuildTray()
    {
        enabledItem = new NativeMenuItem("Redirect Edge links") { ToggleType = MenuItemToggleType.CheckBox };
        enabledItem.Click += (_, _) => SetEnabled(!watcher.IsRunning);

        var startupItem = new NativeMenuItem("Start with Windows")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = StartWithWindows.IsOn,
        };
        startupItem.Click += (_, _) =>
        {
            try { StartWithWindows.Set(!StartWithWindows.IsOn); }
            catch (Exception ex) { Log.Write($"Couldn't change Start with Windows: {ex.Message}"); }
            startupItem.IsChecked = StartWithWindows.IsOn;
        };

        var settingsItem = new NativeMenuItem("Settings...");
        settingsItem.Click += (_, _) =>
        {
            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow(settings);
                settingsWindow.Closed += (_, _) => settingsWindow = null;
                settingsWindow.Show();
            }
            settingsWindow.Activate();
        };

        var logItem = new NativeMenuItem("Open log");
        logItem.Click += (_, _) =>
        {
            if (File.Exists(Log.FilePath)) Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
        };

        var updateItem = new NativeMenuItem("Check for updates");
        updateItem.Click += (_, _) => CheckForUpdates_Click();

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => Quit();

        tray = new TrayIcon
        {
            Menu = new NativeMenu { enabledItem, startupItem, settingsItem, new NativeMenuItemSeparator(), logItem, updateItem, new NativeMenuItemSeparator(), exitItem },
        };
        // Left click flips it on and off.
        tray.Clicked += (_, _) => SetEnabled(!watcher.IsRunning);
        TrayIcon.SetIcons(this, [tray]);
    }

    private void Quit()
    {
        watcher.Dispose();
        tray.Dispose();
        StartUpdater();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
    }

    private void AskAboutEdge()
    {
        var ask = new AskWindow(settings);
        ask.Closed += (_, _) =>
        {
            if (ask.KeepClosing) watcher.KeepClosing();
            else watcher.LetEdgeRun();
        };
        ask.Show();
        ask.Activate();
    }

    private void SetEnabled(bool on)
    {
        try
        {
            if (on) watcher.Start();
            else watcher.Stop();
        }
        catch (Exception ex)
        {
            // WMI can be broken or disabled; say so instead of pretending to work.
            Log.Write($"Couldn't start watching for Edge: {ex.Message}");
            lastRedirect = "Couldn't watch for Edge, see the log";
        }
        settings.Enabled = on;
        settings.Save();
        ShowState();
    }

    private void ShowState()
    {
        var on = watcher.IsRunning;
        enabledItem.IsChecked = on;
        tray.Icon = on ? onIcon : offIcon;
        var text = "edge Lord: " + (on ? watcher.IsInstant ? "on" : "on (not admin, may miss links)" : "off");
        if (lastRedirect.Length > 0) text += "\n" + lastRedirect;
        // Windows cuts tray tooltips off at 127 characters.
        tray.ToolTipText = text.Length > 127 ? text[..124] + "..." : text;
    }

    private static WindowIcon LoadIcon(string name)
        => new(AssetLoader.Open(new Uri($"avares://EdgeLord/Assets/{name}")));
}
