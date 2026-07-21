// swift-tools-version: 5.9
import PackageDescription

var package = Package(
    name: "Holo",
    platforms: [.macOS(.v14)],
    products: [
        .library(name: "HoloCore", targets: ["HoloCore"]),
        .executable(name: "HoloSoak", targets: ["HoloSoak"])
    ],
    dependencies: [],
    targets: [
        .target(name: "HoloCore", path: "Sources/HoloCore"),
        .executableTarget(
            name: "HoloSoak",
            dependencies: ["HoloCore"],
            path: "Sources/HoloSoak"
        ),
        .testTarget(
            name: "HoloCoreTests",
            dependencies: ["HoloCore"],
            path: "Tests/HoloCoreTests"
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
