import Foundation

/// Settings that belong to the person rather than to a desk.
///
/// A desk profile stores what Holo learned about one surface: its calibration, its four
/// actions, its combos. How fast someone taps twice is not a property of the desk, so it
/// lives here instead and follows the user across every profile.
public struct HoloPreferences: Codable, Equatable, Sendable {
    /// How long a tap that begins a combo is held while Holo waits for its partner.
    /// One value for every combo — combos are not individually timed.
    public var comboWindowSeconds: Double

    public init(comboWindowSeconds: Double = TapSequenceRecognizer.defaultWindowSeconds) {
        self.comboWindowSeconds = TapSequenceRecognizer.clampedWindowSeconds(comboWindowSeconds)
    }

    private enum CodingKeys: String, CodingKey {
        case comboWindowSeconds
    }

    /// Decoded defensively. A stored window is clamped rather than trusted, so a file
    /// edited by hand or written by a future build cannot leave combos unreachable, and
    /// an absent key simply means this build added a setting the file predates.
    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let stored = try container.decodeIfPresent(Double.self, forKey: .comboWindowSeconds)
        comboWindowSeconds = TapSequenceRecognizer.clampedWindowSeconds(
            stored ?? TapSequenceRecognizer.defaultWindowSeconds
        )
    }
}

public final class PreferencesStore {
    public let fileURL: URL

    public init(fileURL: URL? = nil, fileManager: FileManager = .default) throws {
        if let fileURL {
            self.fileURL = fileURL
        } else {
            let support = try fileManager.url(
                for: .applicationSupportDirectory,
                in: .userDomainMask,
                appropriateFor: nil,
                create: true
            )
            let directory = support.appendingPathComponent("Holo", isDirectory: true)
            try fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
            self.fileURL = directory.appendingPathComponent("preferences.json")
        }
    }

    public func save(_ preferences: HoloPreferences) throws {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try encoder.encode(preferences).write(to: fileURL, options: .atomic)
    }

    /// No file means nothing has been changed yet, which is a default rather than a
    /// failure. A file that cannot be parsed is still an error worth surfacing.
    public func load() throws -> HoloPreferences {
        guard FileManager.default.fileExists(atPath: fileURL.path) else { return HoloPreferences() }
        return try JSONDecoder().decode(HoloPreferences.self, from: Data(contentsOf: fileURL))
    }
}
