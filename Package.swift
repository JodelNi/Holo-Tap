// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "Holo",
    platforms: [.macOS(.v14)],
    products: [
        .library(name: "HoloCore", targets: ["HoloCore"]),
        .executable(name: "HoloSoak", targets: ["HoloSoak"]),
        .executable(name: "HoloDaemon", targets: ["HoloDaemon"])
    ],
    targets: [
        .systemLibrary(
            name: "CPulseAudio",
            pkgConfig: "libpulse-simple",
            providers: [
                .apt(["libpulse-dev"])
            ]
        ),
        .target(name: "HoloCore", path: "Sources/HoloCore"),
        .executableTarget(
            name: "HoloSoak",
            dependencies: ["HoloCore"],
            path: "Sources/HoloSoak"
        ),
        .executableTarget(
            name: "HoloDaemon",
            dependencies: ["HoloCore", "CPulseAudio"],
            path: "Sources/HoloDaemon"
        ),
        .testTarget(
            name: "HoloCoreTests",
            dependencies: ["HoloCore"],
            path: "Tests/HoloCoreTests"
        )
    ]
)

