using System.Windows;
using System.Windows.Controls;
using Media = System.Windows.Media;
using Holo.Models;
using Holo.Services;
using Holo.Views;

namespace Holo;

public partial class MainWindow : Window
{
    private readonly AudioCaptureService _audioService;
    private readonly ClassificationService _classificationService;
    private readonly CalibrationService _calibrationService;
    private readonly EvaluationService _evaluationService;
    private readonly EvaluationStore _evaluationStore;
    private readonly ProfileService _profileService;
    private readonly TrayManager _trayManager;

    private readonly DeskView _deskView;
    private readonly CalibrationView _calibrationView;
    private readonly ActionsView _actionsView;
    private readonly EvaluationView _evaluationView;
    private readonly DiagnosticsView _diagnosticsView;

    private HoloProfile? _activeProfile;

    public MainWindow(
        AudioCaptureService audioService,
        ClassificationService classificationService,
        CalibrationService calibrationService,
        EvaluationService evaluationService,
        EvaluationStore evaluationStore,
        ProfileService profileService,
        TrayManager trayManager)
    {
        InitializeComponent();

        _audioService = audioService;
        _classificationService = classificationService;
        _calibrationService = calibrationService;
        _evaluationService = evaluationService;
        _evaluationStore = evaluationStore;
        _profileService = profileService;
        _trayManager = trayManager;

        // Subscribe to audio level updates and classification results
        _classificationService.LevelUpdated += OnLevelUpdated;
        _classificationService.ErrorOccurred += OnServiceError;
        _classificationService.TapClassified += OnTapClassified;

        // Create view pages
        _deskView = new DeskView(classificationService);
        _calibrationView = new CalibrationView(
            audioService,
            calibrationService,
            OnProfileCreated);
        _actionsView = new ActionsView();
        _evaluationView = new EvaluationView(evaluationService, evaluationStore, profileService);
        _diagnosticsView = new DiagnosticsView(audioService);

        // Show the desk view by default
        PageContent.Content = _deskView;

        // Load existing profiles
        LoadProfiles();

        this.Closing += OnClosing;
    }

    private void LoadProfiles()
    {
        var profiles = _profileService.LoadAll();
        if (profiles.Count > 0)
        {
            var latest = profiles[0];
            ActivateProfile(latest);
        }
    }

    private void ActivateProfile(HoloProfile profile)
    {
        _activeProfile = profile;
        _classificationService.SetProfile(profile);
        _deskView.SetProfile(profile);
        _evaluationView.SetProfile(profile);
        _trayManager?.ShowNotification("Holo", $"Active profile: {profile.Name}");
    }

    private void OnProfileCreated(HoloProfile profile)
    {
        ActivateProfile(profile);
    }

    private void OnNavigationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavListBox.SelectedItem == NavDesk)
        {
            PageTitle.Text = "Desk";
            PageContent.Content = _deskView;
        }
        else if (NavListBox.SelectedItem == NavCalibrate)
        {
            PageTitle.Text = "Calibration";
            PageContent.Content = _calibrationView;
        }
        else if (NavListBox.SelectedItem == NavActions)
        {
            PageTitle.Text = "Actions";
            PageContent.Content = _actionsView;
        }
        else if (NavListBox.SelectedItem == NavEvaluate)
        {
            PageTitle.Text = "Accuracy Test";
            PageContent.Content = _evaluationView;
        }
        else if (NavListBox.SelectedItem == NavDiagnostics)
        {
            PageTitle.Text = "Diagnostics";
            PageContent.Content = _diagnosticsView;
        }
    }

    private void OnLevelUpdated(float level)
    {
        Dispatcher.Invoke(() =>
        {
            MicLevelBar.Value = Math.Clamp(level, 0, 1);
            MicStatusDot.Fill = level > 0.01
                ? Media.Brushes.Red
                : Media.Brushes.Gray;
            MicStatusText.Text = level > 0.01 ? "Microphone active" : "Microphone off";
        });
    }

    private void OnTapClassified(ClassificationDecision decision)
    {
        if (decision.WasAccepted && decision.Zone.HasValue)
        {
            _trayManager.ShowNotification(
                "Tap detected",
                $"{decision.Zone.Value.DisplayName()} — confidence {decision.Confidence:P0}");
        }
    }

    private void OnServiceError(string error)
    {
        Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show(this, error, "Holo — Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        _trayManager.ShowNotification("Holo", "Holo is still running in the system tray.");
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            _trayManager.ShowNotification("Holo", "Holo is still running in the system tray.");
        }
    }
}
