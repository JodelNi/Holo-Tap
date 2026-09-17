import Foundation
import HoloCore
import CPulseAudio

print("Starting HoloDaemon (PulseAudio)")

// Try to load a calibrated profile from disk
let classifier: TrainedTapClassifier
do {
    let store = try ProfileStore()
    let profiles = try store.loadAll()
    if let latest = profiles.first {
        classifier = latest.classifier
        print("Loaded calibrated profile: \(latest.name)")
    } else {
        print("No calibrated profile found. Run HoloUI to calibrate first.")
        exit(1)
    }
} catch {
    print("Failed to load profiles: \(error)")
    exit(1)
}

// Initialize PulseAudio Simple API for recording
var error: Int32 = 0
var sampleSpec = pa_sample_spec(
    format: PA_SAMPLE_FLOAT32LE,
    rate: 48000,
    channels: 1
)

let pulseDevice = pa_simple_new(
    nil,
    "HoloDaemon",
    PA_STREAM_RECORD,
    nil,
    "Holo Tap Detection",
    &sampleSpec,
    nil,
    nil,
    &error
)

guard let pulseDevice = pulseDevice else {
    print("Failed to connect to PulseAudio: \(String(cString: pa_strerror(error)))")
    exit(1)
}

print("Connected to PulseAudio. Listening for taps...")

let detector = StreamingTapDetector(sampleRate: 48000, channelCount: 1)
let extractor = TapFeatureExtractor(sampleRate: 48000, strategy: classifier.strategy)

var zoneActions: [DeskZone: LocalActionCommand] = [
    .leftTop: .runShellCommand("echo 'Left Rear Tapped!'"),
    .rightTop: .runShellCommand("echo 'Right Rear Tapped!'"),
    .leftBottom: .runShellCommand("echo 'Left Front Tapped!'"),
    .rightBottom: .runShellCommand("echo 'Right Front Tapped!'")
]

let dispatcher = LinuxActionDispatcher()

let chunkSamples = 4800
let bufferSize = chunkSamples * MemoryLayout<Float>.size
var buffer = [Float](repeating: 0, count: chunkSamples)

while true {
    if pa_simple_read(pulseDevice, &buffer, bufferSize, &error) < 0 {
        print("pa_simple_read() failed: \(String(cString: pa_strerror(error)))")
        break
    }

    let taps = detector.process(channels: [buffer])
    for tap in taps {
        let feature = extractor.extract(from: tap)
        let decision = classifier.predict(feature)

        if let zone = decision.zone {
            print("Tap detected: \(zone.displayName) (confidence: \(String(format: "%.0f%%", decision.confidence * 100)))")

            if let command = zoneActions[zone] {
                dispatcher.dispatch(command)
            }
        } else if let reason = decision.rejectionReason {
            print("Tap rejected: \(reason.displayName)")
        }
    }
}

pa_simple_free(pulseDevice)
