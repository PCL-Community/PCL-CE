import Foundation
import XCTest
@testable import PCLCore

/// Opt-in network verification, confined to a supplied test directory. This
/// downloads actual resources but never launches Minecraft or changes defaults.
final class CommunityResourceSmokeTests: XCTestCase {
    func testCommunityResourcesAndOnlineModpackInIsolatedGame() async throws {
        let env = ProcessInfo.processInfo.environment
        guard let path = env["PCL_COMMUNITY_SMOKE_GAME_DIR"], !path.isEmpty else {
            throw XCTSkip("Set PCL_COMMUNITY_SMOKE_GAME_DIR to opt into real community resource downloads in an isolated test game directory.")
        }
        let root = URL(fileURLWithPath: path, isDirectory: true)
        let fm = FileManager.default
        try fm.createDirectory(at: root, withIntermediateDirectories: true)
        let configuration = URLSessionConfiguration.ephemeral
        configuration.connectionProxyDictionary = [:]
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }
        let client = CommunityResourceClient(session: session)
        let installer = CommunityResourceInstaller(client: client, session: session)
        let profileID = "社区下载-Fabric"
        let java = try XCTUnwrap(JavaDiscovery.discover().first { $0.majorVersion == 17 && $0.architecture == InstallPlatform.architecture }, "This optional test needs native Java 17")
        let vanilla = MinecraftInstaller(session: session)
        let loaders = ModLoaderInstaller(session: session, vanillaInstaller: vanilla)
        var records: [[String: Any]] = []
        let profileURL = root.appendingPathComponent("versions/" + profileID)
        if !fm.fileExists(atPath: profileURL.appendingPathComponent(profileID + ".json").path) {
            print("COMMUNITY_SMOKE Installing Fabric 1.20.4 profile"); fflush(stdout)
            let manifest = try await vanilla.fetchManifest()
            let minecraft = try XCTUnwrap(manifest.versions.first { $0.id == "1.20.4" })
            try await loaders.install(version: ModLoaderVersion(kind: .fabric, minecraftVersion: "1.20.4", version: "0.16.0"),
                minecraftVersion: minecraft, gameDirectory: root, profileID: profileID, javaExecutable: java.executable, progress: { _ in })
        }
        let target = try XCTUnwrap(LocalVersionScanner.scan(gameDirectory: root).first { $0.id == profileID })
        var settings = try VersionSettingsRepository.load(version: target)
        settings.isolateGameDirectory = true
        try VersionSettingsRepository.save(settings, for: target)
        let world = profileURL.appendingPathComponent("saves/Community download fixture")
        try fm.createDirectory(at: world, withIntermediateDirectories: true)
        // A marker-only save fixture exercises the exact datapacks destination;
        // it is never presented as a playable/generated Minecraft world.
        if !fm.fileExists(atPath: world.appendingPathComponent("level.dat").path) {
            try Data("Community resource installer destination fixture; not a generated game world".utf8).write(to: world.appendingPathComponent("level.dat"))
        }
        func choose(_ slug: String, _ kind: CommunityResourceKind) async throws -> (CommunityProject, CommunityVersion) {
            let project = try await client.project(id: slug, source: .modrinth, kind: kind)
            let versions = try await client.versions(for: project, minecraftVersion: "1.20.4", loader: kind == .mod || kind == .modpack ? .fabric : nil)
            let version = try XCTUnwrap(versions.first { item in
                guard let file = item.primaryFile else { return false }
                return file.size <= 30 * 1024 * 1024 && (kind == .mod || (file.filename as NSString).pathExtension.lowercased() == (kind == .modpack ? "mrpack" : "zip"))
            }, "No bounded 1.20.4 file for \(slug)")
            return (project, version)
        }
        for (slug, kind) in [("indium", CommunityResourceKind.mod), ("fabric-api", .mod),
                              ("fast-better-grass", .resourcepack), ("makeup-ultra-fast-shaders", .shader), ("incendium", .datapack)] {
            let (project, version) = try await choose(slug, kind)
            print("COMMUNITY_SMOKE \(kind.rawValue) \(project.name) \(version.name)"); fflush(stdout)
            let result = try await installer.install(project: project, version: version, gameDirectory: root, targetVersion: target,
                worldDirectory: kind == .datapack ? world : nil, includeDependencies: true, progress: { _ in })
            let main = try XCTUnwrap(result.files.first { $0.lastPathComponent == version.primaryFile?.filename })
            XCTAssertTrue(try CommunityResourceInstaller.fileMatches(at: main, file: XCTUnwrap(version.primaryFile)))
            XCTAssertTrue(result.files.allSatisfy { $0.path.hasPrefix(profileURL.resolvingSymlinksInPath().path + "/") || $0.path.hasPrefix(profileURL.path + "/") })
            if slug == "indium" { XCTAssertGreaterThanOrEqual(result.files.count, 2, "Indium must install its pinned Sodium dependency automatically") }
            records.append(["slug": slug, "kind": kind.rawValue, "projectID": project.projectID, "versionID": version.id,
                "file": main.path, "bytes": version.primaryFile!.size, "hashVerified": true,
                "installedFiles": result.files.map(\.path), "downloaded": result.downloadedFiles, "reused": result.reusedFiles])
        }
        let index = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: profileURL.appendingPathComponent("PCL/community-resources.json"))) as? [String: [String: Any]])
        var verifiedRuntimeFiles = 0
        for (key, entry) in index {
            let pieces = key.split(separator: ":")
            guard pieces.contains("modrinth"), let id = entry["versionID"] as? String, let relative = entry["relativePath"] as? String else { continue }
            print("COMMUNITY_SMOKE Recheck version \(id)"); fflush(stdout)
            let version = try await client.version(id: id, source: .modrinth)
            let file = try XCTUnwrap(version.primaryFile)
            let runtime = try MinecraftVersionMetadata.safeFile(relativePath: relative, under: profileURL)
            XCTAssertTrue(try CommunityResourceInstaller.fileMatches(at: runtime, file: file), "Independent version metadata recheck: \(relative)")
            verifiedRuntimeFiles += 1
        }
        XCTAssertGreaterThanOrEqual(verifiedRuntimeFiles, 6)
        print("COMMUNITY_SMOKE Fetching online pack sop"); fflush(stdout)
        let (pack, packVersion) = try await choose("sop", .modpack)
        let archive = root.appendingPathComponent("community-pack.mrpack")
        try await installer.download(version: packVersion, to: archive, progress: { _ in })
        XCTAssertTrue(try CommunityResourceInstaller.fileMatches(at: archive, file: XCTUnwrap(packVersion.primaryFile)))
        let importedID = "社区整合包验证"
        if !fm.fileExists(atPath: root.appendingPathComponent("versions/\(importedID)/\(importedID).json").path) {
            print("COMMUNITY_SMOKE Online pack \(pack.name) \(packVersion.name)"); fflush(stdout)
            let importer = ModpackImporter(session: session, installer: vanilla, loaderInstaller: loaders, curseForgeAPIKey: "")
            try await importer.importPack(archive: archive, gameDirectory: root, profileID: importedID, javaExecutable: java.executable, progress: { _ in })
        }
        let imported = root.appendingPathComponent("versions/" + importedID)
        let plan = try MinecraftLauncher.makeLaunchPlan(configuration: LaunchConfiguration(gameDirectory: root, versionID: importedID,
            playerName: "PCLResource", javaExecutable: java.executable, memoryMB: 4096, runtimeGameDirectory: imported))
        XCTAssertTrue(plan.arguments.contains("net.fabricmc.loader.impl.launch.knot.KnotClient"))
        XCTAssertFalse(plan.arguments.contains { $0.contains("${") })
        let report = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: imported.appendingPathComponent("PCL/import-report.json"))) as? [String: Any])
        let output: [String: Any] = ["date": ISO8601DateFormatter().string(from: Date()), "root": root.path,
            "profile": profileID, "minecraft": "1.20.4", "loader": "Fabric 0.16.0", "resources": records,
            "independentlyVerifiedRuntimeFiles": verifiedRuntimeFiles,
            "datapackWorldIsDestinationFixtureOnly": true, "gameLaunched": false,
            "onlinePack": ["projectID": pack.projectID, "versionID": packVersion.id, "archiveHashVerified": true,
                "importReport": report, "offlineLaunchPlanVerified": true], "passed": true]
        let outputFile = env["PCL_COMMUNITY_SMOKE_REPORT"].map { URL(fileURLWithPath: $0) } ?? root.appendingPathComponent("community-smoke-report.json")
        try JSONSerialization.data(withJSONObject: output, options: [.prettyPrinted, .sortedKeys]).write(to: outputFile, options: .atomic)
        print("COMMUNITY_SMOKE COMPLETE resources=\(records.count) runtimeHashVerified=\(verifiedRuntimeFiles) onlinePack=true report=\(outputFile.path)"); fflush(stdout)
    }
}
