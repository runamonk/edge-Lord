using Avalonia.Controls;
using Avalonia.Interactivity;

namespace EdgeLord;

public partial class SettingsWindow : Window
{
    // For the designer only.
    public SettingsWindow() => InitializeComponent();

    internal SettingsWindow(Settings settings) : this()
    {
        CloseEdge.IsChecked = settings.CloseEdge;
        AskAfterCloses.Value = settings.AskAfterCloses;
        AskWithinMinutes.Value = settings.AskWithinMinutes;
        CheckForUpdates.IsChecked = settings.CheckForUpdates;
        UpdateEveryDays.Value = settings.UpdateEveryDays;
        VersionText.Text = $"You have version {App.AppVersion}. It asks before downloading anything; to check now, use the tray menu.";
        Closed += (_, _) =>
        {
            settings.CheckForUpdates = CheckForUpdates.IsChecked == true;
            settings.UpdateEveryDays = (int)(UpdateEveryDays.Value ?? settings.UpdateEveryDays);
            settings.CloseEdge = CloseEdge.IsChecked == true;
            settings.AskAfterCloses = (int)(AskAfterCloses.Value ?? settings.AskAfterCloses);
            settings.AskWithinMinutes = (int)(AskWithinMinutes.Value ?? settings.AskWithinMinutes);
            settings.Save();
        };
    }

    private void CloseClicked(object? sender, RoutedEventArgs e) => Close();
}
