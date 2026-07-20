using Holo.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace Holo.Views;

public partial class DiagnosticsView : UserControl
{
    private readonly AudioCaptureService _audioService;

    public DiagnosticsView(AudioCaptureService audioService)
    {
        InitializeComponent();
        _audioService = audioService;

        DeviceInfo.Text = $"Sample Rate: {audioService.SampleRate} Hz\n" +
                          $"Channels: {audioService.ChannelCount}\n" +
                          $"Listening: {audioService.IsListening}";
    }
}
