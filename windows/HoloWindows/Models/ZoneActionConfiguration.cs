namespace Holo.Models;

/// <summary>
/// Maps to HoloCore.ZoneActionConfiguration on macOS.
/// </summary>
public enum ActionKind
{
    None,
    Sound,
    CopyText,
    SpeakText,
    OpenUrl,
    RunShortcut,
    OpenApplication,
    OpenItem,
    RunShellCommand,
    ScreenshotClipboard,
    ScreenshotSelection
}

public class ZoneActionConfiguration
{
    public ActionKind Kind { get; set; } = ActionKind.None;
    public string SoundName { get; set; } = "Tink";
    public string Text { get; set; } = "";
    public byte[]? BookmarkData { get; set; }
}

public class ZoneConfiguration
{
    public DeskZone Zone { get; set; }
    public ZoneActionConfiguration Action { get; set; } = new();
}
