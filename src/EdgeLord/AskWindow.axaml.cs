using Avalonia.Controls;
using Avalonia.Interactivity;

namespace EdgeLord;

public partial class AskWindow : Window
{
    // Closing the window without choosing leaves Edge alone; killing a browser should need a yes.
    public bool KeepClosing { get; private set; }

    // For the designer only.
    public AskWindow() => InitializeComponent();

    internal AskWindow(Settings settings) : this()
    {
        Message.Text = $"Edge has been closed {settings.AskAfterCloses} times in {settings.AskWithinMinutes} minutes and keeps coming back. "
            + "Something may need it, or you may be trying to use it.\n\n"
            + "If you let it run, links open in Edge until you close it. After that, it gets closed again.";
    }

    private void LetRunClicked(object? sender, RoutedEventArgs e) => Close();

    private void KeepClosingClicked(object? sender, RoutedEventArgs e)
    {
        KeepClosing = true;
        Close();
    }
}
