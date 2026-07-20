# Holo for Windows

Windows-native port of [Holo](https://github.com/JustinGamer191/Holo) — a desk tap detection utility that turns the area around a laptop into four assignable tap zones using the built-in microphone.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    WPF UI (HoloWindow)                   │
│  DeskView │ CalibrationView │ ActionsView │ ...          │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│                     AppModel                             │
│  Profile management │ Zone config │ State coordination   │
└──────┬──────────────────────┬──────────────────────┬────┘
       │                      │                      │
┌──────▼──────────┐  ┌───────▼──────────┐  ┌───────▼──┐
│ AudioCapture    │  │ Tap Detector &   │  │ Action   │
│ Service (NAudio)│  │ Classifier (DSP) │  │ Dispatcher│
└─────────────────┘  └──────────────────┘  └──────────┘
       │                      │                      │
       ▼                      ▼                      ▼
 Windows WASAPI      C# Port of Swift DSP        ShellExecute
 (microphone)        (StreamingTapDetector,      (local actions)
                     FFT, FeatureExtractor)
```

## macOS → Windows Feature Mapping

| macOS Feature | Windows Equivalent | Status |
|---|---|---|
| `AVAudioEngine` | `NAudio.WaveInEvent` | ✅ Ported |
| `StreamingTapDetector` | `DSP/StreamingTapDetector.cs` | ✅ Ported |
| `Radix2FFT` | `DSP/FFT.cs` | ✅ Ported |
| `TapFeatureExtractor` | `DSP/TapFeatureExtractor.cs` | 🔄 Stub |
| `TapClassifier` | `DSP/TapClassifier.cs` | 🔄 Stub |
| `AppModel` (ObservableObject) | `App.xaml.cs` + MainWindow | ✅ Ported |
| SwiftUI sidebar | WPF `ListBox` sidebar | ✅ Ported |
| macOS menu bar | Windows system tray `NotifyIcon` | ✅ Ported |
| `ProfileStore` (JSON) | `Services/ProfileService.cs` | ✅ Ported |
| `LocalActionDispatcher` | `Services/ActionDispatcherService.cs` | ✅ Ported |
| `GuidedSessions` | `CalibrationView` | 🔄 Stub |
| `Evaluation` | `EvaluationView` | 🔄 Stub |
| `ImpactEventGate` | `DSP/StreamingTapDetector.cs` (inline) | ✅ Ported |

## Project Structure

```
/windows/HoloWindows/
├── HoloWindows.csproj          # .NET 8 + WPF project
├── App.xaml / App.xaml.cs       # Entry point + tray setup
├── MainWindow.xaml / .cs        # Main WPF window + sidebar
├── Models/                      # Data types ported from HoloCore
│   ├── DeskZone.cs
│   ├── SensingStrategy.cs
│   ├── SignalQuality.cs
│   ├── TapFeatureVector.cs
│   ├── ZoneActionConfiguration.cs
│   └── Profile.cs
├── DSP/                         # Signal processing ported from HoloCore
│   ├── FFT.cs                   # Radix-2 FFT
│   ├── StreamingTapDetector.cs  # Adaptive onset detection + ImpactEventGate
│   ├── TapFeatureExtractor.cs   # Feature extraction (stub)
│   └── TapClassifier.cs         # Zone classifier (stub)
├── Services/
│   ├── AudioCaptureService.cs   # NAudio microphone capture
│   ├── TrayManager.cs           # NotifyIcon system tray
│   ├── ActionDispatcherService.cs # Windows-local action execution
│   └── ProfileService.cs        # JSON profile persistence
└── Views/
    ├── DeskView.xaml / .cs       # 4-zone tap map
    ├── CalibrationView.xaml / .cs # Guided calibration (stub)
    ├── ActionsView.xaml / .cs    # Zone action editor (stub)
    ├── EvaluationView.xaml / .cs # 60-tap accuracy test (stub)
    └── DiagnosticsView.xaml / .cs # Hardware diagnostics
```

## Build & Run

### Prerequisites
- .NET 8 SDK
- Visual Studio 2022 (or `dotnet` CLI)
- A laptop with a built-in microphone

### Build
```sh
cd windows/HoloWindows
dotnet build
```

### Run
```sh
dotnet run
```

## TODOs (Missing Features)

### High Priority
- [ ] Port full `TapFeatureExtractor` from Swift (passive, active, hybrid features)
- [ ] Port full `TapClassifier` (regularized linear model + nearest-example rejection)
- [ ] Implement guided calibration session (10 taps per zone with quality gates)
- [ ] Implement live evaluation (60-tap accuracy test with confusion matrix)
- [ ] Persist trained classifier to profile JSON

### Medium Priority
- [ ] Proper `.ico` icon for system tray (replace programmatic `H` icon)
- [ ] Audio device selection (choose input/output)
- [ ] Active probe support (play chirp through speakers during detection)
- [ ] Raw WAV debug recording (`WaveFileWriter` port)
- [ ] Zone action editor UI with pickers for each action type

### Lower Priority
- [ ] Approach comparison (passive vs active vs hybrid)
- [ ] Signal diagnostics with live spectrum display
- [ ] Windows Graphics Capture for screenshot actions
- [ ] Shell command execution with sandbox-awareness
- [ ] Shortcuts integration (PowerShell equivalent)

## Notes

- The Swift DSP code (`HoloCore`) is platform-independent C-like code and ports cleanly to C#. The math is identical.
- The macOS UI (`HoloApp`) is fully SwiftUI/AppKit — completely replaced with WPF for Windows.
- NAudio is the only external NuGet dependency; all DSP and models are hand-ported.
