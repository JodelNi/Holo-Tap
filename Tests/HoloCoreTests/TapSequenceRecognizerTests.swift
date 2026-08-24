import XCTest
@testable import HoloCore

final class TapSequenceRecognizerTests: XCTestCase {
    private let doubleLeftRear = TapGesture.pair(.leftTop, .leftTop)
    private let leftRearToRightFront = TapGesture.pair(.leftTop, .rightBottom)

    // MARK: - Zones that begin no combo are never delayed

    func testZoneOutsideAnyComboDispatchesImmediately() {
        var recognizer = TapSequenceRecognizer()
        let outcome = recognizer.ingest(
            zone: .rightTop,
            hostTimeSeconds: 100,
            comboPrefixes: [.leftTop],
            boundGestures: [doubleLeftRear]
        )
        XCTAssertEqual(outcome.gestures, [.single(.rightTop)])
        XCTAssertNil(outcome.pendingZone)
        XCTAssertNil(recognizer.heldZone)
    }

    func testProfileWithNoCombosNeverHoldsAnyZone() {
        var recognizer = TapSequenceRecognizer()
        for (index, zone) in DeskZone.allCases.enumerated() {
            let outcome = recognizer.ingest(
                zone: zone,
                hostTimeSeconds: 100 + Double(index),
                comboPrefixes: [],
                boundGestures: []
            )
            XCTAssertEqual(outcome.gestures, [.single(zone)])
            XCTAssertNil(outcome.pendingZone)
        }
    }

    // MARK: - Holding and flushing

    func testComboPrefixIsHeldWithADeadlineAndFlushesAsASingle() throws {
        var recognizer = TapSequenceRecognizer(windowSeconds: 0.45)
        let outcome = recognizer.ingest(
            zone: .leftTop,
            hostTimeSeconds: 100,
            comboPrefixes: [.leftTop],
            boundGestures: [doubleLeftRear]
        )
        XCTAssertTrue(outcome.gestures.isEmpty)
        XCTAssertEqual(outcome.pendingZone, .leftTop)
        XCTAssertEqual(try XCTUnwrap(outcome.pendingDeadlineSeconds), 100.45, accuracy: 1e-9)
        XCTAssertEqual(recognizer.heldZone, .leftTop)

        XCTAssertEqual(recognizer.flush(), [.single(.leftTop)])
        XCTAssertNil(recognizer.heldZone)
        XCTAssertTrue(recognizer.flush().isEmpty)
    }

    func testResetDiscardsAHeldTapWithoutDispatchingIt() {
        var recognizer = TapSequenceRecognizer()
        _ = recognizer.ingest(
            zone: .leftTop,
            hostTimeSeconds: 100,
            comboPrefixes: [.leftTop],
            boundGestures: [doubleLeftRear]
        )
        recognizer.reset()
        XCTAssertNil(recognizer.heldZone)
        XCTAssertTrue(recognizer.flush().isEmpty)
    }

    // MARK: - Completing a combo

    func testSameZoneTwiceInsideTheWindowEmitsOneCombo() {
        var recognizer = TapSequenceRecognizer(windowSeconds: 0.45)
        _ = recognizer.ingest(
            zone: .leftTop,
            hostTimeSeconds: 100,
            comboPrefixes: [.leftTop],
            boundGestures: [doubleLeftRear]
        )
        let outcome = recognizer.ingest(
            zone: .leftTop,
            hostTimeSeconds: 100.3,
            comboPrefixes: [.leftTop],
            boundGestures: [doubleLeftRear]
        )
        XCTAssertEqual(outcome.gestures, [doubleLeftRear])
        XCTAssertNil(outcome.pendingZone)
        XCTAssertNil(recognizer.heldZone)
    }

    func testTwoDifferentZonesInsideTheWindowEmitOneCombo() {
        var recognizer = TapSequenceRecognizer(windowSeconds: 0.45)
        let prefixes: Set<DeskZone> = [.leftTop]
        let bound: Set<TapGesture> = [leftRearToRightFront]
        _ = recognizer.ingest(zone: .leftTop, hostTimeSeconds: 10, comboPrefixes: prefixes, boundGestures: bound)
        let outcome = recognizer.ingest(
            zone: .rightBottom,
            hostTimeSeconds: 10.4,
            comboPrefixes: prefixes,
            boundGestures: bound
        )
        XCTAssertEqual(outcome.gestures, [leftRearToRightFront])
    }

    func testAtTheWindowBoundaryTheComboStillCompletes() {
        // 5.0 and 5.5 are exactly representable, so this really is the boundary.
        var recognizer = TapSequenceRecognizer(windowSeconds: 0.5)
        _ = recognizer.ingest(zone: .leftTop, hostTimeSeconds: 5, comboPrefixes: [.leftTop], boundGestures: [doubleLeftRear])
        let outcome = recognizer.ingest(
            zone: .leftTop,
            hostTimeSeconds: 5.5,
            comboPrefixes: [.leftTop],
            boundGestures: [doubleLeftRear]
        )
        XCTAssertEqual(outcome.gestures, [doubleLeftRear])
    }

