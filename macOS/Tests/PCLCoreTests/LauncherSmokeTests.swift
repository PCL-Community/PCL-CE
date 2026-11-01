import Foundation
import XCTest
@testable import PCLCore

/// Explicit opt-in integration check. Normal test runs never download Minecraft or open a game.
/// PCL_REAL_SMOKE_GAME_DIR selects an isolated game directory; PCL_SMOKE_PREPARE_ONLY=1 only
/// installs and validates the launch plan. The second invocation reuses hash-checked downloads.
final class LauncherSmokeTests: XCTestCase {
    func testOfficialVanillaInstallAndRender() async throws {
        let environment = ProcessInfo.processInfo.environment
        guard let path = environment["PCL_REAL_SMOKE_GAME_DIR"], !path.isEmpty else {
            throw XCTSkip("Set PCL_REAL_SMOKE_GAME_DIR to opt into official download and a real Java/Minecraft launch.")
        }
        let directory = URL(fileURLWithPath: path, isDirectory: true)
        let versionID = environment["PCL_REAL_SMOKE_VERSION"] ?? "1.20.4"
        let installer = MinecraftInstaller()
        let progressOutput = SmokeProgress()
        if environment["PCL_SMOKE_PARTIAL_ASSETS_RENDER"] == "1" {
            print("SMOKE_PARTIAL_ASSETS_RENDER: core/native rendering only; full sound/language asset installation is NOT verified.")
        } else {
            let manifest = try await installer.fetchManifest()
            let version = try XCTUnwrap(manifest.versions.first(where: { $0.id == versionID }))
            try await installer.install(version: version, gameDirectory: directory) { progress in progressOutput.report(progress) }
        }
        let installed = try LocalVersionScanner.scan(gameDirectory: directory)
        let local = try XCTUnwrap(installed.first(where: { $0.id == versionID }))
        let runtimes = JavaDiscovery.discover()
        let runtime = try XCTUnwrap(runtimes.first(where: { $0.majorVersion == local.requiredJava && $0.architecture == InstallPlatform.architecture }))
        let config = LaunchConfiguration(gameDirectory: directory, versionID: versionID, playerName: "PCLSmoke", javaExecutable: runtime.executable, memoryMB: 2048)
        let plan = try MinecraftLauncher.makeLaunchPlan(configuration: config)
        XCTAssertTrue(plan.arguments.contains("net.minecraft.client.main.Main"))
        XCTAssertTrue(plan.arguments.contains("-XstartOnFirstThread"))
        XCTAssertFalse(plan.arguments.contains(where: { $0.contains("${") }))
        print("SMOKE_PREPARED version=\(versionID) java=\(runtime.majorVersion) arch=\(runtime.architecture) directory=\(directory.path)")
        fflush(stdout)
        if environment["PCL_SMOKE_PREPARE_ONLY"] == "1" { return }
        let output = SmokeGameOutput(logFile: directory.appendingPathComponent("pcl-smoke.log"))
        let launcher = MinecraftLauncher()
        defer { launcher.stop() }
        try launcher.launch(configuration: config, onOutput: { line in output.append(line) }, onExit: { status in output.exit(status) })
        let deadline = Date().addingTimeInterval(120)
        while Date() < deadline, !output.rendered, output.exitStatus == nil {
            try await Task.sleep(nanoseconds: 500_000_000)
        }
        XCTAssertTrue(output.rendered, "Minecraft did not initialize its texture atlases/OpenGL renderer. See \(directory.appendingPathComponent("pcl-smoke.log").path)")
        XCTAssertNil(output.exitStatus, "Minecraft exited before rendering; see smoke log")
        if output.rendered {
            try Data("Minecraft \(versionID) initialized the renderer.\n".utf8).write(to: directory.appendingPathComponent("pcl-rendering-ready"), options: .atomic)
            print("SMOKE_RENDERING_READY \(directory.path)")
            fflush(stdout)
            let hold = Double(environment["PCL_SMOKE_HOLD_SECONDS"] ?? "8") ?? 8
            try await Task.sleep(nanoseconds: UInt64(max(0, min(60, hold)) * 1_000_000_000))
        }
        launcher.stop()
        for _ in 0..<20 where output.exitStatus == nil { try await Task.sleep(nanoseconds: 250_000_000) }
        XCTAssertNotNil(output.exitStatus, "Minecraft did not stop after the smoke test")
    }
}

private final class SmokeProgress: @unchecked Sendable {
    private let lock = NSLock()
    private var lastBucket = -1
    func report(_ progress: InstallProgress) {
        lock.lock(); defer { lock.unlock() }
        let bucket = Int(progress.fraction * 10)
        if bucket != lastBucket {
            lastBucket = bucket
            print("SMOKE_INSTALL \(bucket * 10)% \(progress.completed)/\(progress.total)")
            fflush(stdout)
        }
    }
}

private final class SmokeGameOutput: @unchecked Sendable {
    private let lock = NSLock()
    private var lines: [String] = []
    private var didRender = false
    private var status: Int32?
    private let logFile: URL
    init(logFile: URL) { self.logFile = logFile }
    var rendered: Bool { lock.lock(); defer { lock.unlock() }; return didRender }
    var exitStatus: Int32? { lock.lock(); defer { lock.unlock() }; return status }
    func append(_ line: String) {
        lock.lock(); defer { lock.unlock() }
        lines.append(line)
        if line.contains("Created:") && line.contains("textures/atlas/") { didRender = true }
        try? Data(lines.joined(separator: "\n").utf8).write(to: logFile, options: .atomic)
        print("SMOKE_GAME \(line)")
        fflush(stdout)
    }
    func exit(_ code: Int32) {
        lock.lock(); status = code; lock.unlock()
        append("PROCESS_EXIT=\(code)")
    }
}
