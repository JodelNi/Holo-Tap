import XCTest
@testable import HoloCore

final class DetectorTests: XCTestCase {
    func testDetectorIgnoresSteadyBackgroundAndFindsImpulse() {
        let sampleRate = 48_000.0
        let detector = StreamingTapDetector(sampleRate: sampleRate, channelCount: 1)
        var events: [DetectedTap] = []

        for _ in 0..<80 {
            events += detector.process(channels: [Array(repeating: 0.0004, count: 512)])
        }
        XCTAssertTrue(events.isEmpty)

        var impulse = Array(repeating: Float(0.0004), count: 512)
        impulse[120] = 0.2
        impulse[121] = -0.13
        events += detector.process(channels: [impulse])
        for _ in 0..<10 {
            events += detector.process(channels: [Array(repeating: 0.0002, count: 512)])
        }

        XCTAssertEqual(events.count, 1)
        XCTAssertEqual(events[0].channels[0].count, detector.analysisWindowSamples)
        XCTAssertGreaterThan(events[0].onsetOffset, 0)
    }

    func testLateOnsetInLargeHostBufferStaysInsideAnalysisWindow() {
        // macOS can deliver ~100 ms buffers no matter what we request.
        // We want to make it so a tap near the end of a buffer still gets registered.
        let sampleRate = 48_000.0
        let detector = StreamingTapDetector(
            sampleRate: sampleRate,
            channelCount: 1,
            warmUpDuration: 0
        )
        var events: [DetectedTap] = []
        for _ in 0..<40 {
            events += detector.process(channels: [Array(repeating: 0.0004, count: 512)])
        }
        XCTAssertTrue(events.isEmpty)

        var large = Array(repeating: Float(0.0004), count: 4_800)
        addTapBurst(to: &large, at: 4_600, sampleRate: sampleRate)
        events += detector.process(channels: [large])
        for _ in 0..<10 {
            events += detector.process(channels: [Array(repeating: 0.0002, count: 512)])
        }

        XCTAssertEqual(events.count, 1, "A tap late in a large host buffer must still be emitted")
        guard let event = events.first else { return }
        XCTAssertEqual(event.channels[0].count, detector.analysisWindowSamples)
        XCTAssertEqual(event.onsetOffset, detector.preRollSamples)

        let samples = event.channels[0]
        let peakIndex = samples.indices.max { abs(samples[$0]) < abs(samples[$1]) } ?? 0
        XCTAssertGreaterThan(
            abs(samples[peakIndex]), 0.1,
            "The emitted window must contain the tap itself, not just pre-tap audio"
        )
        XCTAssertLessThanOrEqual(
            abs(peakIndex - event.onsetOffset), 4,
            "The tap's raw peak must sit at the reported onset offset"
        )
    }

    func testOnsetOffsetIsIndependentOfPositionInDeliveredBuffer() {
        // The same tap, early or late in a buffer, must land at the same
        // offset — otherwise features learn buffer timing, not the tap.
        let sampleRate = 48_000.0
        var offsets: [Int] = []

        for (bufferSize, tapIndex) in [(512, 120), (4_800, 4_600)] {
            let detector = StreamingTapDetector(
                sampleRate: sampleRate,
                channelCount: 1,
                warmUpDuration: 0
            )
            var events: [DetectedTap] = []
            for _ in 0..<40 {
                events += detector.process(channels: [Array(repeating: 0.0004, count: 512)])
            }
            var buffer = Array(repeating: Float(0.0004), count: bufferSize)
            addTapBurst(to: &buffer, at: tapIndex, sampleRate: sampleRate)
            events += detector.process(channels: [buffer])
            for _ in 0..<10 {
                events += detector.process(channels: [Array(repeating: 0.0002, count: 512)])
            }
            XCTAssertEqual(events.count, 1, "Tap at index \(tapIndex) of \(bufferSize) must be emitted")
            offsets.append(events.first?.onsetOffset ?? -1)
        }

        XCTAssertEqual(
            offsets.first, offsets.last,
            "Onset offset must not depend on where the tap fell in the delivered buffer"
        )
    }