    // MARK: - Pairs that do not match

    func testUnboundPairEmitsBothTapsAsSinglesInOrder() {
        var recognizer = TapSequenceRecognizer(windowSeconds: 0.45)
        _ = recognizer.ingest(zone: .leftTop, hostTimeSeconds: 1, comboPrefixes: [.leftTop], boundGestures: [doubleLeftRear])
        let outcome = recognizer.ingest(
            zone: .rightTop,
            hostTimeSeconds: 1.2,
            comboPrefixes: [.leftTop],
            boundGestures: [doubleLeftRear]
        )
        XCTAssertEqual(outcome.gestures, [.single(.leftTop), .single(.rightTop)])
        XCTAssertNil(outcome.pendingZone)
    }

    func testSecondTapAfterTheWindowEmitsTwoSingles() throws {
        var recognizer = TapSequenceRecognizer(windowSeconds: 0.45)
        _ = recognizer.ingest(zone: .leftTop, hostTimeSeconds: 1, comboPrefixes: [.leftTop], boundGestures: [doubleLeftRear])
        let outcome = recognizer.ingest(
            zone: .leftTop,
            hostTimeSeconds: 1.6,
            comboPrefixes: [.leftTop],
            boundGestures: [doubleLeftRear]
        )
        // The held tap is flushed, and the late tap is itself a combo prefix.
        XCTAssertEqual(outcome.gestures, [.single(.leftTop)])
        XCTAssertEqual(outcome.pendingZone, .leftTop)
        XCTAssertEqual(try XCTUnwrap(outcome.pendingDeadlineSeconds), 2.05, accuracy: 1e-9)
    }

    func testFlushedTapIsFollowedByANewHoldWhenTheSecondTapIsAlsoAPrefix() {
        var recognizer = TapSequenceRecognizer(windowSeconds: 0.45)
        let prefixes: Set<DeskZone> = [.leftTop, .rightTop]
        let bound: Set<TapGesture> = [doubleLeftRear, .pair(.rightTop, .rightBottom)]
        _ = recognizer.ingest(zone: .leftTop, hostTimeSeconds: 1, comboPrefixes: prefixes, boundGestures: bound)
        let outcome = recognizer.ingest(zone: .rightTop, hostTimeSeconds: 1.2, comboPrefixes: prefixes, boundGestures: bound)
        XCTAssertEqual(outcome.gestures, [.single(.leftTop)])
        XCTAssertEqual(outcome.pendingZone, .rightTop)
        XCTAssertEqual(recognizer.heldZone, .rightTop)
    }

    // MARK: - Matching does not overlap

    func testFourTapsMatchTheSameComboTwiceWithoutOverlapping() {
        var recognizer = TapSequenceRecognizer(windowSeconds: 0.45)
        let prefixes: Set<DeskZone> = [.leftTop]
        let bound: Set<TapGesture> = [doubleLeftRear]
        var emitted: [TapGesture] = []
        for time in [1.0, 1.3, 1.6, 1.9] {
            let outcome = recognizer.ingest(
                zone: .leftTop,
                hostTimeSeconds: time,
                comboPrefixes: prefixes,
                boundGestures: bound
            )
            emitted.append(contentsOf: outcome.gestures)
        }
        XCTAssertEqual(emitted, [doubleLeftRear, doubleLeftRear])
        XCTAssertNil(recognizer.heldZone)
    }

    // MARK: - Defensive timing

    func testNonFiniteTimestampDispatchesImmediatelyRatherThanHolding() {
        for timestamp in [Double.nan, .infinity, -.infinity] {
            var recognizer = TapSequenceRecognizer()
            let outcome = recognizer.ingest(
                zone: .leftTop,
                hostTimeSeconds: timestamp,
                comboPrefixes: [.leftTop],
                boundGestures: [doubleLeftRear]
            )
            XCTAssertEqual(outcome.gestures, [.single(.leftTop)])
            XCTAssertNil(outcome.pendingZone)
            XCTAssertNil(recognizer.heldZone)
        }
    }

    func testBackwardsTimestampDoesNotCompleteACombo() {
        var recognizer = TapSequenceRecognizer(windowSeconds: 0.45)
        _ = recognizer.ingest(zone: .leftTop, hostTimeSeconds: 100, comboPrefixes: [.leftTop], boundGestures: [doubleLeftRear])
        let outcome = recognizer.ingest(
            zone: .leftTop,
            hostTimeSeconds: 99,
            comboPrefixes: [.leftTop],
            boundGestures: [doubleLeftRear]
        )
        XCTAssertEqual(outcome.gestures, [.single(.leftTop)])
        XCTAssertEqual(outcome.pendingZone, .leftTop)
    }

