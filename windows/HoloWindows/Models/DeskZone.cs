namespace Holo.Models;

/// <summary>
/// Maps directly to HoloCore.DeskZone on macOS.
/// Four zones: rear and front on each side of the laptop.
/// </summary>
public enum DeskZone : int
{
    LeftTop = 0,
    LeftBottom = 1,
    RightTop = 2,
    RightBottom = 3
}

public static class DeskZoneExtensions
{
    public static bool IsLeft(this DeskZone zone) => (int)zone < 2;
    public static int Row(this DeskZone zone) => (int)zone % 2;

    public static string ShortName(this DeskZone zone) =>
        zone switch
        {
            DeskZone.LeftTop => "LR",
            DeskZone.LeftBottom => "LF",
            DeskZone.RightTop => "RR",
            DeskZone.RightBottom => "RF",
            _ => "??"
        };

    public static string DisplayName(this DeskZone zone) =>
        zone switch
        {
            DeskZone.LeftTop => "Left Rear",
            DeskZone.LeftBottom => "Left Front",
            DeskZone.RightTop => "Right Rear",
            DeskZone.RightBottom => "Right Front",
            _ => "Unknown"
        };
}
