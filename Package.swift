// swift-tools-version: 5.9
import PackageDescription

var package = Package(
    name: "Holo",
    platforms: [.macOS(.v14)],
    products: [
        .library(name: "HoloCore", targets: ["HoloCore"]),
        .executable(name: "HoloSoak", targets: ["HoloSoak"]),
        .executable(name: "HoloReplay", targets: ["HoloReplay"])
    ],
    dependencies: [],
    targets: [
        .target(name: "HoloCore", path: "Sources/HoloCore"),
        .executableTarget(
            name: "HoloSoak",
            dependencies: ["HoloCore"],
            path: "Sources/HoloSoak"
        ),
        .target(
            name: "HoloReplaySupport",
            dependencies: ["HoloCore"],
            path: "Sources/HoloReplaySupport"
        ),
        .executableTarget(
            name: "HoloReplay",
            dependencies: ["HoloCore", "HoloReplaySupport"],
            path: "Sources/HoloReplay"
        ),
        .testTarget(
            name: "HoloCoreTests",
            dependencies: ["HoloCore"],
            path: "Tests/HoloCoreTests"
        ),
        .testTarget(
            name: "HoloReplayTests",
            dependencies: ["HoloReplaySupport", "HoloCore"],
            path: "Tests/HoloReplayTests"
        )
    ]
)

#if os(Linux)
package.dependencies.append(
    .package(url: "https://github.com/rhx/SwiftGtk.git", branch: "main")
)

package.products.append(contentsOf: [
    .executable(name: "HoloDaemon", targets: ["HoloDaemon"]),
    .executable(name: "HoloUI", targets: ["HoloUI"])
])

package.targets.append(contentsOf: [
    .systemLibrary(
        name: "CPulseAudio",
        pkgConfig: "libpulse-simple",
        providers: [
            .apt(["libpulse-dev"])
        ]
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
    )
])
#endif