    func testLateOnsetTrimKeepsChannelsAlignedSampleExact() {
        // The trim must shorten every channel by the same amount — if it
        // didn't, the interchannel delay feature would quietly break.
        // So check that a known cross-channel lag survives.
        let sampleRate = 48_000.0
        let detector = StreamingTapDetector(
            sampleRate: sampleRate,
            channelCount: 2,
            warmUpDuration: 0
        )
        var events: [DetectedTap] = []
        let quiet = Array(repeating: Float(0.0004), count: 512)
        for _ in 0..<40 {
            events += detector.process(channels: [quiet, quiet])
        }
        XCTAssertTrue(events.isEmpty)

        let interchannelLag = 3
        var left = Array(repeating: Float(0.0004), count: 4_800)
        var right = left
        addTapBurst(to: &left, at: 4_600, sampleRate: sampleRate)
        addTapBurst(to: &right, at: 4_600 + interchannelLag, sampleRate: sampleRate)
        events += detector.process(channels: [left, right])
        for _ in 0..<10 {
            let calm = Array(repeating: Float(0.0002), count: 512)
            events += detector.process(channels: [calm, calm])
        }

        XCTAssertEqual(events.count, 1)
        guard let event = events.first else { return }
        XCTAssertEqual(event.channels.count, 2)
        XCTAssertEqual(event.channels[0].count, detector.analysisWindowSamples)
        XCTAssertEqual(event.channels[1].count, detector.analysisWindowSamples)

        let peaks = event.channels.map { samples in
            samples.indices.max { abs(samples[$0]) < abs(samples[$1]) } ?? 0
        }
        XCTAssertGreaterThan(abs(event.channels[0][peaks[0]]), 0.1)
        XCTAssertGreaterThan(abs(event.channels[1][peaks[1]]), 0.1)
        XCTAssertLessThanOrEqual(abs(peaks[0] - event.onsetOffset), 4)
        XCTAssertEqual(
            peaks[1] - peaks[0], interchannelLag,
            "Trimming must remove the same leading sample count from every channel"
        )
    }

    func testDetectorHonorsRefractoryPeriod() {
        let detector = StreamingTapDetector(
            sampleRate: 48_000,
            channelCount: 1,
            analysisDuration: 0.025,
            warmUpDuration: 0
        )
        var chunk = Array(repeating: Float.zero, count: 512)
        chunk[100] = 0.3
        var events: [DetectedTap] = []
        for _ in 0..<4 { events += detector.process(channels: [chunk]) }
        XCTAssertLessThanOrEqual(events.count, 1)
    }

    func testDetectorDoesNotTriggerOnActiveProbe() {
        for sampleRate in [44_100.0, 48_000.0] {
            let detector = StreamingTapDetector(
                sampleRate: sampleRate,
                channelCount: 1,
                warmUpDuration: 0
            )
            let chirp = ActiveProbe.chirp(sampleRate: sampleRate).map { $0 * 0.55 }
            let periodSamples = Int(sampleRate * 0.120)
            var signal: [Float] = []
            for _ in 0..<12 {
                signal.append(contentsOf: chirp)
                signal.append(contentsOf: Array(repeating: 0, count: periodSamples - chirp.count))
            }

            var events: [DetectedTap] = []
            var offset = 0
            while offset < signal.count {
                let end = min(offset + 512, signal.count)
                events += detector.process(channels: [Array(signal[offset..<end])])
                offset = end
            }

            XCTAssertTrue(events.isEmpty, "Probe self-triggered at \(sampleRate) Hz")
        }
    }

    func testDetectorLearnsElevatedBackgroundBeforeEmittingTap() {
        let sampleRate = 48_000.0
        let detector = StreamingTapDetector(sampleRate: sampleRate, channelCount: 1)
        var generator = DeterministicNoise(state: 0x484F4C4F)
        var backgroundEvents: [DetectedTap] = []

        for _ in 0..<120 {
            backgroundEvents += detector.process(channels: [backgroundChunk(generator: &generator)])
        }

        XCTAssertTrue(backgroundEvents.isEmpty, "Steady room noise must not look like repeated taps")
        XCTAssertGreaterThan(detector.noiseFloorRMS, 0.002)

        var tap = backgroundChunk(generator: &generator)
        tap[120] = 0.48
        tap[124] = -0.31
        var tapEvents = detector.process(channels: [tap])
        for _ in 0..<10 {
            tapEvents += detector.process(channels: [backgroundChunk(generator: &generator)])
        }

        XCTAssertEqual(tapEvents.count, 1, "A real impulse must remain detectable above the learned room floor")
    }

