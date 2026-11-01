import Foundation
import XCTest
@testable import PCLCore

final class ExtraComponentsSmokeTests: XCTestCase {
    func testDownloadModifyAndLaunchComponents() async throws {
        let env = ProcessInfo.processInfo.environment
        guard let path = env["PCL_COMPONENTS_SMOKE_ROOT"] else { throw XCTSkip("Set isolated PCL_COMPONENTS_SMOKE_ROOT for real component download/modify/launch validation") }
        let root = URL(fileURLWithPath: path), output = root.deletingLastPathComponent()
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        let service = ExtraComponentsInstaller(), vanilla = MinecraftInstaller(), loaders = ModLoaderInstaller()
        let manifest = try await vanilla.fetchManifest()
        let modern = try XCTUnwrap(manifest.versions.first { $0.id == "1.20.1" })
        let old = try XCTUnwrap(manifest.versions.first { $0.id == "1.12.2" })
        let modernEntries = try await service.fetchOptiFineVersions(minecraftVersion: modern.id)
        let ofModern = try XCTUnwrap(modernEntries.first { $0.version == "HD_U_I6" })
        let oldEntries = try await service.fetchOptiFineVersions(minecraftVersion: old.id)
        let ofOld = try XCTUnwrap(oldEntries.first { $0.version == "HD_U_G5" })
        let liteEntries = try await service.fetchLiteLoaderVersions(minecraftVersion: old.id)
        let lite = try XCTUnwrap(liteEntries.first)
        let bridges = try await service.fetchOptiFabricVersions(minecraftVersion: modern.id)
        let bridge = try XCTUnwrap(bridges.first)
        let fabricEntries = try await loaders.fetchVersions(kind: .fabric, minecraftVersion: modern.id)
        let fabric = try XCTUnwrap(fabricEntries.first { $0.version == "0.14.25" })
        let runtimes = JavaDiscovery.discover()
        let java17 = try XCTUnwrap(runtimes.first { $0.majorVersion == 17 && $0.architecture == "arm64" })
        let java8 = try XCTUnwrap(runtimes.first { $0.majorVersion == 8 && $0.architecture == "x86_64" })
        let forgeOnly = env["PCL_COMPONENTS_SMOKE_FORGE_ONLY"] == "1"
        let reportURL = output.appendingPathComponent("component-flow.json")
        var report: [String: Any] = [:]
        // Read accumulated results before the initial save. A Forge-only retry
        // must not overwrite the earlier standalone/Fabric/Lite render results.
        if forgeOnly, let data = try? Data(contentsOf: reportURL),
           let previous = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
           previous["root"] as? String == root.path { report = previous }
        report.merge(["date": "2026-11-01", "stableAfterAtlasSeconds": 30, "root": root.path, "optifine": ofModern.version, "optifabric": bridge.versionNumber, "liteLoader": lite.version, "java8": java8.executable.path], uniquingKeysWith: { _, current in current })
        func save() throws { try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted,.sortedKeys]).write(to: output.appendingPathComponent("component-flow.json"), options: .atomic) }
        try save()
        print("COMPONENT_CATALOGUES_READY optifabric=\(bridges.count) chosen=\(bridge.versionNumber)"); fflush(stdout)
        if env["PCL_COMPONENTS_SMOKE_STAGE"] == "catalogue" { return }
        let relay = ComponentSmokeProgress()
        if !forgeOnly {
        let vanillaOF = "PCL-OptiFine-SMOKE", fabricOF = "PCL-OptiFabric-SMOKE", liteID = "PCL-LiteLoader-SMOKE", liteOnlyID = "PCL-LiteLoaderOnly-SMOKE"
        if !FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/\(vanillaOF)/\(vanillaOF).json").path) {
            try await service.install(minecraft: modern, loader: nil, selection: .init(optiFine: ofModern), fabricAPI: nil,
                gameDirectory: root, profileID: vanillaOF, javaExecutable: java17.executable, progress: { relay.emit($0) })
        }
        report["standaloneOptiFineInstalled"] = true; try save()
        if !FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/\(fabricOF)/\(fabricOF).json").path) {
            // A separate install preserves the standalone rendering case; modification
            // below additionally exercises existing-version commit and old components.
            try await service.install(minecraft: modern, loader: fabric, selection: .init(optiFine: ofModern, optiFabric: bridge), fabricAPI: nil,
                gameDirectory: root, profileID: fabricOF, javaExecutable: java17.executable, progress: { relay.emit($0) })
        }
        report["fabricOptiFineAndBridgeInstalled"] = true; try save()
        if !FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/\(liteID)/\(liteID).json").path) {
            try await service.install(minecraft: old, loader: nil, selection: .init(liteLoader: lite), fabricAPI: nil,
                gameDirectory: root, profileID: liteID, javaExecutable: java8.executable, progress: { relay.emit($0) })
        }
        if !FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/\(liteOnlyID)/\(liteOnlyID).json").path) {
            try await service.install(minecraft: old, loader: nil, selection: .init(liteLoader: lite), fabricAPI: nil,
                gameDirectory: root, profileID: liteOnlyID, javaExecutable: java8.executable, progress: { relay.emit($0) })
        }
        report["liteLoaderInstalled"] = true; try save()
        if env["PCL_COMPONENTS_SMOKE_RENDER"] == "1" {
            for (id, java, expected) in [(vanillaOF, java17, "optifine"), (fabricOF, java17, "optifabric"), (liteOnlyID, java8, "liteloader")] {
                try await render(id, root: root, java: java, expected: expected)
                report[id + "Rendered"] = true; try save()
            }
        }
        let local = try XCTUnwrap(try LocalVersionScanner.scan(gameDirectory: root).first { $0.id == liteID })
        let fingerprint = try VersionComponentManager.fingerprint(version: local, gameDirectory: root)
        let before = try await service.inspect(version: local, gameDirectory: root, isolate: true)
        let api = try FabricAPIComponent.installed(gameDirectory: root, version: local, isolate: true)
        try await service.modify(version: local, minecraft: old, minecraftID: old.id, loader: nil,
            selection: .init(optiFine: ofOld, liteLoader: lite), fabricAPI: nil, keepOriginalFabricAPI: true,
            originalFabricAPI: api, originalExtras: before, newName: liteID, isolate: true,
            expectedFingerprint: fingerprint, gameDirectory: root, javaExecutable: java8.executable, progress: { relay.emit($0) })
        let combined = try MinecraftVersionMetadata.resolve(versionID: liteID, gameDirectory: root)
        let plan = try MinecraftLauncher.makeLaunchPlan(configuration: .init(gameDirectory: root, versionID: liteID, playerName: "PCLSmoke", javaExecutable: java8.executable, memoryMB: 2048, runtimeGameDirectory: local.directory))
        XCTAssertTrue(plan.arguments.contains("com.mumfrey.liteloader.launch.LiteLoaderTweaker"))
        XCTAssertTrue(plan.arguments.contains("optifine.OptiFineForgeTweaker"))
        XCTAssertNotNil(combined["pclOptiFine"]); XCTAssertNotNil(combined["pclLiteLoader"])
        report["existingLiteLoaderModifiedToCombineOptiFine"] = true; try save()
        if env["PCL_COMPONENTS_SMOKE_RENDER"] == "1" {
            try await render(liteID, root: root, java: java8, expected: "liteloader")
            report["combinedLiteLoaderOptiFineRendered"] = true; try save()
        }
        }
        if let required = ofModern.requiredForgeVersion, !required.isEmpty {
            let forgeEntries = try await loaders.fetchVersions(kind: .forge, minecraftVersion: modern.id)
            let forge = try XCTUnwrap(forgeEntries.first { $0.version == required })
            let id = "PCL-Forge-OptiFine-SMOKE"
            if !FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/\(id)/\(id).json").path) {
                try await service.install(minecraft: modern, loader: forge, selection: .init(optiFine: ofModern), fabricAPI: nil,
                    gameDirectory: root, profileID: id, javaExecutable: java17.executable, progress: { relay.emit($0) })
            }
            report["forgeOptiFineInstalled"] = true; try save()
            if env["PCL_COMPONENTS_SMOKE_RENDER"] == "1" { try await render(id, root: root, java: java17, expected: "optifine"); report["forgeOptiFineRendered"] = true; try save() }
        }
        print("COMPONENT_FLOW_READY \(output.appendingPathComponent("component-flow.json").path)"); fflush(stdout)
    }

    private func render(_ id: String, root: URL, java: JavaRuntime, expected: String) async throws {
        let log = root.appendingPathComponent("PCL/render-\(id).log")
        let output = try ComponentSmokeOutput(log: log)
        let launcher = MinecraftLauncher(); defer { launcher.stop() }
        try launcher.launch(configuration: .init(gameDirectory: root, versionID: id, playerName: "PCLSmoke", javaExecutable: java.executable,
            memoryMB: 2048, runtimeGameDirectory: root.appendingPathComponent("versions/\(id)")), onOutput: { output.append($0) }, onExit: { output.exit($0) })
        let deadline = Date().addingTimeInterval(150)
        while Date() < deadline && !output.rendered && output.status == nil { try await Task.sleep(nanoseconds: 500_000_000) }
        // Texture creation alone precedes late OpenGL/LiteLoader initialization.
        // Keep the client alive through the menu instead of terminating at the first atlas.
        if output.rendered {
            let settledUntil = Date().addingTimeInterval(30)
            while Date() < settledUntil && output.status == nil { try await Task.sleep(nanoseconds: 500_000_000) }
        }
        let rendered = output.rendered, status = output.status
        launcher.stop()
        for _ in 0..<30 where output.status == nil { try await Task.sleep(nanoseconds: 200_000_000) }
        XCTAssertTrue(rendered, "\(id) did not render; see \(log.path)")
        XCTAssertNil(status, "\(id) exited early; see \(log.path)")
        XCTAssertTrue(output.text.lowercased().contains(expected), "Expected component \(expected) missing from startup log")
        if !rendered { throw MinecraftInstallError.invalidMetadata("真实游戏未完成渲染：\(log.path)") }
        print("COMPONENT_RENDER_READY \(id) stableAfterAtlas=30s"); fflush(stdout)
    }
}
private final class ComponentSmokeProgress: @unchecked Sendable {
    private let lock = NSLock(); private var last = -1
    func emit(_ value: InstallProgress) { lock.lock(); defer { lock.unlock() }; let bucket = Int(value.fraction * 10); if bucket != last { last = bucket; print("COMPONENT_PROGRESS \(bucket) \(value.message)"); fflush(stdout) } }
}
private final class ComponentSmokeOutput: @unchecked Sendable {
    private let lock = NSLock(); private var lines = "", finished: Int32?, ready = false
    private let file: FileHandle
    init(log: URL) throws { try FileManager.default.createDirectory(at: log.deletingLastPathComponent(), withIntermediateDirectories: true); FileManager.default.createFile(atPath: log.path, contents: nil); file = try FileHandle(forWritingTo: log) }
    deinit { try? file.close() }
    var rendered: Bool { lock.lock(); defer { lock.unlock() }; return ready }
    var status: Int32? { lock.lock(); defer { lock.unlock() }; return finished }
    var text: String { lock.lock(); defer { lock.unlock() }; return lines }
    func append(_ value: String) { lock.lock(); defer { lock.unlock() }; lines += value + "\n"; if value.contains("Created:") && (value.contains("atlas") || value.contains("textures")) { ready = true }; try? file.write(contentsOf: Data((value + "\n").utf8)) }
    func exit(_ value: Int32) { lock.lock(); finished = value; lock.unlock() }
}
