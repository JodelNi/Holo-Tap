import Foundation
import HoloCore

public struct LinuxActionDispatcher {
    private let isWayland: Bool
    
    public init() {
        self.isWayland = ProcessInfo.processInfo.environment["WAYLAND_DISPLAY"] != nil
    }
    
    public func dispatch(_ command: LocalActionCommand) {
        switch command {
        case .playSound(let _):
            // Simple beep or system sound
            run("paplay /usr/share/sounds/freedesktop/stereo/message.oga || aplay /usr/share/sounds/alsa/Front_Center.wav")
            
        case .copyText(let text):
            let safeText = text.replacingOccurrences(of: "'", with: "'\\''")
            if isWayland {
                run("echo '\(safeText)' | wl-copy")
            } else {
                run("echo '\(safeText)' | xclip -selection clipboard")
            }
            
        case .speakText(let text):
            let safeText = text.replacingOccurrences(of: "'", with: "'\\''")
            run("spd-say '\(safeText)' || espeak '\(safeText)'")
            
        case .openURL(let url):
            run("xdg-open '\(url.absoluteString)'")
            
        case .runShellCommand(let cmd):
            run(cmd)
            
        case .takeScreenshot(let interactive):
            if isWayland {
                if interactive {
                    run("grim -g \"$(slurp)\" - | wl-copy")
                } else {
                    run("grim - | wl-copy")
                }
            } else {
                if interactive {
                    run("scrot -s -e 'xclip -selection clipboard -t image/png -i $f && rm $f'")
                } else {
                    run("scrot -e 'xclip -selection clipboard -t image/png -i $f && rm $f'")
                }
            }
            
        case .runShortcut, .openApplication, .openItem:
            print("Action not supported on Linux natively: \(command)")
        }
    }
    
    private func run(_ shellCommand: String) {
        print("Executing: \(shellCommand)")
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/sh")
        process.arguments = ["-c", shellCommand]
        try? process.run()
    }
}
