import XCTest
import CryptoKit
@testable import PCLCore

final class VersionPackExporterTests: XCTestCase {
    private func root() throws -> URL {
        let value = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-Export-\(UUID().uuidString)").resolvingSymlinksInPath()
        try FileManager.default.createDirectory(at: value, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: value) }
        return value
    }
    private func put(_ data: Data, at path: String, root: URL) throws {
        let file = root.appendingPathComponent(path)
        try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
        try data.write(to: file)
    }
    private func put(_ text: String, at path: String, root: URL) throws { try put(Data(text.utf8), at: path, root: root) }
    private func fixture(_ root: URL, isolated: Bool = true) throws -> InstalledVersion {
        try put(ExportFixtureProtocol.metadata, at: "versions/1.20.4/1.20.4.json", root: root)
        try put(ExportFixtureProtocol.client, at: "versions/1.20.4/1.20.4.jar", root: root)
        try put(ExportFixtureProtocol.library, at: "libraries/example/shared/1/shared-1.jar", root: root)
        try put(ExportFixtureProtocol.index, at: "assets/indexes/export-fixture.json", root: root)
        let hash = MinecraftInstaller.hash(ExportFixtureProtocol.asset)
        try put(ExportFixtureProtocol.asset, at: "assets/objects/\(hash.prefix(2))/\(hash)", root: root)
        let child: [String: Any] = ["id": "我的版本", "type": "release", "inheritsFrom": "1.20.4"]
        try put(JSONSerialization.data(withJSONObject: child), at: "versions/我的版本/我的版本.json", root: root)
        let version = InstalledVersion(id: "我的版本", type: "release", requiredJava: 17,
            directory: root.appendingPathComponent("versions/我的版本", isDirectory: true), inheritsFrom: "1.20.4")
        var profile = VersionProfileSettings(); profile.isolateGameDirectory = isolated; profile.javaPath = "/private/java/should-not-export"; profile.extraJVMArguments = "-Dprivate.value=personal"
        try VersionSettingsRepository.save(profile, for: version)
        let runtime = isolated ? version.directory : root
        try put("enabled mod bytes", at: "mods/example.jar", root: runtime)
        try put("disabled mod bytes", at: "mods/disabled.jar.disabled", root: runtime)
        try put("user option", at: "options.txt", root: runtime)
        try put("mod setting", at: "config/example.json", root: runtime)
        try put("secret account", at: "config/microsoft_accounts.json", root: runtime)
        try put("log", at: "config/cache/something.log", root: runtime)
        try put("a save", at: "saves/My world/level.dat", root: runtime)
        try put("screenshot", at: "screenshots/test.png", root: runtime)
        try put("server address", at: "servers.dat", root: runtime)
        try put("secret launcher account", at: "launcher_accounts.json", root: runtime)
        return version
    }
    private func paths(_ url: URL) throws -> Set<String> { Set(try ModpackZIPArchive(url: url).entries.map(\.path)) }
    private func archiveData(_ path: String, archive: ModpackZIPArchive) throws -> Data {
        try archive.data(for: XCTUnwrap(archive.member(path)), maximumBytes: 16 * 1024 * 1024)
    }

    func testCompleteZipFlattensInheritanceIncludesDependenciesAndRoundTripsThroughImporter() async throws {
        let workspace = try root(), game = workspace.appendingPathComponent("game")
        let version = try fixture(game)
        let source = try Data(contentsOf: version.directory.appendingPathComponent("我的版本.json"))
        let destination = workspace.appendingPathComponent("完整版本.zip")
        let result = try await VersionPackExporter().export(version: version, gameDirectory: game, options: .init(), to: destination)
        XCTAssertEqual(result.format, .completeZip)
        let archive = try ModpackZIPArchive(url: destination), names = Set(archive.entries.map(\.path))
        XCTAssertTrue(names.contains("versions/我的版本/我的版本.jar"))
        XCTAssertTrue(names.contains("libraries/example/shared/1/shared-1.jar"))
        XCTAssertTrue(names.contains("versions/我的版本/mods/example.jar"))
        XCTAssertFalse(names.contains(where: { $0.contains("saves/") || $0.contains("disabled.jar.disabled") || $0.hasSuffix("accounts.json") || $0.hasSuffix("servers.dat") }))
        let flattened = try XCTUnwrap(JSONSerialization.jsonObject(with: archiveData("versions/我的版本/我的版本.json", archive: archive)) as? [String: Any])
        XCTAssertNil(flattened["inheritsFrom"]); XCTAssertEqual(flattened["jar"] as? String, "我的版本")
        XCTAssertEqual(flattened["pclOriginalVersion"] as? String, "1.20.4")
        let safe = try XCTUnwrap(JSONSerialization.jsonObject(with: archiveData("versions/我的版本/PCL/macos-settings.json", archive: archive)) as? [String: Any])
        XCTAssertEqual(safe["isolateGameDirectory"] as? Bool, true); XCTAssertNil(safe["javaPath"]); XCTAssertNil(safe["extraJVMArguments"])
        let config = URLSessionConfiguration.ephemeral; config.protocolClasses = [ExportFixtureProtocol.self]
        let session = URLSession(configuration: config)
        let installer = MinecraftInstaller(session: session, manifestURL: URL(string: "https://pcl-export.invalid/manifest")!)
        let importer = ModpackImporter(session: session, installer: installer)
        let descriptor = try await importer.inspect(archive: destination)
        XCTAssertEqual(descriptor.format, .completeVersion); XCTAssertEqual(descriptor.minecraftVersion, "1.20.4")
        let imported = workspace.appendingPathComponent("imported")
        try await importer.importPack(archive: destination, gameDirectory: imported, profileID: "已导入", javaExecutable: URL(fileURLWithPath: "/usr/bin/true"), progress: { _ in })
        XCTAssertEqual(try Data(contentsOf: imported.appendingPathComponent("versions/已导入/mods/example.jar")), Data("enabled mod bytes".utf8))
        XCTAssertEqual(try Data(contentsOf: imported.appendingPathComponent("versions/已导入/已导入.jar")), ExportFixtureProtocol.client)
        XCTAssertEqual(try Data(contentsOf: version.directory.appendingPathComponent("我的版本.json")), source)
    }

    func testModrinthExportsBundledOverridesWithoutExternalKeysAndPreservesLoader() async throws {
        let workspace = try root(), game = workspace.appendingPathComponent("game")
        let version = try fixture(game)
        let profileFile = version.directory.appendingPathComponent("我的版本.json")
        var metadata = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: profileFile)) as? [String: Any])
        metadata["pclLoaderKind"] = "fabric"; metadata["pclLoaderVersion"] = "0.18.4"
        try JSONSerialization.data(withJSONObject: metadata).write(to: profileFile)
        try put("optifine", at: "mods/OptiFine.jar", root: version.directory)
        var options = VersionPackExportOptions(); options.name = "测试整合包"; options.format = .modrinth; options.includeDisabledMods = true
        let destination = workspace.appendingPathComponent("pack.mrpack")
        _ = try await VersionPackExporter().export(version: version, gameDirectory: game, options: options, to: destination)
        let descriptor = try await ModpackImporter().inspect(archive: destination)
        XCTAssertEqual(descriptor.format, .modrinth); XCTAssertEqual(descriptor.loaderKind, .fabric); XCTAssertEqual(descriptor.loaderVersion, "0.18.4")
        let archive = try ModpackZIPArchive(url: destination)
        XCTAssertNotNil(archive.member("overrides/mods/example.jar")); XCTAssertNotNil(archive.member("overrides/mods/disabled.jar.disabled"))
        XCTAssertNil(archive.member("overrides/mods/OptiFine.jar"))
        let index = try XCTUnwrap(JSONSerialization.jsonObject(with: archiveData("modrinth.index.json", archive: archive)) as? [String: Any])
        XCTAssertEqual((index["files"] as? [Any])?.count, 0)
        XCTAssertFalse(descriptor.needsAPIKey)
    }

    func testSharedRuntimeSelectionAndOptionalPersonalData() async throws {
        let workspace = try root(), game = workspace.appendingPathComponent("game")
        let version = try fixture(game, isolated: false)
        try put("wrong isolated file", at: "mods/wrong.jar", root: version.directory)
        var options = VersionPackExportOptions(); options.includeSaves = true; options.includeScreenshots = true; options.includeServers = true; options.includeMods = false
        let destination = workspace.appendingPathComponent("shared.zip")
        _ = try await VersionPackExporter().export(version: version, gameDirectory: game, options: options, to: destination)
        let names = try paths(destination)
        XCTAssertTrue(names.contains("versions/我的版本/saves/My world/level.dat"))
        XCTAssertTrue(names.contains("versions/我的版本/screenshots/test.png")); XCTAssertTrue(names.contains("versions/我的版本/servers.dat"))
        XCTAssertFalse(names.contains(where: { $0.contains("/mods/") || $0.contains("/config/") || $0.hasSuffix("accounts.json") }))
    }

    func testIndividualResourceShaderAndSaveSelectionsNeverIncludeUnselectedEntries() async throws {
        let workspace = try root(), game = workspace.appendingPathComponent("game")
        let version = try fixture(game)
        for path in ["resourcepacks/Selected.zip", "resourcepacks/Other.zip", "texturepacks/Other.zip",
                     "shaderpacks/Selected.zip", "shaderpacks/Other.zip", "saves/Other world/level.dat"] {
            try put(path, at: path, root: version.directory)
        }
        var options = VersionPackExportOptions()
        options.selectedResourcePacks = ["resourcepacks/Selected.zip"]
        options.selectedShaderPacks = ["Selected.zip"]
        options.includeSaves = true; options.selectedSaves = ["My world"]
        let destination = workspace.appendingPathComponent("selected.zip")
        _ = try await VersionPackExporter().export(version: version, gameDirectory: game, options: options, to: destination)
        let names = try paths(destination)
        XCTAssertTrue(names.contains("versions/我的版本/resourcepacks/Selected.zip"))
        XCTAssertTrue(names.contains("versions/我的版本/shaderpacks/Selected.zip"))
        XCTAssertTrue(names.contains("versions/我的版本/saves/My world/level.dat"))
        XCTAssertFalse(names.contains(where: { $0.contains("Other") }))
        options.selectedResourcePacks = []; options.selectedShaderPacks = []; options.selectedSaves = []
        let empty = workspace.appendingPathComponent("deselected.zip")
        _ = try await VersionPackExporter().export(version: version, gameDirectory: game, options: options, to: empty)
        XCTAssertFalse(try paths(empty).contains(where: { $0.contains("/resourcepacks/") || $0.contains("/shaderpacks/") || $0.contains("/saves/") }))
    }

    func testIndividualSelectionsRejectTraversalAndMissingEntries() async throws {
        let workspace = try root(), game = workspace.appendingPathComponent("game")
        let version = try fixture(game), destination = workspace.appendingPathComponent("selected.zip")
        var options = VersionPackExportOptions(); options.includeSaves = true
        for path in ["../private", "saves/../private", "saves/My world/level.dat", "other/World", "saves\\World"] {
            options.selectedSaves = [path]
            do { _ = try await VersionPackExporter().export(version: version, gameDirectory: game, options: options, to: destination); XCTFail("Unsafe selection must reject: \(path)") } catch { }
            XCTAssertFalse(FileManager.default.fileExists(atPath: destination.path))
        }
        options.selectedSaves = ["saves/Missing world"]
        do { _ = try await VersionPackExporter().export(version: version, gameDirectory: game, options: options, to: destination); XCTFail("Missing explicit selection must reject") } catch { }
        XCTAssertFalse(FileManager.default.fileExists(atPath: destination.path))
    }

    func testShaderCandidatesAndCompanionSettingsFollowSelectedPacksAndFlag() async throws {
        let workspace = try root(), game = workspace.appendingPathComponent("game")
        let version = try fixture(game)
        for path in ["shaderpacks/One.zip", "shaderpacks/One.zip.txt", "shaderpacks/Two.zip", "shaderpacks/Two.zip.txt",
                     "shaderpacks/Folder/shaders/program.glsl", "shaderpacks/Folder.txt", "shaderpacks/README.txt",
                     "resourcepacks/Actual.zip", "resourcepacks/Actual.zip.txt", "resourcepacks/README.txt"] {
            try put(path, at: path, root: version.directory)
        }
        let shaders = try VersionPackExporter.selectableEntries(in: version.directory, folders: ["shaderpacks"])
        XCTAssertEqual(Set(shaders.map(\.relativePath)), ["shaderpacks/One.zip", "shaderpacks/Two.zip", "shaderpacks/Folder"])
        let resources = try VersionPackExporter.selectableEntries(in: version.directory, folders: ["resourcepacks"])
        XCTAssertEqual(resources.map(\.relativePath), ["resourcepacks/Actual.zip"])
        var options = VersionPackExportOptions()
        let all = workspace.appendingPathComponent("all-packs.zip")
        _ = try await VersionPackExporter().export(version: version, gameDirectory: game, options: options, to: all)
        let allNames = try paths(all)
        XCTAssertTrue(allNames.contains("versions/我的版本/shaderpacks/One.zip.txt"))
        XCTAssertTrue(allNames.contains("versions/我的版本/shaderpacks/Two.zip.txt"))
        XCTAssertTrue(allNames.contains("versions/我的版本/shaderpacks/Folder.txt"))
        XCTAssertFalse(allNames.contains(where: { $0.hasSuffix("README.txt") || $0.hasSuffix("Actual.zip.txt") }))
        options.selectedShaderPacks = ["One.zip"]
        let selected = workspace.appendingPathComponent("one-pack.zip")
        _ = try await VersionPackExporter().export(version: version, gameDirectory: game, options: options, to: selected)
        let selectedNames = try paths(selected)
        XCTAssertTrue(selectedNames.contains("versions/我的版本/shaderpacks/One.zip.txt"))
        XCTAssertFalse(selectedNames.contains(where: { $0.contains("shaderpacks/Two") || $0.contains("shaderpacks/Folder") }))
        options.includeShaderPackSettings = false
        let noSettings = workspace.appendingPathComponent("without-settings.zip")
        _ = try await VersionPackExporter().export(version: version, gameDirectory: game, options: options, to: noSettings)
        XCTAssertTrue(try paths(noSettings).contains("versions/我的版本/shaderpacks/One.zip"))
        XCTAssertFalse(try paths(noSettings).contains("versions/我的版本/shaderpacks/One.zip.txt"))
        options.includeShaderPackSettings = true; options.selectedShaderPacks = []
        let none = workspace.appendingPathComponent("no-shaders.zip")
        _ = try await VersionPackExporter().export(version: version, gameDirectory: game, options: options, to: none)
        XCTAssertFalse(try paths(none).contains(where: { $0.contains("/shaderpacks/") }))
    }

    func testOlderExportConfigurationDefaultsShaderSettingsToEnabled() throws {
        let encoder = JSONEncoder(), decoder = JSONDecoder()
        let encoded = try encoder.encode(VersionPackExportOptions())
        var legacy = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        legacy.removeValue(forKey: "includeShaderPackSettings")
        let restored = try decoder.decode(VersionPackExportOptions.self, from: JSONSerialization.data(withJSONObject: legacy))
        XCTAssertTrue(restored.includeShaderPackSettings)
        var disabled = restored; disabled.includeShaderPackSettings = false
        XCTAssertFalse(try decoder.decode(VersionPackExportOptions.self, from: encoder.encode(disabled)).includeShaderPackSettings)
    }

    func testSymlinksAndDestinationInsideGameRejectWithoutPublishing() async throws {
        let workspace = try root(), game = workspace.appendingPathComponent("game")
        let version = try fixture(game)
        let outside = workspace.appendingPathComponent("private.jar"); try Data("private".utf8).write(to: outside)
        try FileManager.default.createSymbolicLink(at: version.directory.appendingPathComponent("mods/link.jar"), withDestinationURL: outside)
        let exporter = VersionPackExporter(), target = workspace.appendingPathComponent("bad.zip")
        do { _ = try await exporter.export(version: version, gameDirectory: game, options: .init(), to: target); XCTFail("Must reject a selected symlink") } catch { }
        XCTAssertFalse(FileManager.default.fileExists(atPath: target.path))
        do { _ = try await exporter.export(version: version, gameDirectory: game, options: .init(), to: game.appendingPathComponent("bad.zip")); XCTFail("Must reject publishing into source") } catch { }
        XCTAssertEqual(try Data(contentsOf: outside), Data("private".utf8))
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: workspace.path).contains(where: { $0.hasPrefix(".pcl-export-") }))
    }

    func testCorruptDependencyAndCollisionPreserveExistingDestination() async throws {
        let workspace = try root(), game = workspace.appendingPathComponent("game")
        let version = try fixture(game), destination = workspace.appendingPathComponent("existing.zip")
        let original = Data("existing user file".utf8); try original.write(to: destination)
        let exporter = VersionPackExporter()
        do { _ = try await exporter.export(version: version, gameDirectory: game, options: .init(), to: destination); XCTFail("Must reject collision") } catch { }
        try put(Data(repeating: 0, count: ExportFixtureProtocol.library.count), at: "libraries/example/shared/1/shared-1.jar", root: game)
        do { _ = try await exporter.export(version: version, gameDirectory: game, options: .init(), to: destination, replaceExisting: true); XCTFail("Must verify dependency hash") } catch { }
        XCTAssertEqual(try Data(contentsOf: destination), original)
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: workspace.path).contains(where: { $0.hasPrefix(".pcl-export-") }))
    }

    func testCancelledStreamingExportPreservesTargetAndCleansTemporaryFile() async throws {
        let workspace = try root(), game = workspace.appendingPathComponent("game")
        let version = try fixture(game)
        try put(Data(repeating: 7, count: 12 * 1024 * 1024), at: "mods/large.jar", root: version.directory)
        let destination = workspace.appendingPathComponent("existing.zip"), original = Data("retain existing export".utf8)
        try original.write(to: destination)
        let cancellation = ExportCancellationProbe()
        let exporter = VersionPackExporter()
        let task = Task {
            try await exporter.export(version: version, gameDirectory: game, options: .init(), to: destination, replaceExisting: true) { value in
                if value.completedBytes > 0 { cancellation.cancel() }
            }
        }
        cancellation.set(task)
        do { _ = try await task.value; XCTFail("Cancellation must stop publication") } catch is CancellationError { }
        XCTAssertEqual(try Data(contentsOf: destination), original)
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: workspace.path).contains(where: { $0.hasPrefix(".pcl-export-") }))
        let retry = workspace.appendingPathComponent("retry.zip")
        _ = try await exporter.export(version: version, gameDirectory: game, options: .init(), to: retry)
        XCTAssertTrue(FileManager.default.fileExists(atPath: retry.path))
    }
}

