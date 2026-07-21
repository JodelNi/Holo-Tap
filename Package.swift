// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "Holo",
    platforms: [.macOS(.v13)],
    products: [
        .library(name: "HoloCore", targets: ["HoloCore"]),
        .executable(name: "HoloSoak", targets: ["HoloSoak"])
    ],
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
