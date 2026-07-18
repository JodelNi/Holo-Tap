import SwiftUI

struct HoloUnavailableView<LabelContent: View, Description: View, Actions: View>: View {
    let label: LabelContent
    let description: Description
    let actions: Actions

    init(
        @ViewBuilder label: () -> LabelContent,
        @ViewBuilder description: () -> Description,
        @ViewBuilder actions: () -> Actions
    ) {
        self.label = label()
        self.description = description()
        self.actions = actions()
    }

    var body: some View {
        // This local replacement is to cater for older Macs where
        // ContentUnavailableView is not available.
        VStack(spacing: 14) {
            label
                .font(.title2.weight(.semibold))
            description
                .font(.callout)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
            actions
                .padding(.top, 4)
        }
        .frame(maxWidth: 420)
        .padding(36)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

extension View {
    func onCompatibleChange<Value: Equatable>(
        of value: Value,
        perform action: @escaping (_ oldValue: Value, _ newValue: Value) -> Void
    ) -> some View {
        modifier(CompatibleChangeModifier(value: value, action: action))
    }
}

private struct CompatibleChangeModifier<Value: Equatable>: ViewModifier {
    let value: Value
    let action: (_ oldValue: Value, _ newValue: Value) -> Void
    @State private var previousValue: Value?

    func body(content: Content) -> some View {
        // This compatibility modifier is to cater for older Macs while keeping
        // old/new value semantics used by the macOS 14 onChange API.
        content.onChange(of: value) { newValue in
            let oldValue = previousValue ?? value
            previousValue = newValue
            action(oldValue, newValue)
        }
        .onAppear {
            previousValue = value
        }
    }
}
