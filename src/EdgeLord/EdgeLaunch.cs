namespace EdgeLord;

// Reads msedge.exe command lines that Windows produced for microsoft-edge: links.
internal static class EdgeLaunch
{
    private const string Scheme = "microsoft-edge:";

    // The web address the launch was meant to open, or null if it isn't a microsoft-edge: link.
    public static string? FindUrl(string? commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return null;
        // Edge's own helper processes; never a link.
        if (commandLine.Contains("--type=", StringComparison.OrdinalIgnoreCase)) return null;

        var start = commandLine.IndexOf(Scheme, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        var rest = commandLine[(start + Scheme.Length)..].Trim().Trim('"').Trim();

        // microsoft-edge:https://example.com
        if (IsWeb(rest)) return rest;
        // microsoft-edge:https%3A%2F%2Fexample.com
        var unescaped = Uri.UnescapeDataString(rest);
        if (IsWeb(unescaped)) return unescaped;

        // microsoft-edge:?launchContext1=...&url=https%3A%2F%2Fexample.com
        var query = rest.IndexOf('?');
        if (query < 0) return null;
        foreach (var pair in rest[(query + 1)..].Split('&'))
        {
            if (!pair.StartsWith("url=", StringComparison.OrdinalIgnoreCase)) continue;
            var url = Uri.UnescapeDataString(pair[4..]);
            return IsWeb(url) ? url : null;
        }
        return null;
    }

    // Only http and https get passed on, so a crafted link can't launch anything else.
    private static bool IsWeb(string text)
        => Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
