import Foundation

/// One or two taps, in order.
///
/// A single tap is the original behaviour: one zone, one action. Two taps form a
/// combo, either the same zone twice or two different zones, which lets four zones
/// address more than four commands without adding a fifth zone.
public struct TapGesture: Codable, Hashable, Sendable, Identifiable {
    /// Combos are deliberately limited to two taps. Every additional tap multiplies
    /// the chance that one misclassification loses the whole gesture, and lengthens
    /// the wait before a plain single tap can be dispatched.
    public static let maximumTapCount = 2

    public private(set) var zones: [DeskZone]

    public init?(zones: [DeskZone]) {
        guard (1...Self.maximumTapCount).contains(zones.count) else { return nil }
        self.zones = zones
    }

    private init(unchecked zones: [DeskZone]) {
        self.zones = zones
    }

    public static func single(_ zone: DeskZone) -> TapGesture {
        TapGesture(unchecked: [zone])
    }

    public static func pair(_ first: DeskZone, _ second: DeskZone) -> TapGesture {
        TapGesture(unchecked: [first, second])
    }

    public var id: String { zones.map(\.shortName).joined(separator: "-") }

    public var tapCount: Int { zones.count }

    public var isSingle: Bool { zones.count == 1 }

    public var firstZone: DeskZone { zones[0] }

    public var displayName: String {
        guard zones.count > 1 else { return zones[0].displayName }
        if zones[0] == zones[1] { return "\(zones[0].displayName) twice" }
        return zones.map(\.displayName).joined(separator: " → ")
    }

    private enum CodingKeys: String, CodingKey {
        case zones
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let decoded = try container.decode([DeskZone].self, forKey: .zones)
        guard (1...Self.maximumTapCount).contains(decoded.count) else {
            throw DecodingError.dataCorruptedError(
                forKey: .zones,
                in: container,
                debugDescription: "A tap gesture holds 1 to \(Self.maximumTapCount) zones."
            )
        }
        zones = decoded
    }
}

/// A user-added combo and the action it runs. Combos reuse `ZoneActionConfiguration`
/// so every action kind, and the whole of `LocalActionPlanner`, applies unchanged.
public struct ComboBinding: Codable, Equatable, Sendable, Identifiable {
    public var gesture: TapGesture
    public var action: ZoneActionConfiguration

    public var id: String { gesture.id }

    public init(gesture: TapGesture, action: ZoneActionConfiguration = ZoneActionConfiguration()) {
        self.gesture = gesture
        self.action = action
    }
}
