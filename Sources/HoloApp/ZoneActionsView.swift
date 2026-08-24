import AppKit
import HoloCore
import SwiftUI
import UniformTypeIdentifiers

struct ZoneActionsView: View {
    @ObservedObject var model: AppModel
    @State private var confirmDelete = false
    @State private var isAddingCombo = false
    /// Mirrors the model while the slider is being dragged. The setting is written once
    /// on release rather than on every frame of the drag.
    @State private var windowDraft = TapSequenceRecognizer.defaultWindowSeconds

    var body: some View {
        Group {
            if let profile = model.selectedProfile {
                VStack(alignment: .leading, spacing: 18) {
                    VStack(alignment: .leading, spacing: 5) {
                        Text("Assign actions")
                            .font(.title.weight(.semibold))
                        Text("Each accepted tap runs its assigned action. Combos add two-tap gestures. Changes save automatically.")
                            .font(.callout)
                            .foregroundStyle(.secondary)
                    }

                    Form {
                        Section("Left of MacBook") {
                            ForEach(sortedConfigurations(profile, isLeft: true)) { configuration in
                                zoneRow(profile: profile, configuration: configuration)
                            }
                        }

                        Section("Right of MacBook") {
                            ForEach(sortedConfigurations(profile, isLeft: false)) { configuration in
                                zoneRow(profile: profile, configuration: configuration)
                            }
                        }

                        comboSection(profile)

                        Section("Automation") {
                            Label("Use Run Shortcut for multi-step workflows such as opening Claude and starting a voice workflow.", systemImage: "command")
                            Label("Shell commands run through /bin/zsh with Holo's current macOS permissions.", systemImage: "terminal")
                            Label("Screenshot actions copy the result and may request Screen Recording access.", systemImage: "camera.viewfinder")
                        }
                        .font(.caption)
                        .foregroundStyle(.secondary)

                        Section {
                            HStack {
                                Text("Profile: \(profile.name)")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                                Spacer()
                                Button("Delete Profile", role: .destructive) {
                                    confirmDelete = true
                                }
                            }
                        }
                    }

                    .formStyle(.grouped)
                }
                .frame(maxWidth: 780, alignment: .leading)
                .padding(36)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ContentUnavailableView {
                    Label("No Desk Profile", systemImage: "macbook")
                } description: {
                    Text("Calibrate the four zones before assigning actions.")
                } actions: {
                    Button("Open Calibration") { model.section = .calibrate }
                        .holoPrimaryButton()
                }
            }
        }
        .background(HoloTheme.background)
        .confirmationDialog("Delete this desk profile?", isPresented: $confirmDelete) {
            Button("Delete Profile", role: .destructive) { model.deleteSelectedProfile() }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("The saved calibration and its four actions will be removed.")
        }
        .onAppear { windowDraft = model.comboWindowSeconds }
        .onChange(of: model.comboWindowSeconds) { _, newValue in
            windowDraft = newValue
        }
        .sheet(isPresented: $isAddingCombo) {
            AddComboSheet(existingIDs: Set(model.selectedProfile?.combos.map(\.id) ?? [])) { gesture in
                model.addCombo(gesture)
            }
        }
    }

    private func zoneRow(profile: HoloProfile, configuration: ZoneConfiguration) -> some View {
        let side = configuration.zone.isLeft ? "left" : "right"
        // A zone that begins a combo pays for it on every single tap, so the cost is
        // stated on the row that carries it rather than only in the Combos help text.
        let beginsACombo = profile.comboPrefixZones.contains(configuration.zone)
        return ZoneActionRow(
            title: configuration.zone.positionName,
            subtitle: configuration.zone.isLeft ? "Left side" : "Right side",
            accessibilityName: "\(configuration.zone.positionName), \(side) side",
            note: beginsACombo ? "Waits up to \(Self.secondsLabel(model.comboWindowSeconds))" : nil,
            labelWidth: 112,
            action: configuration.action,
            onChange: { action in
                model.updateAction(for: configuration.zone, action: action)
            },
            onTest: model.testAction,
            onError: { error in
                model.errorMessage = "The action could not be assigned. \(error.localizedDescription)"
            }
        )
        .id("\(profile.id)-\(configuration.zone.rawValue)")
    }

