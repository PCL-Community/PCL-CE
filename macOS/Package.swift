// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "PCLMac",
    platforms: [.macOS(.v14)],
    products: [
        .library(name: "PCLCore", targets: ["PCLCore"]),
        .executable(name: "PCLMac", targets: ["PCLMac"])
    ],
    targets: [
        .target(name: "PCLCore"),
        .executableTarget(name: "PCLMac", dependencies: ["PCLCore"]),
        .testTarget(name: "PCLCoreTests", dependencies: ["PCLCore"])
    ],
    swiftLanguageVersions: [.v5]
)
