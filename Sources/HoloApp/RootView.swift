import HoloCore
import SwiftUI

struct RootView: View {
    @ObservedObject var model: AppModel

    var body: some View {
        NavigationSplitView {
            VStack(spacing: 0) {
                List(selection: $model.section) {
                    Section {
                        ForEach(AppSection.primary) { navigationRow($0) }
                    }

                    Section("Advanced") {
                        ForEach(AppSection.advanced) { navigationRow($0) }
                    }
                }
                .listStyle(.sidebar)

                Divider()

                microphoneFooter
                    .padding(12)
            }
            .navigationTitle("Holo")
            .navigationSplitViewColumnWidth(min: 176, ideal: 196, max: 230)
        } detail: {
            content
                .navigationTitle(model.section.title)
                .toolbar { toolbar }
                .safeAreaInset(edge: .bottom) {
                    statusBar
                }
        }
        .navigationSplitViewStyle(.balanced)
        .alert("Holo", isPresented: Binding(
            get: { model.errorMessage != nil },
            set: { if !$0 { model.errorMessage = nil } }
        )) {
            Button("OK", role: .cancel) { model.errorMessage = nil }
        } message: {
            Text(model.errorMessage ?? "Unknown error")
        }
    }

    private func navigationRow(_ section: AppSection) -> some View {
        Label(section.title, systemImage: section.symbol)
            .tag(section)
            .disabled(!model.canNavigate(to: section))
    }

    @ViewBuilder
    private var content: some View {
        switch model.section {
        case .live:
            LiveSurfaceView(model: model)
        case .calibrate:
            CalibrationView(model: model)
        case .diagnostics:
            DiagnosticsView(model: model)
        case .evaluate:
            EvaluationView(model: model)
        case .actions:
            ZoneActionsView(model: model)
        }
    }

    @ToolbarContentBuilder
    private var toolbar: some ToolbarContent {
        ToolbarItemGroup(placement: .primaryAction) {
            if !model.profiles.isEmpty {
                Menu {
                    ForEach(model.profiles) { profile in
                        Button {
                            model.selectProfile(profile.id)
                        } label: {
                            if profile.id == model.selectedProfileID {
                                Label(profile.name, systemImage: "checkmark")
                            } else {
                                Text(profile.name)
                            }
                        }
                    }
                } label: {
                    Label(model.selectedProfile?.name ?? "Profile", systemImage: "macbook")
                }
                .help("Choose a desk profile")
                .disabled(model.guidedSection != nil)
            }

            if model.selectedProfile == nil && model.guidedSection == nil {
                Button {
                    model.openSetup()
                } label: {
                    Label("Set Up Desk", systemImage: "scope")
                }
                .help("Calibrate the four zones before listening")
            } else {
                Button {
                    model.togglePause()
                } label: {
                    Label(
                        model.audio.isListening ? "Pause" : "Resume",
                        systemImage: model.audio.isListening ? "pause.fill" : "play.fill"
                    )
                }
                .help(model.audio.isListening ? "Pause microphone immediately" : "Resume microphone")
            }
        }
    }

    private var microphoneFooter: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 8) {
                Image(systemName: microphoneSymbol)
                    .foregroundStyle(model.audio.isListening ? .red : .secondary)
                VStack(alignment: .leading, spacing: 1) {
                    Text(microphoneTitle)
                        .font(.caption.weight(.medium))
                    Text(microphoneDetail)
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }
                Spacer(minLength: 0)
            }
            ProgressView(value: model.audio.liveLevel)
                .progressViewStyle(.linear)
                .tint(model.audio.isListening ? .blue : .gray)
        }
    }

    private var needsInitialSetup: Bool {
        model.selectedProfile == nil && model.calibrationSession == nil
    }

    private var microphoneSymbol: String {
        if needsInitialSetup { return "scope" }
        return model.audio.isListening ? "mic.fill" : "mic.slash"
    }

    private var microphoneTitle: String {
        if needsInitialSetup { return "Setup required" }
        return model.audio.isListening ? "Microphone active" : "Microphone off"
    }

    private var microphoneDetail: String {
        if needsInitialSetup { return "Calibrate four zones first" }
        return model.audio.isListening ? "Processed on this Mac" : "No audio capture"
    }

    private var statusBar: some View {
        HStack(spacing: 8) {
            Circle()
                .fill(model.audio.isListening ? Color.green : Color.secondary)
                .frame(width: 6, height: 6)
            Text(model.statusMessage)
                .font(.caption)
                .foregroundStyle(.secondary)
                .lineLimit(1)
            Spacer()
            if model.audio.isListening && model.audio.strategy != .passive {
                Label("Speaker probe active", systemImage: "speaker.wave.1")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            if model.debugRecordingEnabled {
                Label("Debug recording on", systemImage: "record.circle")
                    .font(.caption)
                    .foregroundStyle(.red)
            } else if model.hasDebugRecordings {
                Label("Saved debug audio", systemImage: "externaldrive")
                    .font(.caption)
                    .foregroundStyle(.orange)
            } else {
                Label("Audio discarded", systemImage: "lock")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .padding(.horizontal, 14)
        .frame(height: 32)
        .background(.bar)
    }
}