    func testImpactGateRejectsPlosiveSpeechButKeepsAResonantTap() {
        let sampleRate = 48_000.0
        let onset = 576
        let speech = DetectedTap(
            channels: [candidateSignal(sampleRate: sampleRate, onset: onset, kind: .speech)],
            onsetOffset: onset,
            streamSampleIndex: 0,
            noiseFloorRMS: 0.001
        )
        let tap = DetectedTap(
            channels: [candidateSignal(sampleRate: sampleRate, onset: onset, kind: .tap)],
            onsetOffset: onset,
            streamSampleIndex: 0,
            noiseFloorRMS: 0.001
        )

        let speechMetrics = ImpactEventGate.metrics(for: speech, sampleRate: sampleRate)
        XCTAssertNotNil(speechMetrics)
        XCTAssertGreaterThan(speechMetrics?.effectiveDurationSeconds ?? 0, 0.04)
        XCTAssertFalse(ImpactEventGate.accepts(speech, sampleRate: sampleRate))
        XCTAssertTrue(ImpactEventGate.accepts(tap, sampleRate: sampleRate))
    }

    func testDetectorDoesNotEmitPlosiveSpeechCandidate() {
        let sampleRate = 48_000.0
        let detector = StreamingTapDetector(
            sampleRate: sampleRate,
            channelCount: 1,
            warmUpDuration: 0
        )
        let signal = candidateSignal(
            sampleRate: sampleRate,
            onset: 900,
            kind: .speech,
            duration: 0.18
        )
        var events: [DetectedTap] = []
        var offset = 0
        while offset < signal.count {
            let end = min(offset + 512, signal.count)
            events += detector.process(channels: [Array(signal[offset..<end])])
            offset = end
        }

        XCTAssertTrue(events.isEmpty, "A plosive followed by voiced energy must not be emitted as a tap")
    }

    /// A short burst shaped like a real fingertip tap, with enough energy
    /// to stay detectable even when spread across a ~100 ms buffer.
    private func addTapBurst(to buffer: inout [Float], at start: Int, sampleRate: Double) {
        for index in 0..<min(200, buffer.count - start) {
            let time = Double(index) / sampleRate
            let impact = 0.35 * exp(-time * 2_100) * cos(2 * Double.pi * 1_900 * time)
            let resonance = 0.11 * exp(-time * 240) * sin(2 * Double.pi * 620 * time)
            buffer[start + index] += Float(impact + resonance)
        }
    }

    private func backgroundChunk(generator: inout DeterministicNoise) -> [Float] {
        var chunk = Array(repeating: Float.zero, count: 512)
        for start in stride(from: 0, to: chunk.count, by: 8) {
            let value = Float((generator.unit() * 2 - 1) * 0.014)
            for index in start..<min(start + 8, chunk.count) {
                chunk[index] = value
            }
        }
        return chunk
    }

    private enum CandidateKind {
        case tap
        case speech
    }

    private func candidateSignal(
        sampleRate: Double,
        onset: Int,
        kind: CandidateKind,
        duration: Double = 0.090
    ) -> [Float] {
        let count = max(Int(sampleRate * duration), onset + 1)
        return (0..<count).map { index in
            let background = 0.0007 * sin(2 * Double.pi * 83 * Double(index) / sampleRate)
            guard index >= onset else { return Float(background) }
            let time = Double(index - onset) / sampleRate
            switch kind {
            case .tap:
                let impact = 0.24 * exp(-time * 2_100) * cos(2 * Double.pi * 1_900 * time)
                let resonance = 0.075 * exp(-time * 24) * sin(2 * Double.pi * 620 * time)
                return Float(background + impact + resonance)
            case .speech:
                let plosive = 0.24 * exp(-time * 850) * sin(2 * Double.pi * 2_700 * time)
                let voiceEnvelope = min(time / 0.010, 1)
                let voiced = 0.075 * voiceEnvelope * (
                    sin(2 * Double.pi * 145 * time)
                        + 0.55 * sin(2 * Double.pi * 290 * time)
                        + 0.30 * sin(2 * Double.pi * 435 * time)
                )
                return Float(background + plosive + voiced)
            }
        }
    }
}

private struct DeterministicNoise {
    var state: UInt64

    mutating func unit() -> Double {
        state = state &* 6_364_136_223_846_793_005 &+ 1
        return Double(state >> 11) / Double(UInt64.max >> 11)
    }
}
