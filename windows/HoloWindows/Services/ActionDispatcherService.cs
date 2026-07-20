using System.Diagnostics;
using Holo.Models;

namespace Holo.Services;

/// <summary>
/// Windows-native action dispatcher.
/// Maps to macOS HoloApp.LocalActionDispatcher (NSWorkspace, NSSound, etc.)
/// </summary>
public sealed class ActionDispatcherService
{
    public event Action<string>? ErrorOccurred;

    public bool Execute(ZoneActionConfiguration action)
    {
        try
        {
            switch (action.Kind)
            {
                case ActionKind.None:
                    return false; // visual feedback only

                case ActionKind.Sound:
                    // TODO: Port system sounds via System.Media.SystemSounds
                    System.Media.SystemSounds.Beep.Play();
                    return true;

                case ActionKind.CopyText:
                    System.Windows.Clipboard.SetText(action.Text);
                    return true;

                case ActionKind.SpeakText:
                    // TODO: Uses Windows Speech Synthesis (requires System.Speech reference)
                    System.Media.SystemSounds.Beep.Play();
                    return true;

                case ActionKind.OpenUrl:
                    var psi = new ProcessStartInfo
                    {
                        FileName = action.Text,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    return true;

                case ActionKind.RunShortcut:
                    // TODO: Map to Windows equivalent (PowerShell script / .lnk)
                    ErrorOccurred?.Invoke("Shortcuts not yet supported on Windows");
                    return false;

                case ActionKind.OpenApplication:
                    var appPsi = new ProcessStartInfo
                    {
                        FileName = action.Text,
                        UseShellExecute = true
                    };
                    Process.Start(appPsi);
                    return true;

                case ActionKind.OpenItem:
                    var itemPsi = new ProcessStartInfo
                    {
                        FileName = action.Text,
                        UseShellExecute = true
                    };
                    Process.Start(itemPsi);
                    return true;

                case ActionKind.RunShellCommand:
                    var cmdPsi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {action.Text}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(cmdPsi);
                    return true;

                case ActionKind.ScreenshotClipboard:
                case ActionKind.ScreenshotSelection:
                    // TODO: Implement using Windows Graphics Capture API / Snipping Tool
                    var snippingPsi = new ProcessStartInfo
                    {
                        FileName = "SnippingTool.exe",
                        UseShellExecute = true
                    };
                    Process.Start(snippingPsi);
                    return true;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Action failed: {ex.Message}");
            return false;
        }
    }
}
