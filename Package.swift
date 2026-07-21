// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "Holo",
    platforms: [.macOS(.v14)],
    products: [
        .library(name: "HoloCore", targets: ["HoloCore"]),
        .executable(name: "HoloSoak", targets: ["HoloSoak"]),
        .executable(name: "HoloDaemon", targets: ["HoloDaemon"]),
        .executable(name: "HoloUI", targets: ["HoloUI"])
    ],
    dependencies: [
        .package(url: "https://github.com/rhx/SwiftGtk.git", branch: "main")
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
        .executableTarget(
            name: "HoloUI",
            dependencies: [
                "HoloCore",
                "CPulseAudio",
                .product(name: "Gtk", package: "SwiftGtk")
            ],
            path: "Sources/HoloUI"
        ),
        .testTarget(
            name: "HoloCoreTests",
            dependencies: ["HoloCore"],
            path: "Tests/HoloCoreTests"
        )
    ]
)

