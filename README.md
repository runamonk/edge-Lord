# edge Lord

![edge Lord icon](src/EdgeLord/Assets/EdgeLord.png)

**Lord over Edge. Your links go where you said.**

Windows keeps opening links in Edge no matter what your default browser is: Start menu search, Widgets, help links in
Settings, the odd Outlook or Teams link. edge Lord sits in the tray and sends them to your default browser instead.

Nothing is written to the registry and no system files are touched, so turning it off really is just a click.

Built with .NET 10 and Avalonia 12, the same stack as Zuul and Brain Pending.

## How it works

Those links go through `microsoft-edge:` addresses, which Windows won't let any other browser handle. So edge Lord
doesn't try. It watches for Edge starting with one of those links, closes it, and opens the link in your default
browser. Edge is caught as it creates its window, before it shows, so you shouldn't see it.

Only `http` and `https` links are passed on. If Edge is your default browser, edge Lord leaves everything alone, since
the alternative is an endless loop.

### Keeping Edge closed

An Edge that's already running takes the link in a few milliseconds, which is faster than anything can stop it. So
edge Lord also closes Edge when it starts without a link, and closes any Edge that's open when edge Lord starts.

If Edge keeps coming back (three closes within two minutes, by default), edge Lord stops and asks:

- **Keep closing it**: closes it and starts counting again.
- **Let Edge run**: leaves it alone until you've closed Edge yourself, then goes back to closing it. Links open in Edge
  in the meantime.

Settings turns this off, or changes the limits.

### Turn off Edge's Startup Boost

Edge › Settings › System and performance: turn off **Startup boost** and **Continue running background extensions
and apps when Microsoft Edge is closed**. Otherwise Edge starts in the background when you sign in and sits there
waiting to grab links.

## The tray

The crown is gold when it's on and grey when it's off. Left-click flips it. Right-click for:

- **Redirect Edge links**: on or off; it remembers.
- **Start with Windows**: a shortcut in your Startup folder, not a registry entry.
- **Settings...**
- **Open log**: every redirect, every Edge it closed, and anything that slipped past.
- **Check for updates**
- **Exit**

Hover over it to see the last link it redirected.

## Admin

edge Lord asks to run as administrator when it starts (silently, if UAC is set to never notify). That gives it a second,
earlier warning that Edge is starting. Without admin it still catches Edge's window, and the tooltip says
"not admin, may miss links".

Windows doesn't normally elevate programs started from the Startup folder, so after signing in it may run without
admin. Check the tooltip.

## Settings

Everything lives in `%LOCALAPPDATA%\EdgeLord`: `settings.json` and `redirects.log` (which starts over at 1 MB).

## Updates

Release builds check GitHub for a newer version when edge Lord starts (Settings turns that off or spaces it out), and
the tray menu checks now. It asks before downloading, checks the download's SHA-256, and installs it when edge Lord
quits. The replaced files are kept in a `backup` folder next to EdgeLord.exe. Dev builds never update.

## Build

```
dotnet test EdgeLord.slnx
publish.cmd
```

`scripts/Generate-AppIcon.ps1` redraws the icons.

Releases come from tags: push `v1.2.3` and GitHub Actions builds, tests, and publishes the zip with the updater in it.
Tags with a dash, like `v1.2.3-test`, become pre-releases, which the updater never offers.
