// swift-tools-version:5.9
import PackageDescription

// OctoWatch (macOS). The UI is native SwiftUI; all GitHub logic lives in the shared
// Rust core (../core), reached through the UniFFI-generated Swift bindings in
// Sources/OctoWatch/Generated. Build the core static library first — see README.md.
let package = Package(
    name: "OctoWatch",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "OctoWatch", targets: ["OctoWatch"])
    ],
    targets: [
        // C header + module map for the generated bindings (import octowatch_coreFFI).
        .target(name: "octowatch_coreFFI"),

        .executableTarget(
            name: "OctoWatch",
            dependencies: ["octowatch_coreFFI"],
            linkerSettings: [
                // Universal liboctowatch_core.a, produced by scripts/build-core-macos.sh
                // into macos/lib/. Run `swift build` from the macos/ directory.
                .unsafeFlags(["-Llib", "-loctowatch_core"]),
                .linkedFramework("Security"),
                .linkedFramework("CoreFoundation"),
                .linkedFramework("SystemConfiguration"),
            ]
        ),
    ]
)