private final class ExportCancellationProbe: @unchecked Sendable {
    private let lock = NSLock()
    private var task: Task<VersionPackExportResult, Error>?
    private var shouldCancel = false
    func set(_ value: Task<VersionPackExportResult, Error>) { lock.lock(); task = value; let cancel = shouldCancel; lock.unlock(); if cancel { value.cancel() } }
    func cancel() { lock.lock(); shouldCancel = true; let value = task; lock.unlock(); value?.cancel() }
}

private final class ExportFixtureProtocol: URLProtocol {
    static let client = Data("fixture Minecraft client".utf8)
    static let library = Data("fixture library".utf8)
    static let asset = Data("fixture asset".utf8)
    static var index: Data {
        try! JSONSerialization.data(withJSONObject: ["objects": ["example/resource.ogg": ["hash": MinecraftInstaller.hash(asset), "size": asset.count]]], options: [.sortedKeys])
    }
    static var metadata: Data {
        let value: [String: Any] = ["id": "1.20.4", "type": "release", "mainClass": "net.minecraft.client.main.Main", "javaVersion": ["majorVersion": 17],
            "libraries": [["name": "example:shared:1", "downloads": ["artifact": ["path": "example/shared/1/shared-1.jar", "url": "https://pcl-export.invalid/shared-1.jar", "sha1": MinecraftInstaller.hash(library), "size": library.count]]]],
            "downloads": ["client": ["url": "https://pcl-export.invalid/client.jar", "sha1": MinecraftInstaller.hash(client), "size": client.count]],
            "assetIndex": ["id": "export-fixture", "url": "https://pcl-export.invalid/index.json", "sha1": MinecraftInstaller.hash(index), "size": index.count]]
        return try! JSONSerialization.data(withJSONObject: value, options: [.sortedKeys])
    }
    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        guard let url = request.url else { return }
        let data: Data
        switch url.lastPathComponent {
        case "manifest": data = try! JSONSerialization.data(withJSONObject: ["latest": ["release": "1.20.4", "snapshot": "1.20.4"], "versions": [["id": "1.20.4", "type": "release", "url": "https://pcl-export.invalid/version", "sha1": MinecraftInstaller.hash(Self.metadata)]]])
        case "version": data = Self.metadata
        case "client.jar": data = Self.client
        case "shared-1.jar": data = Self.library
        case "index.json": data = Self.index
        case MinecraftInstaller.hash(Self.asset): data = Self.asset
        default: client?.urlProtocol(self, didFailWithError: URLError(.resourceUnavailable)); return
        }
        client?.urlProtocol(self, didReceive: HTTPURLResponse(url: url, statusCode: 200, httpVersion: "HTTP/1.1", headerFields: nil)!, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data); client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() {}
}