    // MARK: - The user-adjustable window

    func testClampingKeepsTheWindowInsideTheSupportedRange() {
        let range = TapSequenceRecognizer.windowRangeSeconds
        XCTAssertEqual(TapSequenceRecognizer.clampedWindowSeconds(0.01), range.lowerBound)
        XCTAssertEqual(TapSequenceRecognizer.clampedWindowSeconds(-3), range.lowerBound)
        XCTAssertEqual(TapSequenceRecognizer.clampedWindowSeconds(9.5), range.upperBound)
        XCTAssertEqual(TapSequenceRecognizer.clampedWindowSeconds(range.lowerBound), range.lowerBound)
        XCTAssertEqual(TapSequenceRecognizer.clampedWindowSeconds(range.upperBound), range.upperBound)
        XCTAssertEqual(TapSequenceRecognizer.clampedWindowSeconds(0.6), 0.6)
    }

    func testNonFiniteWindowFallsBackToTheDefaultRatherThanABound() {
        for value in [Double.nan, .infinity, -.infinity] {
            XCTAssertEqual(
                TapSequenceRecognizer.clampedWindowSeconds(value),
                TapSequenceRecognizer.defaultWindowSeconds
            )
        }
    }

    func testTheDefaultWindowSitsInsideTheAdjustableRange() {
        XCTAssertTrue(
            TapSequenceRecognizer.windowRangeSeconds
                .contains(TapSequenceRecognizer.defaultWindowSeconds)
        )
        // The floor has to stay above the detector's own limit: a 140 ms refractory
        // after each 90 ms analysis window means no second tap before ~230 ms.
        XCTAssertGreaterThan(TapSequenceRecognizer.windowRangeSeconds.lowerBound, 0.23)
    }

    func testAShortenedWindowRejectsAPairTheDefaultWouldHaveAccepted() {
        let bound: Set<TapGesture> = [doubleLeftRear]
        let prefixes: Set<DeskZone> = [.leftTop]

        var tight = TapSequenceRecognizer(windowSeconds: 0.25)
        _ = tight.ingest(zone: .leftTop, hostTimeSeconds: 1, comboPrefixes: prefixes, boundGestures: bound)
        let tightOutcome = tight.ingest(
            zone: .leftTop,
            hostTimeSeconds: 1.4,
            comboPrefixes: prefixes,
            boundGestures: bound
        )
        XCTAssertEqual(tightOutcome.gestures, [.single(.leftTop)])
        XCTAssertEqual(tightOutcome.pendingZone, .leftTop)

        var relaxed = TapSequenceRecognizer(windowSeconds: 1.0)
        _ = relaxed.ingest(zone: .leftTop, hostTimeSeconds: 1, comboPrefixes: prefixes, boundGestures: bound)
        let relaxedOutcome = relaxed.ingest(
            zone: .leftTop,
            hostTimeSeconds: 1.4,
            comboPrefixes: prefixes,
            boundGestures: bound
        )
        XCTAssertEqual(relaxedOutcome.gestures, [doubleLeftRear])
    }

    // MARK: - Gesture value semantics

    func testGestureRejectsLengthsOutsideOneToTwo() {
        XCTAssertNil(TapGesture(zones: []))
        XCTAssertNil(TapGesture(zones: [.leftTop, .leftBottom, .rightTop]))
        XCTAssertEqual(TapGesture(zones: [.leftTop])?.tapCount, 1)
        XCTAssertEqual(TapGesture(zones: [.leftTop, .leftBottom])?.tapCount, 2)
    }

    func testGestureIdentityAndDisplayNames() {
        XCTAssertEqual(TapGesture.single(.leftTop).id, "LR")
        XCTAssertEqual(doubleLeftRear.id, "LR-LR")
        XCTAssertEqual(leftRearToRightFront.id, "LR-RF")
        XCTAssertEqual(doubleLeftRear.displayName, "Left Rear twice")
        XCTAssertEqual(leftRearToRightFront.displayName, "Left Rear → Right Front")
        XCTAssertNotEqual(TapGesture.pair(.leftTop, .rightBottom), .pair(.rightBottom, .leftTop))
    }

    func testGestureDecodingRejectsAnUnsupportedLength() throws {
        let encoder = JSONEncoder()
        let valid = try encoder.encode(doubleLeftRear)
        XCTAssertEqual(try JSONDecoder().decode(TapGesture.self, from: valid), doubleLeftRear)

        let tooLong = Data(#"{"zones":[0,1,2]}"#.utf8)
        XCTAssertThrowsError(try JSONDecoder().decode(TapGesture.self, from: tooLong))
        let empty = Data(#"{"zones":[]}"#.utf8)
        XCTAssertThrowsError(try JSONDecoder().decode(TapGesture.self, from: empty))
    }
}
