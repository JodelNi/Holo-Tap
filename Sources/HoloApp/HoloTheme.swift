import AppKit
import SwiftUI

enum HoloTheme {
    static let background = Color(nsColor: .windowBackgroundColor)
}

extension View {
    @ViewBuilder
    func holoPrimaryButton() -> some View {
        // The bordered fallback is to cater for older Macs and older Xcode
        // toolchains that do not know the macOS 26 Liquid Glass style.
        #if compiler(>=6.0)
        if #available(macOS 26.0, *) {
            self.buttonStyle(.glassProminent)
        } else {
            self.buttonStyle(.borderedProminent)
        }
        #else
        self.buttonStyle(.borderedProminent)
        #endif
    }

    @ViewBuilder
    func holoSecondaryButton() -> some View {
        self.buttonStyle(.bordered)
    }
}
