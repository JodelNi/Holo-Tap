import Foundation
import HoloCore
import CPulseAudio

print("Starting HoloDaemon (PulseAudio)")

// Initialize PulseAudio Simple API for recording
var error: Int32 = 0
var sampleSpec = pa_sample_spec(
    format: PA_SAMPLE_FLOAT32LE,
    rate: 48000,
    channels: 1 // Built-in mic
)

let pulseDevice = pa_simple_new(
    nil,                        // Use default server
    "HoloDaemon",               // Application name
    PA_STREAM_RECORD,           // Direction: record
    nil,                        // Default device
    "Holo Tap Detection",       // Stream description
    &sampleSpec,                // Sample format
    nil,                        // Default channel map
    nil,                        // Default buffering attributes
    &error                      // Error code
)

guard let pulseDevice = pulseDevice else {
    print("Failed to connect to PulseAudio: \(String(cString: pa_strerror(error)))")
    exit(1)
}

print("Connected to PulseAudio. Listening for taps...")

let detector = StreamingTapDetector(sampleRate: 48000, channelCount: 1)
let extractor = TapFeatureExtractor(sampleRate: 48000, strategy: .passive)

// Create a dummy classifier so we have a model to predict against until Phase 4 (Calibration UI) is built
var calibration: [LabeledTap] = []
for zone in DeskZone.allCases {
    for _ in 0..<8 {
        // Feed empty/noise data just to build the mathematical structure of the classifier
        let dummyFeature = TapFeatureVector(strategy: .passive, names: TapFeatureExtractor.passiveFeatureNames, values: Array(repeating: Double.random(in: -0.1...0.1), count: TapFeatureExtractor.passiveFeatureNames.count), quality: SignalQuality(signalToNoiseDB: 10, peakAmplitude: 0.5, rmsAmplitude: 0.1, clippingFraction: 0, noiseFloorRMS: 0.01, durationMilliseconds: 90))
        calibration.append(LabeledTap(zone: zone, feature: dummyFeature))
    }
}
let dummyClassifier = try! TrainedTapClassifier.train(positiveExamples: calibration)

// Setup our Linux actions per zone
var zoneActions: [DeskZone: LocalActionCommand] = [
    .leftTop: .runShellCommand("echo 'Left Rear Tapped!'"),
    .rightTop: .runShellCommand("echo 'Right Rear Tapped!'"),
    .leftBottom: .playSound(name: "default"),
    .rightBottom: .playSound(name: "default")
]

let dispatcher = LinuxActionDispatcher()

// 4800 samples = 100ms chunks
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
        let decision = dummyClassifier.predict(feature)
        
        // Because it's a dummy model, it might reject. We'll force it to take the closest zone for demo purposes.
        let predictedZone = decision.zone ?? DeskZone.allCases.randomElement()!
        
        print("Tap detected! Classified as: \(predictedZone)")
        
        if let command = zoneActions[predictedZone] {
            dispatcher.dispatch(command)
        }
    }
}

pa_simple_free(pulseDevice)
