using System.Windows.Controls;
using Media = System.Windows.Media;
using MediaAnimation = System.Windows.Media.Animation;
using Holo.Models;
using Holo.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace Holo.Views;

public partial class DeskView : UserControl
{
    private readonly ClassificationService _classificationService;
    private HoloProfile? _currentProfile;
    private readonly Dictionary<DeskZone, Border> _zoneBorders;
    private readonly Dictionary<DeskZone, TextBlock> _confidenceLabels;

    public DeskView(ClassificationService classificationService)
    {
        InitializeComponent();
        _classificationService = classificationService;
        _classificationService.TapClassified += OnTapClassified;

        _zoneBorders = new Dictionary<DeskZone, Border>
        {
            [DeskZone.LeftTop] = LRBorder,
            [DeskZone.LeftBottom] = LFBorder,
            [DeskZone.RightTop] = RRBorder,
            [DeskZone.RightBottom] = RFBorder
        };

        _confidenceLabels = new Dictionary<DeskZone, TextBlock>
        {
            [DeskZone.LeftTop] = LRConfidence,
            [DeskZone.LeftBottom] = LFConfidence,
            [DeskZone.RightTop] = RRConfidence,
            [DeskZone.RightBottom] = RFConfidence
        };
    }

    public void SetProfile(HoloProfile profile)
    {
        _currentProfile = profile;
        UpdateZoneActions(profile);

        Dispatcher.Invoke(() =>
        {
            StatusMessage.Text = profile.Classifier != null
                ? $"Active profile: {profile.Name} | {profile.Calibration!.SampleCount} taps"
                : "Profile loaded — no classifier data";
        });
    }

    public void ClearProfile()
    {
        _currentProfile = null;
        Dispatcher.Invoke(() =>
        {
            StatusMessage.Text = "Set up your desk via Calibration to begin.";
            foreach (var (_, label) in _confidenceLabels)
                label.Text = "";
        });
    }

    private void OnTapClassified(ClassificationDecision decision)
    {
        Dispatcher.Invoke(() =>
        {
            if (decision.WasAccepted && decision.Zone.HasValue)
            {
                var zone = decision.Zone.Value;
                FlashZone(zone);
                UpdateConfidence(zone, decision.Confidence);

                LastTapInfo.Text = $"{zone.DisplayName()} detected — " +
                    $"confidence {decision.Confidence:P1}, " +
                    $"strength {decision.SignalStrength:P1}";
            }
            else
            {
                var reason = decision.RejectionReason?.ToString() ?? "unknown";
                LastTapInfo.Text = $"Rejected: {reason}";
            }
        });
    }

    private void FlashZone(DeskZone zone)
    {
        if (!_zoneBorders.TryGetValue(zone, out var border)) return;

        // Quick flash animation to show detection
        var originalBrush = border.Background.Clone();
        var flashBrush = new Media.SolidColorBrush(Media.Color.FromRgb(76, 175, 80));

        border.Background = flashBrush;
        border.BorderBrush = Media.Brushes.Green;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        timer.Tick += (_, _) =>
        {
            border.Background = originalBrush;
            border.BorderBrush = new Media.SolidColorBrush(Media.Color.FromRgb(0, 120, 215));
            timer.Stop();
        };
        timer.Start();
    }

    private void UpdateConfidence(DeskZone zone, double confidence)
    {
        if (!_confidenceLabels.TryGetValue(zone, out var label)) return;
        label.Text = $"{confidence:P0} confident";
    }

    private void UpdateZoneActions(HoloProfile profile)
    {
        Dispatcher.Invoke(() =>
        {
            foreach (var zoneConfig in profile.Zones)
            {
                var actionText = zoneConfig.Action.Kind switch
                {
                    ActionKind.None => "Visual only",
                    ActionKind.Sound => $"Sound: {zoneConfig.Action.SoundName}",
                    ActionKind.CopyText => "Copy text",
                    ActionKind.SpeakText => "Speak text",
                    ActionKind.OpenUrl => $"Open: {zoneConfig.Action.Text}",
                    ActionKind.RunShortcut => "Run Shortcut",
                    ActionKind.OpenApplication => "Open app",
                    ActionKind.OpenItem => "Open file",
                    ActionKind.RunShellCommand => "Shell command",
                    ActionKind.ScreenshotClipboard => "Screenshot (clipboard)",
                    ActionKind.ScreenshotSelection => "Screenshot (selection)",
                    _ => "Visual only"
                };

                var actionBlock = zoneConfig.Zone switch
                {
                    DeskZone.LeftTop => LRAction,
                    DeskZone.LeftBottom => LFAction,
                    DeskZone.RightTop => RRAction,
                    DeskZone.RightBottom => RFAction,
                    _ => null
                };
                if (actionBlock != null)
                    actionBlock.Text = actionText;
            }
        });
    }
}
