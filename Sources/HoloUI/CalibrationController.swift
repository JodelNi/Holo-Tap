import Foundation
import HoloCore
import Gtk
import GLib
import CGLib

public protocol CalibrationUIDelegate: AnyObject {
    func didUpdateCalibrationState(activeZone: DeskZone?, counts: [DeskZone: Int], isArmed: Bool)
    func calibrationDidComplete(profile: HoloProfile)
}

/// Manages the calibration flow. All state mutations happen on the GTK main
/// thread. The audio thread only appends taps to a locked buffer and never
/// reads controller state directly.
public class CalibrationController: AudioCaptureDelegate {
    public weak var delegate: CalibrationUIDelegate?

    private let audioService = LinuxAudioCaptureService()
    private let tapDetector = StreamingTapDetector(sampleRate: 48000, channelCount: 1)
    private let extractor = TapFeatureExtractor(sampleRate: 48000, strategy: .passive)

    // --- State only touched on the GTK main thread ---
    private var isArmed = false
    private var currentZone: DeskZone?
    private var processedTapCount = 0

    private let targetTapsPerZone = 10

    // --- Shared between threads, protected by tapLock ---
    private let tapLock = DispatchQueue(label: "com.holo.taplock")
    private var pendingTaps: [LabeledTap] = []

    // All taps that have been moved from the pending buffer to the main-thread
    // collection. Only read/written on the GTK main thread.
    private var collectedTaps: [LabeledTap] = []

    private var pollingSourceID: UInt32 = 0

    public init() {
        audioService.delegate = self
    }

    deinit {
        stopPolling()
    }

    public func start() {
        try? audioService.start()
        currentZone = DeskZone.allCases.first
        isArmed = false
        startPolling()
        notifyUI()
    }

    public func stop() {
        audioService.stop()
        stopPolling()
    }

    public func armCurrentZone() {
        guard currentZone != nil else { return }
        isArmed = true
        notifyUI()
    }

    public func undoLastTap() {
        guard let zone = currentZone else { return }
        if let index = collectedTaps.lastIndex(where: { $0.zone == zone }) {
            collectedTaps.remove(at: index)
            notifyUI()
        }
    }

    // MARK: - Audio thread callback

    /// Called from the PulseAudio background thread. This method must not read
    /// `isArmed`, `currentZone`, or any other main-thread state. It only feeds
    /// audio into the detector and, if a tap is found, appends it to the locked
    /// pending buffer. The GTK polling timer drains this buffer on the main thread.
    public func audioCaptureDidOutput(buffer: [Float]) {
        let taps = tapDetector.process(channels: [buffer])
        guard let tap = taps.first else { return }

        let feature = extractor.extract(from: tap)

        tapLock.sync {
            // We store zone as nil here; the main thread will assign the current
            // zone when it drains the buffer, ensuring no race on currentZone.
            pendingTaps.append(LabeledTap(zone: nil, feature: feature))
        }
    }

    // MARK: - GTK main thread polling

    private func startPolling() {
        // Poll every 100ms on the GTK main loop
        pollingSourceID = g_timeout_add(100, { userData -> gboolean in
            let controller = Unmanaged<CalibrationController>.fromOpaque(userData!).takeUnretainedValue()
            controller.drainPendingTaps()
            return 1 // G_SOURCE_CONTINUE
        }, Unmanaged.passUnretained(self).toOpaque())
    }

    private func stopPolling() {
        if pollingSourceID != 0 {
            g_source_remove(pollingSourceID)
            pollingSourceID = 0
        }
    }

    /// Called on the GTK main thread by the polling timer. Drains any taps that
    /// the audio thread deposited since the last poll.
    private func drainPendingTaps() {
        guard isArmed, let zone = currentZone else { return }

        let newTaps: [LabeledTap] = tapLock.sync {
            let taps = pendingTaps
            pendingTaps.removeAll()
            return taps
        }

        guard !newTaps.isEmpty else { return }

        let remaining = targetTapsPerZone - countForZone(zone)
        for tap in newTaps.prefix(remaining) {
            // Re-label with the current zone (safe because we are on the main thread)
            let labeled = LabeledTap(zone: zone, feature: tap.feature)
            collectedTaps.append(labeled)
        }

        let count = countForZone(zone)
        if count >= targetTapsPerZone {
            isArmed = false
            moveToNextZone()
        } else {
            notifyUI()
        }
    }

    // MARK: - Zone progression

    private func moveToNextZone() {
        let allZones = DeskZone.allCases
        guard let current = currentZone,
              let currentIndex = allZones.firstIndex(of: current) else { return }

        if currentIndex + 1 < allZones.count {
            currentZone = allZones[currentIndex + 1]
            notifyUI()
        } else {
            currentZone = nil
            finishCalibration()
        }
    }

    private func finishCalibration() {
        stopPolling()
        audioService.stop()

        do {
            let classifier = try TrainedTapClassifier.train(positiveExamples: collectedTaps)
            let summary = CalibrationSummary(
                sampleCount: collectedTaps.count,
                samplesPerZone: DeskZone.allCases.map { zone in
                    collectedTaps.filter({ $0.zone == zone }).count
                },
                leaveOneOutAccuracy: nil
            )
            let profile = HoloProfile(
                name: "Linux Desk",
                surfaceDescription: "Standard",
                laptopPositionNote: "Centered",
                classifier: classifier,
                calibration: summary
            )
            let store = try ProfileStore()
            try store.save(profile)
            print("Profile saved successfully.")
            delegate?.calibrationDidComplete(profile: profile)
        } catch {
            print("Failed to train and save classifier: \(error)")
        }
    }

    // MARK: - Helpers

    private func countForZone(_ zone: DeskZone) -> Int {
        collectedTaps.filter { $0.zone == zone }.count
    }

    private func allZoneCounts() -> [DeskZone: Int] {
        var counts: [DeskZone: Int] = [:]
        for zone in DeskZone.allCases {
            counts[zone] = countForZone(zone)
        }
        return counts
    }

    private func notifyUI() {
        delegate?.didUpdateCalibrationState(
            activeZone: currentZone,
            counts: allZoneCounts(),
            isArmed: isArmed
        )
    }
}
