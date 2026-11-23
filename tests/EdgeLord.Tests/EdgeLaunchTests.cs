using EdgeLord;

namespace EdgeLord.Tests;

public class EdgeLaunchTests
{
    private const string Edge = @"""C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe""";

    [Fact]
    public void ReadsTheUrlParameterFromWindowsSearch()
    {
        var line = Edge + " --single-argument microsoft-edge:?launchContext1=Microsoft.Windows.Search_cw5n1h2txyewy&url=https%3A%2F%2Fwww.bing.com%2Fsearch%3Fq%3Dcats%26form%3DWNSGPH";
        Assert.Equal("https://www.bing.com/search?q=cats&form=WNSGPH", EdgeLaunch.FindUrl(line));
    }

    [Fact]
    public void ReadsAPlainLink()
        => Assert.Equal("https://example.com/a?b=c", EdgeLaunch.FindUrl(Edge + " --single-argument microsoft-edge:https://example.com/a?b=c"));

    [Fact]
    public void ReadsAnEscapedLink()
        => Assert.Equal("https://example.com/", EdgeLaunch.FindUrl(Edge + " --single-argument microsoft-edge:https%3A%2F%2Fexample.com%2F"));

    [Fact]
    public void ReadsAQuotedLink()
        => Assert.Equal("https://example.com/", EdgeLaunch.FindUrl(Edge + " \"microsoft-edge:https://example.com/\""));

    [Fact]
    public void ReadsTheUrlParameterAfterSlashes()
        => Assert.Equal("http://example.com/", EdgeLaunch.FindUrl(Edge + " microsoft-edge:///?url=http%3A%2F%2Fexample.com%2F"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    // Someone opened Edge on purpose.
    [InlineData(Edge)]
    [InlineData(Edge + " https://example.com")]
    // Edge's helper processes.
    [InlineData(Edge + " --type=renderer --flag microsoft-edge:https://example.com")]
    // Nothing but web links get passed on.
    [InlineData(Edge + " --single-argument microsoft-edge:file:///C:/Windows/System32/calc.exe")]
    [InlineData(Edge + " --single-argument microsoft-edge:?url=ms-settings%3Adefaultapps")]
    [InlineData(Edge + " --single-argument microsoft-edge:?launchContext1=x")]
    public void IgnoresAnythingElse(string? line) => Assert.Null(EdgeLaunch.FindUrl(line));
}

public class CloseLimitTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan TwoMinutes = TimeSpan.FromMinutes(2);

    [Fact]
    public void AsksOnceTheLimitIsUsedUp()
    {
        var limit = new CloseLimit();
        Assert.False(limit.Reached(Start, 3, TwoMinutes));
        Assert.False(limit.Reached(Start.AddSeconds(10), 3, TwoMinutes));
        Assert.False(limit.Reached(Start.AddSeconds(20), 3, TwoMinutes));
        Assert.True(limit.Reached(Start.AddSeconds(30), 3, TwoMinutes));
    }

    [Fact]
    public void ForgetsClosesOlderThanTheWindow()
    {
        var limit = new CloseLimit();
        limit.Reached(Start, 3, TwoMinutes);
        limit.Reached(Start.AddSeconds(10), 3, TwoMinutes);
        limit.Reached(Start.AddSeconds(20), 3, TwoMinutes);
        // The first close has aged out, so this one still counts as a close.
        Assert.False(limit.Reached(Start.AddMinutes(2).AddSeconds(5), 3, TwoMinutes));
    }

    [Fact]
    public void StartsOverAfterAsking()
    {
        var limit = new CloseLimit();
        limit.Reached(Start, 1, TwoMinutes);
        Assert.True(limit.Reached(Start.AddSeconds(1), 1, TwoMinutes));
        Assert.False(limit.Reached(Start.AddSeconds(2), 1, TwoMinutes));
    }
}
