using System.Windows.Controls;
using Holo.Models;
using Holo.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace Holo.Views;

public partial class EvaluationView : UserControl
{
    private readonly EvaluationService _evaluationService;
    private readonly EvaluationStore _evaluationStore;
    private readonly ProfileService _profileService;
    private HoloProfile? _activeProfile;
    private EvaluationReport? _lastReport;
    private readonly TextBlock[,] _cmCells;

    public EvaluationView(
        EvaluationService evaluationService,
        EvaluationStore evaluationStore,
        ProfileService profileService)
    {
        InitializeComponent();
        _evaluationService = evaluationService;
        _evaluationStore = evaluationStore;
        _profileService = profileService;

        _cmCells = new TextBlock[4, 4]
        {
            { Cm00, Cm01, Cm02, Cm03 },
            { Cm10, Cm11, Cm12, Cm13 },
            { Cm20, Cm21, Cm22, Cm23 },
            { Cm30, Cm31, Cm32, Cm33 }
        };

        _evaluationService.ZoneChanged += OnZoneChanged;
        _evaluationService.ProgressUpdated += OnProgressUpdated;
        _evaluationService.TapFeedback += OnTapFeedback;
        _evaluationService.GuidanceMessage += OnGuidance;
        _evaluationService.EvaluationComplete += OnComplete;
        _evaluationService.ErrorOccurred += OnError;

        LoadHistory();
    }

    public void SetProfile(HoloProfile profile)
    {
        _activeProfile = profile;
        Dispatcher.Invoke(() =>
        {
            ActiveProfileInfo.Text = $"Active profile: {profile.Name} ({profile.Calibration?.SampleCount ?? 0} taps)";
            StartButton.IsEnabled = profile.Classifier != null;
        });
    }

    public void ClearProfile()
    {
        _activeProfile = null;
        Dispatcher.Invoke(() =>
        {
            ActiveProfileInfo.Text = "No active profile";
            StartButton.IsEnabled = false;
        });
    }

    private void LoadHistory()
    {
        var history = _evaluationStore.LoadAll();
        var items = history.Take(10).Select(r => new
        {
            Summary = $"{r.CompletedAt:g} — {r.OverallAccuracy:P1} ({r.Correct}/{r.TotalTaps})"
        }).ToList();
        HistoryList.ItemsSource = items;
    }

    private void OnStartEvaluation(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_activeProfile == null) return;

        SetupPanel.Visibility = System.Windows.Visibility.Collapsed;
        ResultsPanel.Visibility = System.Windows.Visibility.Collapsed;
        ActivePanel.Visibility = System.Windows.Visibility.Visible;

        _evaluationService.Start(_activeProfile);
    }

    private void OnZoneChanged(DeskZone zone)
    {
        Dispatcher.Invoke(() =>
        {
            CurrentZoneName.Text = zone.DisplayName();
            CurrentZoneCount.Text = $"0 / 15";
        });
    }

    private void OnProgressUpdated(int collected, int total)
    {
        Dispatcher.Invoke(() =>
        {
            OverallProgress.Value = collected;
            OverallProgress.Maximum = total;
            OverallProgressText.Text = $"{collected} / {total}";

            // Update zone count
            var session = _evaluationService.Session;
            if (session?.CurrentZone != null)
            {
                int zoneCount = session.Records.Count(r => r.ExpectedZone == session.CurrentZone.Value);
                CurrentZoneCount.Text = $"{zoneCount} / {session.TargetPerZone}";
            }
        });
    }

    private void OnTapFeedback(string message)
    {
        Dispatcher.Invoke(() => FeedbackText.Text = message);
    }

    private void OnGuidance(string message)
    {
        Dispatcher.Invoke(() => GuidanceText.Text = message);
    }

    private void OnComplete(EvaluationReport report)
    {
        Dispatcher.Invoke(() =>
        {
            _lastReport = report;
            ActivePanel.Visibility = System.Windows.Visibility.Collapsed;
            ShowResults(report);
            ResultsPanel.Visibility = System.Windows.Visibility.Visible;
        });
    }

    private void OnError(string error)
    {
        Dispatcher.Invoke(() => FeedbackText.Text = $"Error: {error}");
    }

    private void OnToggleArm(object sender, System.Windows.RoutedEventArgs e)
    {
        _evaluationService.ToggleArm();
    }

    private void OnCancel(object sender, System.Windows.RoutedEventArgs e)
    {
        _evaluationService.Cancel();
        ActivePanel.Visibility = System.Windows.Visibility.Collapsed;
        SetupPanel.Visibility = System.Windows.Visibility.Visible;
    }

    private void OnSaveReport(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_lastReport == null) return;
        var path = _evaluationStore.Save(_lastReport);
        FeedbackText.Text = $"Saved to {path}";
        LoadHistory();
    }

    private void OnRunAgain(object sender, System.Windows.RoutedEventArgs e)
    {
        ResultsPanel.Visibility = System.Windows.Visibility.Collapsed;
        SetupPanel.Visibility = System.Windows.Visibility.Visible;
        LoadHistory();
    }

    private void ShowResults(EvaluationReport report)
    {
        // Overall accuracy
        string status = report.OverallAccuracy >= 0.80 ? "✅" : "⚠️";
        ResultOverall.Text = $"{status} {report.OverallAccuracy:P1} accuracy — " +
            $"{report.Correct}/{report.TotalTaps} correct, {report.Rejected} rejected";

        // Confusion matrix display
        var zones = Enum.GetValues<DeskZone>().Cast<DeskZone>().ToArray();
        var cmLines = new List<string>
        {
            $"      {"LR",4} {"LF",4} {"RR",4} {"RF",4}"
        };
        for (int r = 0; r < 4; r++)
        {
            var row = report.ConfusionMatrix[r];
            cmLines.Add($"{zones[r].ShortName(),-4} {row[0],4} {row[1],4} {row[2],4} {row[3],4}");
        }
        ConfusionGrid.ItemsSource = cmLines;

        // Per-zone accuracy
        var zoneResults = report.ZoneResults.Select(z => new
        {
            ZoneName = z.Zone.DisplayName(),
            Accuracy = z.Total > 0 ? (double)z.Correct / z.Total : 0.0,
            AccuracyText = z.Total > 0 ? $"{z.Correct}/{z.Total} ({z.Accuracy:P0})" : "—"
        }).ToList();
        ZoneResultsList.ItemsSource = zoneResults;
    }
}