    @ViewBuilder
    private func comboSection(_ profile: HoloProfile) -> some View {
        Section("Combos") {
            ForEach(profile.combos) { binding in
                HStack(spacing: 8) {
                    ZoneActionRow(
                        title: binding.gesture.displayName,
                        subtitle: "Two taps",
                        accessibilityName: binding.gesture.displayName,
                        labelWidth: 168,
                        action: binding.action,
                        onChange: { action in
                            model.updateCombo(binding.gesture, action: action)
                        },
                        onTest: model.testAction,
                        onError: { error in
                            model.errorMessage = "The action could not be assigned. \(error.localizedDescription)"
                        }
                    )
                    .id("\(profile.id)-combo-\(binding.id)")

                    Button("Remove", systemImage: "minus.circle") {
                        model.removeCombo(binding.gesture)
                    }
                    .labelStyle(.iconOnly)
                    .buttonStyle(.borderless)
                    .accessibilityLabel("Remove combo \(binding.gesture.displayName)")
                }
            }

            // The timing control appears once there is a combo for it to govern. With
            // none bound it would be a setting for nothing.
            if !profile.combos.isEmpty {
                LabeledContent {
                    HStack(spacing: 12) {
                        Slider(
                            value: $windowDraft,
                            in: TapSequenceRecognizer.windowRangeSeconds,
                            step: 0.05
                        ) { isDragging in
                            // Committing on release keeps one drag to a single write, and
                            // keeps a rejected write from fighting the user mid-gesture.
                            // Snapping back afterwards covers a clamped or refused value
                            // alike, so the readout never disagrees with what is in force.
                            guard !isDragging else { return }
                            model.setComboWindowSeconds(windowDraft)
                            windowDraft = model.comboWindowSeconds
                        }
                        .frame(maxWidth: 260)
                        .accessibilityLabel("Time allowed between the two taps of a combo")
                        Text(Self.secondsLabel(windowDraft))
                            .font(.callout.monospacedDigit())
                            .frame(width: 54, alignment: .leading)
                    }
                } label: {
                    VStack(alignment: .leading, spacing: 1) {
                        Text("Time between taps")
                            .font(.body.weight(.medium))
                        Text("All combos")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    .frame(width: 168, alignment: .leading)
                }
                .padding(.vertical, 4)
            }

            HStack(alignment: .firstTextBaseline, spacing: 12) {
                Button("Add Combo", systemImage: "plus") { isAddingCombo = true }
                    .holoSecondaryButton()
                Text("Tap, pause, then tap again within \(Self.secondsLabel(model.comboWindowSeconds)). A trackpad-speed double tap is too quick for Holo to hear as two taps, which is why the shortest setting is \(Self.secondsLabel(TapSequenceRecognizer.windowRangeSeconds.lowerBound)).")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    static func secondsLabel(_ seconds: Double) -> String {
        String(format: "%.2f s", seconds)
    }

    private func sortedConfigurations(_ profile: HoloProfile, isLeft: Bool) -> [ZoneConfiguration] {
        profile.zones
            .filter { $0.zone.isLeft == isLeft }
            .sorted { $0.zone.verticalIndex < $1.zone.verticalIndex }
    }
}

private struct AddComboSheet: View {
    let existingIDs: Set<String>
    let onAdd: (TapGesture) -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var firstZone: DeskZone = .leftTop
    @State private var secondZone: DeskZone = .leftBottom

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 5) {
                Text("Add a combo")
                    .font(.title3.weight(.semibold))
                Text("Two taps in order. The same zone twice is the most reliable combo, because both taps come from one place on the desk.")
                    .font(.callout)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }

            Form {
                Picker("First tap", selection: $firstZone) {
                    ForEach(DeskZone.allCases) { zone in
                        Text(zone.displayName).tag(zone)
                    }
                }
                Picker("Second tap", selection: $secondZone) {
                    ForEach(DeskZone.allCases) { zone in
                        Text(zone.displayName).tag(zone)
                    }
                }
                LabeledContent("Gesture") {
                    Text(gesture.displayName)
                }
            }
            .formStyle(.grouped)

            if isDuplicate {
                Label("This combo is already assigned.", systemImage: "exclamationmark.triangle")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            HStack {
                Spacer()
                Button("Cancel", role: .cancel) { dismiss() }
                    .holoSecondaryButton()
                Button("Add Combo") {
                    onAdd(gesture)
                    dismiss()
                }
                .holoPrimaryButton()
                .disabled(isDuplicate)
            }
        }
        .padding(24)
        .frame(width: 460)
    }

    private var gesture: TapGesture {
        .pair(firstZone, secondZone)
    }

    private var isDuplicate: Bool {
        existingIDs.contains(gesture.id)
    }
}

/// The editor for one binding. Shared by the four single-tap zones and by combos so
/// that both get the same progressive disclosure, Test button, and failed-save revert.
private struct ZoneActionRow: View {
    let title: String
    let subtitle: String
    let accessibilityName: String
    /// An extra fact about this row, such as the delay a bound combo imposes on the
    /// zone that begins it. Omitted entirely when there is nothing to say.
    let note: String?
    let labelWidth: CGFloat
    let onChange: (ZoneActionConfiguration) -> Bool
    let onTest: (ZoneActionConfiguration) -> Void
    let onError: (Error) -> Void
    @State private var action: ZoneActionConfiguration
    @State private var isRevertingFailedSave = false

    private let sounds = ["Tink", "Pop", "Ping", "Glass", "Funk", "Morse", "Purr", "Sosumi"]

    init(
        title: String,
        subtitle: String,
        accessibilityName: String,
        note: String? = nil,
        labelWidth: CGFloat = 86,
        action: ZoneActionConfiguration,
        onChange: @escaping (ZoneActionConfiguration) -> Bool,
        onTest: @escaping (ZoneActionConfiguration) -> Void,
        onError: @escaping (Error) -> Void
    ) {
        self.title = title
        self.subtitle = subtitle
        self.accessibilityName = accessibilityName
        self.note = note
        self.labelWidth = labelWidth
        self.onChange = onChange
        self.onTest = onTest
        self.onError = onError
        _action = State(initialValue: action)
    }

    var body: some View {
        LabeledContent {
            HStack(spacing: 12) {
                Picker("Action", selection: $action.kind) {
                    ForEach(ZoneActionKind.allCases) { kind in
                        Text(kind.displayName).tag(kind)
                    }
                }
                .labelsHidden()
                .frame(width: 190)
                .accessibilityLabel("Action for \(accessibilityName)")

                detailControl
                    .frame(maxWidth: .infinity, alignment: .leading)

                Button("Test", systemImage: "play.fill") {
                    onTest(action)
                }
                .labelStyle(.iconOnly)
                .buttonStyle(.borderless)
                .disabled(!canTest)
                .help(canTest ? "Test this action" : "Finish configuring this action before testing")
                .accessibilityLabel("Test action for \(accessibilityName)")
            }
        } label: {
            VStack(alignment: .leading, spacing: 1) {
                Text(title)
                    .font(.body.weight(.medium))
                Text(subtitle)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                if let note {
                    Text(note)
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            .frame(width: labelWidth, alignment: .leading)
        }
        .padding(.vertical, 4)
        .onChange(of: action) { oldValue, newValue in
            if isRevertingFailedSave {
                isRevertingFailedSave = false
                return
            }
            if !onChange(newValue) {
                isRevertingFailedSave = true
                action = oldValue
            }
        }
    }

    private var canTest: Bool {
        LocalActionPlanner.command(for: action) != nil
    }

    @ViewBuilder
    private var detailControl: some View {
        switch action.kind {
        case .none:
            Text("Highlight only")
                .foregroundStyle(.secondary)
        case .sound:
            Picker("Sound", selection: $action.soundName) {
                ForEach(sounds, id: \.self) { Text($0).tag($0) }
            }
            .labelsHidden()
            .frame(maxWidth: 180)
            .accessibilityLabel("Sound to play")
        case .copyText:
            TextField("Text to copy", text: $action.text)
        case .speakText:
            TextField("Words to speak", text: $action.text)
        case .openURL:
            TextField("Website address", text: $action.text, prompt: Text("https://example.com"))
        case .runShortcut:
            TextField("Shortcut name", text: $action.text, prompt: Text("Start Claude listening"))
        case .openApplication:
            Button {
                chooseApplication()
            } label: {
                Label(action.text.isEmpty ? "Choose Application…" : action.text, systemImage: "app")
                    .lineLimit(1)
            }
            .holoSecondaryButton()
        case .openItem:
            Button {
                chooseItem()
            } label: {
                Label(action.text.isEmpty ? "Choose File or Folder…" : action.text, systemImage: "doc")
                    .lineLimit(1)
            }
            .holoSecondaryButton()
        case .runShellCommand:
            VStack(alignment: .leading, spacing: 3) {
                TextField("Shell command", text: $action.text, prompt: Text("open -a Claude"))
                    .font(.system(.body, design: .monospaced))
                Text("Runs automatically after an accepted tap")
                    .font(.caption2)
                    .foregroundStyle(.orange)
            }
        case .screenshotClipboard:
            Label("All displays → Clipboard", systemImage: "rectangle.on.rectangle")
                .foregroundStyle(.secondary)
        case .screenshotSelection:
            Label("Choose an area → Clipboard", systemImage: "viewfinder")
                .foregroundStyle(.secondary)
        }
    }

    private func chooseApplication() {
        let panel = NSOpenPanel()
        panel.title = "Choose an application"
        panel.prompt = "Assign"
        panel.allowedContentTypes = [.applicationBundle]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.directoryURL = URL(fileURLWithPath: "/Applications", isDirectory: true)
        guard panel.runModal() == .OK, let url = panel.url else { return }
        saveBookmark(for: url, displayName: url.deletingPathExtension().lastPathComponent)
    }

    private func chooseItem() {
        let panel = NSOpenPanel()
        panel.title = "Choose a file or folder"
        panel.prompt = "Assign"
        panel.allowsMultipleSelection = false
        panel.canChooseFiles = true
        panel.canChooseDirectories = true
        guard panel.runModal() == .OK, let url = panel.url else { return }
        saveBookmark(for: url, displayName: url.lastPathComponent)
    }

    private func saveBookmark(for url: URL, displayName: String) {
        do {
            action.bookmarkData = try url.bookmarkData(
                options: .withSecurityScope,
                includingResourceValuesForKeys: nil,
                relativeTo: nil
            )
            action.text = displayName
        } catch {
            action.bookmarkData = nil
            action.text = ""
            onError(error)
        }
    }
}
