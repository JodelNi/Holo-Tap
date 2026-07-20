using System.Windows;
using Holo.Services;

namespace Holo;

public partial class App : System.Windows.Application
{
    private TrayManager? _trayManager;
    private AudioCaptureService? _audioService;
    private ClassificationService? _classificationService;
    private CalibrationService? _calibrationService;
    private EvaluationService? _evaluationService;
    private EvaluationStore? _evaluationStore;
    private ProfileService? _profileService;
    private ActionDispatcherService? _actionDispatcher;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Create services
        _audioService = new AudioCaptureService();
        _actionDispatcher = new ActionDispatcherService();
        _profileService = new ProfileService();
        _classificationService = new ClassificationService(_audioService, _actionDispatcher);
        _calibrationService = new CalibrationService(_audioService, _profileService);
        _evaluationService = new EvaluationService(_classificationService);
        _evaluationStore = new EvaluationStore();

        // Setup system tray
        _trayManager = new TrayManager();
        _trayManager.StartRecordingClicked += OnStartRecording;
        _trayManager.StopRecordingClicked += OnStopRecording;
        _trayManager.ShowWindowClicked += OnShowWindow;
        _trayManager.QuitClicked += OnQuit;

        // Show main window with all services wired
        var mainWindow = new MainWindow(
            _audioService!,
            _classificationService!,
            _calibrationService!,
            _evaluationService!,
            _evaluationStore!,
            _profileService!,
            _trayManager!);
        mainWindow.Show();
    }

    private void OnStartRecording()
    {
        if (_classificationService == null) return;
        bool started = _classificationService.StartListening();
        _trayManager?.SetRecordingState(started);
        if (started)
            _trayManager?.ShowNotification("Holo", "Listening for desk taps...");
        else
            _trayManager?.ShowNotification("Holo", "Failed to start microphone. Check permissions.");
    }

    private void OnStopRecording()
    {
        _classificationService?.StopListening();
        _trayManager?.SetRecordingState(false);
        _trayManager?.ShowNotification("Holo", "Listening stopped.");
    }

    private void OnShowWindow()
    {
        foreach (Window window in Current.Windows)
        {
            if (window is MainWindow mainWindow)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
                return;
            }
        }
        var newWindow = new MainWindow(
            _audioService!,
            _classificationService!,
            _calibrationService!,
            _evaluationService!,
            _evaluationStore!,
            _profileService!,
            _trayManager!);
        newWindow.Show();
    }

    private void OnQuit()
    {
        _audioService?.Dispose();
        _trayManager?.Dispose();
        Current.Shutdown();
    }
}
