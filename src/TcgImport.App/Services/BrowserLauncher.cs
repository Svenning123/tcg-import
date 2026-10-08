using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace TcgImport.App.Services;

/// <summary>
/// Opens links in the browser where the user is signed in to TCG Arena: a configured browser,
/// otherwise the Windows default browser.
/// </summary>
public static class BrowserLauncher
{
    /// <returns>A short name for the browser that was used, for status messages.</returns>
    public static string Open(string url, string? configuredBrowserPath)
    {
        var browser = !string.IsNullOrWhiteSpace(configuredBrowserPath) && File.Exists(configuredBrowserPath)
            ? configuredBrowserPath
            : FindDefaultBrowser();

        if (browser is null)
        {
            // Shell-executing a URL hands it to the default browser.
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return "your default browser";
        }

        // Passing the link as an argument avoids the shell's URL length limits; import links can be several KB.
        var start = new ProcessStartInfo(browser) { UseShellExecute = false };
        start.ArgumentList.Add(url);
        Process.Start(start);
        return DisplayName(browser);
    }

    /// <summary>The executable registered for https links, e.g. chrome.exe.</summary>
    public static string? FindDefaultBrowser()
    {
        using var choice = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
        if (choice?.GetValue("ProgId") is not string progId) return null;

        using var command = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
        if (command?.GetValue(null) is not string commandLine) return null;

        var exe = commandLine.StartsWith('"')
            ? commandLine[1..commandLine.IndexOf('"', 1)]
            : commandLine.Split(' ')[0];
        return File.Exists(exe) ? exe : null;
    }

    private static string DisplayName(string exe)
    {
        var description = FileVersionInfo.GetVersionInfo(exe).FileDescription;
        return string.IsNullOrWhiteSpace(description) ? Path.GetFileNameWithoutExtension(exe) : description;
    }
}
