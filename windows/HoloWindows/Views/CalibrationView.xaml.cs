using System.Windows.Controls;
using Holo.Models;
using Holo.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace Holo.Views;

public partial class CalibrationView : UserControl
{
    private readonly AudioCaptureService _audioService;
    private readonly CalibrationService _calibrationService;
    private readonly Action<HoloProfile>? _onProfileCreated;

    public CalibrationView(
        AudioCaptureService audioService,
        CalibrationService calibrationService,
        Action<HoloProfile>? onProfileCreated = null)
    {
        InitializeComponent();
        _audioService = audioService;
        _calibrationService = calibrationService;
        _onProfileCreated = onProfileCreated;

        SetupPanel.Visibility = System.Windows.Visibility.Visible;
        ActivePanel.Visibility = System.Windows.Visibility.Collapsed;
        CompletePanel.Visibility = System.Windows.Visibility.Collapsed;
    }

    private void OnStartCalibration(object sender, System.Windows.RoutedEventArgs e)
    {
        var draft = new CalibrationDraft
        {
            Name = ProfileNameBox.Text.Trim(),
            SurfaceDescription = SurfaceBox.Text.Trim(),
            LaptopPositionNote = PositionBox.Text.Trim(),
            Strategy = SensingStrategy.Passive
        };

        _calibrationService.ZoneChanged += OnZoneChanged;
        _calibrationService.ProgressUpdated += OnProgressUpdated;
        _calibrationService.TapFeedback += OnTapFeedback;
        _calibrationService.GuidanceMessage += OnGuidance;
        _calibrationService.CalibrationComplete += OnComplete;
        _calibrationService.ErrorOccurred += OnError;

        _calibrationService.Start(draft);
        _audioService.Start();

        SetupPanel.Visibility = System.Windows.Visibility.Collapsed;
        ActivePanel.Visibility = System.Windows.Visibility.Visible;
    }

    private void OnZoneChanged(string zoneName)
    {
        Dispatcher.Invoke(() =>
        {
            CurrentZoneName.Text = zoneName;
            var zone = Enum.GetValues<DeskZone>()
                .FirstOrDefault(z => z.DisplayName() == zoneName);
            CurrentZoneInstruction.Text = GetInstruction(zone);
        });
    }

    private void OnProgressUpdated(int collected, int total)
    {
        Dispatcher.Invoke(() =>
        {
            OverallProgress.Value = collected;
            OverallProgress.Maximum = total;
            OverallProgressText.Text = $"{collected} / {total}";

            // Zone progress: taps within the current zone
            var session = _calibrationService.Session;
            if (session?.CurrentZone != null)
            {
                int zoneCount = session.Count(session.CurrentZone.Value);
                ZoneProgress.Value = zoneCount;
                ZoneProgress.Maximum = session.TargetPerZone;
                ZoneProgressText.Text = $"{zoneCount} / {session.TargetPerZone}";
            }
        });
    }

    private void OnTapFeedback(string message)
    {
        Dispatcher.Invoke(() =>
        {
            FeedbackText.Text = message;
            if (message.StartsWith("✅"))
            {
                FeedbackBorder.BorderBrush = System.Windows.Media.Brushes.Green;
            }
            else if (message.StartsWith("⚠️"))
            {
                FeedbackBorder.BorderBrush = System.Windows.Media.Brushes.Orange;
            }
            else
            {
                FeedbackBorder.BorderBrush = System.Windows.Media.Brushes.Gray;
            }
        });
    }

    private void OnGuidance(string message)
    {
        Dispatcher.Invoke(() =>
        {
            GuidanceText.Text = message;
        });
    }

    private void OnComplete(HoloProfile profile)
    {
        Dispatcher.Invoke(() =>
        {
            _audioService.Stop();
            ActivePanel.Visibility = System.Windows.Visibility.Collapsed;

            int totalZones = profile.Classifier?.PositiveExamples
                .Select(p => p.Zone).Distinct().Count() ?? 0;
            CompleteSummary.Text = $"Collected {profile.Calibration!.SampleCount} taps across {totalZones} zones.\n" +
                                   $"Profile \"{profile.Name}\" saved.";

            _onProfileCreated?.Invoke(profile);
            CompletePanel.Visibility = System.Windows.Visibility.Visible;
        });
    }

    private void OnError(string error)
    {
        Dispatcher.Invoke(() =>
        {
            FeedbackText.Text = $"Error: {error}";
            FeedbackBorder.BorderBrush = System.Windows.Media.Brushes.Red;
        });
    }

    private void OnToggleArm(object sender, System.Windows.RoutedEventArgs e)
    {
        _calibrationService.ToggleArm();
    }

    private void OnCancel(object sender, System.Windows.RoutedEventArgs e)
    {
        _calibrationService.Cancel();
        _audioService.Stop();

        ActivePanel.Visibility = System.Windows.Visibility.Collapsed;
        SetupPanel.Visibility = System.Windows.Visibility.Visible;

        // Reset to setup state
        FeedbackText.Text = "Calibration cancelled.";
    }

    private void OnGoToDesk(object sender, System.Windows.RoutedEventArgs e)
    {
        // Navigate to desk — handled by parent
    }

    private static string GetInstruction(DeskZone zone)
    {
        int edge = zone.Row() == 0 ? 0 : 1; // 0 = display side, 1 = trackpad side
        string edgeName = edge == 0 ? "display" : "trackpad";
        return $"Tap beside the MacBook on the {(zone.IsLeft() ? "left" : "right")}, near the {edgeName} edge";
    }
}
