import Foundation

/// Groups accepted zone decisions into single-tap or two-tap gestures.
///
/// The recognizer keeps no clock of its own. Every decision is made from the audio
/// timestamps the caller supplies, which keeps it synchronous, allocation-free, and
/// testable without an audio graph.
///
/// A tap is only held back when its zone begins a combo the current profile actually
/// binds. Every other zone is emitted immediately, so a profile with no combos
/// behaves exactly as it did before combos existed.
public struct TapSequenceRecognizer {
    public struct Outcome: Equatable, Sendable {
        /// Gestures to dispatch now, in the order they completed. A tap that was
        /// being held and did not find a partner is always first.
        public var gestures: [TapGesture]
        /// A tap held back to see whether a second tap completes a combo.
        public var pendingZone: DeskZone?
        /// Host time at which a held tap should be flushed as a plain single tap.
        public var pendingDeadlineSeconds: Double?

        public init(
            gestures: [TapGesture] = [],
            pendingZone: DeskZone? = nil,
            pendingDeadlineSeconds: Double? = nil
        ) {
            self.gestures = gestures
            self.pendingZone = pendingZone
            self.pendingDeadlineSeconds = pendingDeadlineSeconds
        }
    }

    /// Roughly double the detector's floor. `StreamingTapDetector` holds a 140 ms
    /// refractory after each completed 90 ms analysis window, so a second tap cannot
    /// be observed sooner than about 230 ms after the first onset. The gesture is two
    /// deliberate taps about half a second apart, not a trackpad-speed double tap.
    public static let defaultWindowSeconds = 0.45

    /// How far the window may be moved by the user.
    ///
    /// The lower bound is the detector's own floor rounded up: below about 230 ms a
    /// second tap cannot be observed at all, so a shorter window would look like a
    /// valid setting while making every combo unreachable. Past a second the wait on
    /// every single tap in a combo's first zone stops reading as one gesture, and
    /// unrelated taps begin pairing by accident.
    public static let windowRangeSeconds: ClosedRange<Double> = 0.25...1.0

    /// Forces any stored or entered value into the supported range. A non-finite value
    /// becomes the default rather than the nearest bound, because it carries no
    /// intent — it only ever arrives from a corrupt or hand-edited file, and letting
    /// it through would produce a deadline that never arrives.
    public static func clampedWindowSeconds(_ value: Double) -> Double {
        guard value.isFinite else { return defaultWindowSeconds }
        return min(max(value, windowRangeSeconds.lowerBound), windowRangeSeconds.upperBound)
    }

    public var windowSeconds: Double

    private var pending: (zone: DeskZone, hostTimeSeconds: Double)?

    public init(windowSeconds: Double = TapSequenceRecognizer.defaultWindowSeconds) {
        self.windowSeconds = windowSeconds
    }

    /// The tap currently being held back, if any.
    public var heldZone: DeskZone? { pending?.zone }

    /// Feeds one accepted zone decision through the state machine.
    ///
    /// - Parameters:
    ///   - comboPrefixes: zones that begin at least one bound combo. A tap outside
    ///     this set is never delayed.
    ///   - boundGestures: every gesture the profile binds, used to decide whether a
    ///     completed pair is worth emitting as a combo.
    public mutating func ingest(
        zone: DeskZone,
        hostTimeSeconds: Double,
        comboPrefixes: Set<DeskZone>,
        boundGestures: Set<TapGesture>
    ) -> Outcome {
        var flushed: [TapGesture] = []

        if let held = pending {
            pending = nil
            let elapsed = hostTimeSeconds - held.hostTimeSeconds
            let withinWindow = hostTimeSeconds.isFinite && elapsed >= 0 && elapsed <= windowSeconds
            if withinWindow {
                let combo = TapGesture.pair(held.zone, zone)
                // Matching is greedy and non-overlapping: a completed combo consumes
                // both taps, so a third tap starts a fresh gesture.
                if boundGestures.contains(combo) {
                    return Outcome(gestures: [combo])
                }
            }
            flushed.append(.single(held.zone))
        }

        // A non-finite timestamp cannot be timed against, so the tap is dispatched
        // immediately rather than held for a deadline that would never arrive.
        guard hostTimeSeconds.isFinite, comboPrefixes.contains(zone) else {
            flushed.append(.single(zone))
            return Outcome(gestures: flushed)
        }

        pending = (zone: zone, hostTimeSeconds: hostTimeSeconds)
        return Outcome(
            gestures: flushed,
            pendingZone: zone,
            pendingDeadlineSeconds: hostTimeSeconds + windowSeconds
        )
    }

    /// Emits a held tap as a plain single tap. Called when its window expires.
    public mutating func flush() -> [TapGesture] {
        guard let held = pending else { return [] }
        pending = nil
        return [.single(held.zone)]
    }

    /// Discards a held tap without dispatching it. Used when listening stops or the
    /// Desk surface is left, so a half-finished combo cannot complete much later.
    public mutating func reset() {
        pending = nil
    }
}
