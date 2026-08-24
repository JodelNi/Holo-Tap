import HoloCore
import SwiftUI

struct DeskMapView: View {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var activeZone: DeskZone?
    var targetZone: DeskZone?
    var confidence: Double
    var signalStrength: Double
    var isListening: Bool
    var counts: [Int]? = nil
    /// How the secondary highlight reads to VoiceOver. Guided capture points at the
    /// next zone to tap; the Desk uses it for a tap held back for a combo.
    var targetDescription = "Current target"

    var body: some View {
        GeometryReader { geometry in
            let width = geometry.size.width
            let height = geometry.size.height
            let horizontalGap = width * 0.035
            let sideWidth = width * 0.29
            let laptopWidth = width - sideWidth * 2 - horizontalGap * 2

            HStack(spacing: horizontalGap) {
                sideRail(isLeft: true)
                    .frame(width: sideWidth, height: height)

                MacBookSilhouette(isListening: isListening)
                    .frame(width: laptopWidth, height: height * 0.70)

                sideRail(isLeft: false)
                    .frame(width: sideWidth, height: height)
            }
            .frame(width: width, height: height)
        }
        .aspectRatio(1.58, contentMode: .fit)
        .animation(reduceMotion ? nil : .snappy(duration: 0.22), value: activeZone)
        .animation(reduceMotion ? nil : .easeInOut(duration: 0.18), value: targetZone)
    }

    private func sideRail(isLeft: Bool) -> some View {
        let zones = DeskZone.allCases.filter { $0.isLeft == isLeft }

        return VStack(spacing: 0) {
            ForEach(zones) { zone in
                zoneRow(zone)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)

                if zone != zones.last {
                    Divider()
                }
            }
        }
        .background(Color.primary.opacity(0.025))
        .clipShape(RoundedRectangle(cornerRadius: 15, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: 15, style: .continuous)
                .strokeBorder(Color.primary.opacity(0.10), lineWidth: 1)
        }
    }

    private func zoneRow(_ zone: DeskZone) -> some View {
        let isActive = zone == activeZone
        let isTarget = zone == targetZone

        return HStack(spacing: 10) {
            if zone.isLeft { Spacer(minLength: 0) }

            if !zone.isLeft {
                zoneIndicator(active: isActive, target: isTarget)
            }

            VStack(alignment: zone.isLeft ? .trailing : .leading, spacing: 3) {
                Text(zone.positionName)
                    .font(.callout.weight(.medium))
                Text(zone.isLeft ? "Left" : "Right")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            if let counts, zone.rawValue < counts.count {
                Text("\(counts[zone.rawValue])")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
            }

            if zone.isLeft {
                zoneIndicator(active: isActive, target: isTarget)
            }

            if !zone.isLeft { Spacer(minLength: 0) }
        }
        .padding(.horizontal, 14)
        .background(zoneFill(active: isActive, target: isTarget))
        .contentShape(Rectangle())
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(zone.displayName)
        .accessibilityValue(accessibilityValue(for: zone, active: isActive, target: isTarget))
        .help(zone.instruction)
    }

    private func zoneIndicator(active: Bool, target: Bool) -> some View {
        Capsule()
            .fill(zoneStroke(active: active, target: target))
            .frame(width: 3, height: active ? 34 : 26)
            .opacity(active || target ? 1 : 0)
    }

    private func zoneFill(active: Bool, target: Bool) -> Color {
        if active { return Color.accentColor.opacity(0.14 + min(signalStrength, 1) * 0.08) }
        if target { return Color.accentColor.opacity(0.07) }
        return .clear
    }

    private func zoneStroke(active: Bool, target: Bool) -> Color {
        if active { return Color.accentColor.opacity(0.85) }
        if target { return Color.accentColor.opacity(0.55) }
        return Color.primary.opacity(0.10)
    }

    private func accessibilityValue(for zone: DeskZone, active: Bool, target: Bool) -> String {
        var parts: [String] = []
        if target { parts.append(targetDescription) }
        if active { parts.append("Last detected, \(Int(confidence * 100)) percent confidence") }
        if let counts, zone.rawValue < counts.count { parts.append("\(counts[zone.rawValue]) captured") }
        return parts.isEmpty ? "Inactive" : parts.joined(separator: ", ")
    }
}

private struct MacBookSilhouette: View {
    var isListening: Bool

    var body: some View {
        GeometryReader { geometry in
            let width = geometry.size.width
            let height = geometry.size.height

            ZStack(alignment: .bottom) {
                RoundedRectangle(cornerRadius: 11, style: .continuous)
                    .fill(Color.primary.opacity(0.035))
                    .overlay(
                        RoundedRectangle(cornerRadius: 11, style: .continuous)
                            .stroke(Color.primary.opacity(0.14), lineWidth: 1)
                    )
                    .frame(width: width * 0.88, height: height * 0.86)
                    .offset(y: -height * 0.055)

                VStack(spacing: 8) {
                    Image(systemName: isListening ? "waveform" : "pause")
                        .font(.title2.weight(.light))
                        .foregroundStyle(isListening ? Color.accentColor : Color.secondary)
                    Text("MacBook")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                .offset(y: -height * 0.29)

                Capsule()
                    .fill(Color.primary.opacity(0.12))
                    .frame(width: width, height: max(height * 0.055, 5))
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(isListening ? "MacBook, microphone active" : "MacBook, microphone paused")
    }
}
