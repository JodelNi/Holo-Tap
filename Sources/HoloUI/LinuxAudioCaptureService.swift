import Foundation
import CPulseAudio

public protocol AudioCaptureDelegate: AnyObject {
    func audioCaptureDidOutput(buffer: [Float])
}

public class LinuxAudioCaptureService {
    public weak var delegate: AudioCaptureDelegate?

    private var isRunning = false
    private let queue = DispatchQueue(label: "com.holo.audiocapture", qos: .userInteractive)

    public init() {}

    public func start() throws {
        guard !isRunning else { return }
        isRunning = true

        // The queue must keep the service alive while recording. Using [weak self]
        // here would allow deallocation before the loop even starts.
        queue.async {
            self.recordLoop()
        }
    }

    public func stop() {
        isRunning = false
    }

    private func recordLoop() {
        var error: Int32 = 0
        var sampleSpec = pa_sample_spec(
            format: PA_SAMPLE_FLOAT32LE,
            rate: 48000,
            channels: 1
        )

        let pulseDevice = pa_simple_new(
            nil,
            "HoloUI",
            PA_STREAM_RECORD,
            nil,
            "Holo Tap Calibration",
            &sampleSpec,
            nil,
            nil,
            &error
        )

        guard let device = pulseDevice else {
            print("Failed to connect to PulseAudio: \(String(cString: pa_strerror(error)))")
            return
        }

        defer { pa_simple_free(device) }

        let chunkSamples = 4800 // 100ms at 48kHz
        let bufferSize = chunkSamples * MemoryLayout<Float>.size
        var buffer = [Float](repeating: 0, count: chunkSamples)

        print("PulseAudio recording started.")

        while isRunning {
            if pa_simple_read(device, &buffer, bufferSize, &error) < 0 {
                print("pa_simple_read() failed: \(String(cString: pa_strerror(error)))")
                break
            }
            delegate?.audioCaptureDidOutput(buffer: buffer)
        }

        print("PulseAudio recording stopped.")
    }
}
