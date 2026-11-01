import CryptoKit
import Foundation
import XCTest
import zlib
@testable import PCLCore

final class FabricAPIComponentTests: XCTestCase {
    private func root() throws -> URL {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-FabricAPI-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        return root
    }
    private func profile(_ root: URL) throws -> InstalledVersion {
        let folder = root.appendingPathComponent("versions/Fabric")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        return InstalledVersion(id: "Fabric", type: "custom", requiredJava: 17, directory: folder)
    }
    private func jar(_ name: String, at folder: URL, id: String = "fabric-api", version: String = "1", marker: String = "") throws -> URL {
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let metadata = try JSONSerialization.data(withJSONObject: ["schemaVersion": 1, "id": id, "name": id, "version": version], options: [.sortedKeys])
        let bytes = try fabricAPIZIP([("fabric.mod.json", metadata), ("marker", Data(marker.utf8))])
        let url = folder.appendingPathComponent(name); try bytes.write(to: url); return url
    }
    private func version(_ file: URL, id: String = "api-v1", minecraft: String = "1.20.1") throws -> CommunityVersion {
        let bytes = try Data(contentsOf: file)
        return CommunityVersion(id: id, source: .modrinth, projectID: FabricAPIComponent.projectID,
            name: "Fabric API", versionNumber: "1", gameVersions: [minecraft], loaders: ["fabric"], files: [
                .init(filename: file.lastPathComponent, size: Int64(bytes.count), urls: [URL(string: "https://cdn.modrinth.com/" + file.lastPathComponent)!],
                      sha1: MinecraftInstaller.hash(bytes), sha512: SHA512.hash(data: bytes).map { String(format: "%02x", $0) }.joined(), primary: true)
            ])
    }
    private func state(_ root: URL, _ profile: InstalledVersion, isolated: Bool = true) throws -> FabricAPIState {
        try FabricAPIComponent.installed(gameDirectory: root, version: profile, isolate: isolated)
    }

    func testInstalledStateOnlyIncludesActiveFabricAPIAndDetectsSameVersionByteChanges() throws {
        let root = try root(), target = try profile(root), mods = target.directory.appendingPathComponent("mods")
        let active = try jar("renamed.jar", at: mods, version: "0.92.7+1.20.1")
        _ = try jar("older.jar.disabled", at: mods, version: "old")
        _ = try jar("sodium.jar", at: mods, id: "sodium")
        let before = try state(root, target)
        XCTAssertEqual(before.files.map(\.filename), ["renamed.jar"])
        XCTAssertEqual(before.displayVersion, "0.92.7+1.20.1")
        XCTAssertTrue(before.matches(try version(active)))
        _ = try jar("renamed.jar", at: mods, version: "0.92.7+1.20.1", marker: "external edit")
        XCTAssertNotEqual(before.fingerprint, try state(root, target).fingerprint)
        XCTAssertFalse(before.matches(try version(active)))
    }

    func testInstallAndRollbackLeaveOtherModsAndDisabledJarsUntouched() throws {
        let root = try root(), target = try profile(root), mods = target.directory.appendingPathComponent("mods")
        let other = try jar("other.jar", at: mods, id: "other"), disabled = try jar("api.jar.disabled", at: mods)
        let otherBytes = try Data(contentsOf: other), disabledBytes = try Data(contentsOf: disabled)
        let incoming = try jar("fabric-api-new.jar", at: root.appendingPathComponent("input"), version: "2")
        let txn = try FabricAPIComponent.apply(preparedFile: incoming, gameDirectory: root, version: target, isolate: true, expectedState: state(root, target))
        XCTAssertEqual(try state(root, target).displayVersion, "2")
        try txn.rollback()
        XCTAssertTrue(try state(root, target).files.isEmpty)
        XCTAssertEqual(try Data(contentsOf: other), otherBytes)
        XCTAssertEqual(try Data(contentsOf: disabled), disabledBytes)
    }

    func testUpdateAndRemovalCanRestoreOriginalBytesInCommonRuntime() throws {
        let root = try root(), target = try profile(root), mods = root.appendingPathComponent("mods")
        let original = try jar("my-api.jar", at: mods, version: "1"), oldBytes = try Data(contentsOf: original)
        let incoming = try jar("fabric-api-new.jar", at: root.appendingPathComponent("input"), version: "2")
        let txn = try FabricAPIComponent.apply(preparedFile: incoming, gameDirectory: root, version: target, isolate: false, expectedState: state(root, target, isolated: false))
        XCTAssertEqual(try state(root, target, isolated: false).displayVersion, "2")
        XCTAssertFalse(FileManager.default.fileExists(atPath: original.path))
        try txn.rollback()
        XCTAssertEqual(try Data(contentsOf: original), oldBytes)
        let removal = try FabricAPIComponent.apply(preparedFile: nil, gameDirectory: root, version: target, isolate: false, expectedState: state(root, target, isolated: false))
        XCTAssertTrue(try state(root, target, isolated: false).files.isEmpty)
        try removal.rollback()
        XCTAssertEqual(try Data(contentsOf: original), oldBytes)
    }

    func testIdenticalRenamedJarIsReusedWithoutPublishingSecondCopy() throws {
        let root = try root(), target = try profile(root), mods = target.directory.appendingPathComponent("mods")
        let existing = try jar("自定义文件名.jar", at: mods)
        let incoming = try jar("fabric-api-canonical.jar", at: root.appendingPathComponent("input"))
        XCTAssertEqual(try Data(contentsOf: existing), try Data(contentsOf: incoming))
        let txn = try FabricAPIComponent.apply(preparedFile: incoming, gameDirectory: root, version: target, isolate: true, expectedState: state(root, target))
        txn.finish(); try txn.rollback()
        XCTAssertEqual(try state(root, target).files.map(\.filename), ["自定义文件名.jar"])
    }

    func testStaleSnapshotWrongModAndFilenameCollisionNeverChangeExistingFiles() throws {
        let root = try root(), target = try profile(root), mods = target.directory.appendingPathComponent("mods")
        let old = try jar("old.jar", at: mods), before = try state(root, target)
        let incoming = try jar("next.jar", at: root.appendingPathComponent("input"), version: "2")
        _ = try jar("second.jar", at: mods, version: "another")
        XCTAssertThrowsError(try FabricAPIComponent.apply(preparedFile: incoming, gameDirectory: root, version: target, isolate: true, expectedState: before))
        let fresh = try state(root, target), oldBytes = try Data(contentsOf: old)
        let wrong = try jar("wrong.jar", at: root.appendingPathComponent("input"), id: "sodium")
        XCTAssertThrowsError(try FabricAPIComponent.apply(preparedFile: wrong, gameDirectory: root, version: target, isolate: true, expectedState: fresh))
        let collision = try jar("next.jar", at: mods, id: "other")
        let collisionBytes = try Data(contentsOf: collision)
        XCTAssertThrowsError(try FabricAPIComponent.apply(preparedFile: incoming, gameDirectory: root, version: target, isolate: true, expectedState: fresh))
        XCTAssertEqual(try Data(contentsOf: old), oldBytes)
        XCTAssertEqual(try Data(contentsOf: collision), collisionBytes)
        XCTAssertEqual(try state(root, target).fingerprint, fresh.fingerprint)
    }

    func testFinishAfterVersionRenameRetainsInstalledApiAndDiscardsOnlyPrivateBackup() throws {
        let root = try root(), target = try profile(root), mods = target.directory.appendingPathComponent("mods")
        _ = try jar("old.jar", at: mods)
        let incoming = try jar("new.jar", at: root.appendingPathComponent("input"), version: "2")
        let txn = try FabricAPIComponent.apply(preparedFile: incoming, gameDirectory: root, version: target, isolate: true, expectedState: state(root, target))
        let final = root.appendingPathComponent("versions/Renamed")
        try FileManager.default.moveItem(at: target.directory, to: final)
        txn.finish()
        XCTAssertTrue(FileManager.default.fileExists(atPath: final.appendingPathComponent("mods/new.jar").path))
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: root.path).contains { $0.hasPrefix(".pcl-fabric-api-") })
    }

    func testUnsafeModsAndCacheLinksAreRejectedWithoutTouchingTheirTargets() async throws {
        let root = try root(), target = try profile(root), outside = try self.root()
        let original = try jar("keep.jar", at: outside), bytes = try Data(contentsOf: original)
        try FileManager.default.createSymbolicLink(at: target.directory.appendingPathComponent("mods"), withDestinationURL: outside)
        XCTAssertThrowsError(try state(root, target))
        let input = try jar("new.jar", at: root.appendingPathComponent("input"))
        let component = FabricAPIComponent()
        try FileManager.default.createSymbolicLink(at: root.appendingPathComponent("PCL"), withDestinationURL: outside)
        do { _ = try await component.prepare(version: version(input), gameDirectory: root, reuseFiles: [input], progress: { _ in }); XCTFail("Cache link must be rejected") }
        catch { }
        XCTAssertEqual(try Data(contentsOf: original), bytes)
    }

    func testPrepareDownloadsOnceAndReusesRenamedExistingBytesWithNoNetwork() async throws {
        let root = try root(), input = try jar("canonical.jar", at: root.appendingPathComponent("input"))
        let bytes = try Data(contentsOf: input), fixture = FabricAPIFixture(files: ["canonical.jar": bytes])
        let (component, session) = setup(fixture)
        defer { session.invalidateAndCancel() }
        let requested = try version(input)
        let one = try await component.prepare(version: requested, gameDirectory: root, progress: { _ in })
        let two = try await component.prepare(version: requested, gameDirectory: root, progress: { _ in })
        XCTAssertEqual(one, two)
        XCTAssertEqual(fixture.paths.filter { $0 == "/canonical.jar" }.count, 1)
        XCTAssertEqual(try Data(contentsOf: one), bytes)
        let root2 = try self.root(), renamed = try jar("renamed.jar", at: root2.appendingPathComponent("mods"))
        _ = try await component.prepare(version: requested, gameDirectory: root2, reuseFiles: [renamed], progress: { _ in })
        XCTAssertEqual(fixture.paths.filter { $0 == "/canonical.jar" }.count, 1)
    }

    func testCatalogueFiltersExactMinecraftAndFabricAndRejectsProjectSubstitution() async throws {
        let root = try root(), input = try jar("api.jar", at: root)
        let file = try version(input).primaryFile!
        func entry(_ id: String, minecraft: String, loader: String) -> [String: Any] {
            ["id": id, "project_id": FabricAPIComponent.projectID, "name": id, "version_number": id,
             "game_versions": [minecraft], "loaders": [loader], "files": [["filename": file.filename, "size": file.size,
                "url": file.urls[0].absoluteString, "hashes": ["sha1": file.sha1!]]]]
        }
        let fixture = FabricAPIFixture(catalogue: [entry("good", minecraft: "1.20.1", loader: "fabric"),
            entry("othermc", minecraft: "1.21", loader: "fabric"), entry("forge", minecraft: "1.20.1", loader: "forge")])
        let (component, session) = setup(fixture); defer { session.invalidateAndCancel() }
        let versions = try await component.versions(minecraftVersion: "1.20.1")
        XCTAssertEqual(versions.map(\.id), ["good"])
        XCTAssertTrue(fixture.queries.contains { $0.contains("game_versions") && $0.contains("loaders") })
        fixture.projectID = "substitution"
        do { _ = try await component.versions(minecraftVersion: "1.20.1"); XCTFail("Wrong project") } catch { }
    }

    func testApiOnlyUpdateLeavesMinecraftClientAndMetadataByteIdentical() throws {
        let root = try root(), target = try profile(root), mods = target.directory.appendingPathComponent("mods")
        let json = target.directory.appendingPathComponent("Fabric.json"), client = target.directory.appendingPathComponent("Fabric.jar")
        try Data("original metadata".utf8).write(to: json); try Data("original client".utf8).write(to: client)
        let oldJSON = try Data(contentsOf: json), oldClient = try Data(contentsOf: client)
        _ = try jar("api-old.jar", at: mods)
        let incoming = try jar("api-new.jar", at: root.appendingPathComponent("input"), version: "2")
        let txn = try FabricAPIComponent.apply(preparedFile: incoming, gameDirectory: root, version: target, isolate: true, expectedState: state(root, target))
        txn.finish()
        XCTAssertEqual(try state(root, target).displayVersion, "2")
        XCTAssertEqual(try Data(contentsOf: json), oldJSON)
        XCTAssertEqual(try Data(contentsOf: client), oldClient)
    }

    func testClientRenameFailureRollsBackUpdatedApiClientAndMetadataTogether() throws {
        let root = try root(), target = try profile(root)
        func metadata(_ id: String, _ json: [String: Any], client: Data?) throws {
            let folder = root.appendingPathComponent("versions/" + id)
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            try JSONSerialization.data(withJSONObject: json, options: [.sortedKeys]).write(to: folder.appendingPathComponent(id + ".json"))
            if let client { try client.write(to: folder.appendingPathComponent(id + ".jar")) }
        }
        let newClient = Data("prepared new Minecraft client".utf8)
        try metadata(target.id, ["id": target.id, "mainClass": "old.Main", "pclOriginalVersion": "1.20.1"], client: Data("old Minecraft client".utf8))
        try metadata("1.20.4", ["id": "1.20.4", "mainClass": "new.Main", "downloads": ["client": ["size": newClient.count, "sha1": MinecraftInstaller.hash(newClient)]]], client: newClient)
        try metadata("prepared", ["id": "prepared", "inheritsFrom": "1.20.4", "mainClass": "fabric.Main", "pclLoaderKind": "fabric", "pclLoaderVersion": "0.18.4"], client: nil)
        // The final rename fails after the client/metadata replacement, exercising
        // VersionComponentManager's rollback plus its caller's Mod rollback.
        try Data("user auxiliary file".utf8).write(to: target.directory.appendingPathComponent("renamed.json"))
        let json = target.directory.appendingPathComponent("Fabric.json"), client = target.directory.appendingPathComponent("Fabric.jar")
        let oldJSON = try Data(contentsOf: json), oldClient = try Data(contentsOf: client)
        let oldAPI = try jar("api-old.jar", at: target.directory.appendingPathComponent("mods")), oldAPIBytes = try Data(contentsOf: oldAPI)
        let incoming = try jar("api-new.jar", at: root.appendingPathComponent("input"), version: "2")
        let fingerprint = try VersionComponentManager.fingerprint(version: target, gameDirectory: root)
        let txn = try FabricAPIComponent.apply(preparedFile: incoming, gameDirectory: root, version: target, isolate: true, expectedState: state(root, target))
        do {
            _ = try VersionComponentManager.applyPreparedProfile(version: target, preparedID: "prepared", gameDirectory: root, newName: "renamed", expectedFingerprint: fingerprint)
            XCTFail("Must reject auxiliary filename collision")
        } catch { try txn.rollback() }
        XCTAssertEqual(try Data(contentsOf: oldAPI), oldAPIBytes)
        XCTAssertEqual(try Data(contentsOf: json), oldJSON)
        XCTAssertEqual(try Data(contentsOf: client), oldClient)
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/renamed").path))
        XCTAssertEqual(try state(root, target).files.map(\.filename), ["api-old.jar"])
    }

    func testLiveExistingPackFabricApiReuseWithoutTouchingItsProfile() async throws {
        let environment = ProcessInfo.processInfo.environment
        guard let outputPath = environment["PCL_FABRIC_API_LIVE_DIR"], let gamePath = environment["PCL_FABRIC_API_EXISTING_GAME_DIR"] else {
            throw XCTSkip("Opt in with PCL_FABRIC_API_LIVE_DIR and PCL_FABRIC_API_EXISTING_GAME_DIR for read-only catalogue/reuse validation")
        }
        let output = URL(fileURLWithPath: outputPath), game = URL(fileURLWithPath: gamePath)
        try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
        let target = try XCTUnwrap(LocalVersionScanner.scan(gameDirectory: game).first { $0.id == "乌托邦探险之旅3.5.2" })
        let profile = try VersionSettingsRepository.load(version: target)
        let installed = try state(game, target, isolated: profile.isolateGameDirectory)
        XCTAssertEqual(installed.files.count, 1)
        let before = try installed.files.map { try Data(contentsOf: $0.url) }
        let component = FabricAPIComponent()
        let versions = try await component.versions(minecraftVersion: "1.20.1")
        let exact = try XCTUnwrap(versions.first { installed.matches($0) }, "Official catalogue must recognize the existing whole Fabric API JAR")
        let scratch = output.appendingPathComponent("reuse-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: scratch, withIntermediateDirectories: true)
        let relay = FabricAPIMessageRelay()
        let prepared = try await component.prepare(version: exact, gameDirectory: scratch, reuseFiles: installed.files.map(\.url), progress: { relay.append($0.message) })
        XCTAssertEqual(try Data(contentsOf: prepared), before[0])
        XCTAssertTrue(relay.messages.contains("复用已有 Fabric API"))
        for (file, bytes) in zip(installed.files, before) { XCTAssertEqual(try Data(contentsOf: file.url), bytes) }
        let report: [String: Any] = ["minecraft": "1.20.1", "catalogueCount": versions.count, "matchedVersion": exact.versionNumber,
            "existingJAR": installed.files[0].filename, "officialFullHashesMatched": true, "downloadedModBytes": 0,
            "preparedCache": prepared.path, "originalProfileUntouched": true, "gameplayLaunched": false]
        try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted,.sortedKeys]).write(to: output.appendingPathComponent("live-reuse.json"))
        print("FABRIC_API_LIVE \(exact.versionNumber); \(versions.count) matching Minecraft catalogue entries; full existing JAR reused without network Mod transfer")
    }

    private func setup(_ fixture: FabricAPIFixture) -> (FabricAPIComponent, URLSession) {
        FabricAPIProtocol.register(fixture)
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [FabricAPIProtocol.self]
        configuration.httpAdditionalHeaders = ["X-PCL-Fabric-APIFixture": fixture.id]
        let session = URLSession(configuration: configuration)
        addTeardownBlock { session.invalidateAndCancel(); FabricAPIProtocol.unregister(fixture) }
        return (FabricAPIComponent(session: session), session)
    }
}

private final class FabricAPIFixture: @unchecked Sendable {
    let id = UUID().uuidString
    private let lock = NSLock()
    var projectID = FabricAPIComponent.projectID
    let files: [String: Data]
    let catalogue: [[String: Any]]
    private var visited: [String] = []
    private var parameters: [String] = []
    init(files: [String: Data] = [:], catalogue: [[String: Any]] = []) { self.files = files; self.catalogue = catalogue }
    var paths: [String] { lock.lock(); defer { lock.unlock() }; return visited }
    var queries: [String] { lock.lock(); defer { lock.unlock() }; return parameters }
    func response(_ url: URL) throws -> Data? {
        lock.lock(); defer { lock.unlock() }
        visited.append(url.path); parameters.append(url.query ?? "")
        if url.path == "/v2/project/fabric-api" { return try JSONSerialization.data(withJSONObject: ["id": projectID, "slug": "fabric-api", "title": "Fabric API", "project_type": "mod"]) }
        if url.path == "/v2/project/\(FabricAPIComponent.projectID)/version" { return try JSONSerialization.data(withJSONObject: catalogue) }
        return files[url.lastPathComponent]
    }
}
private final class FabricAPIProtocol: URLProtocol, @unchecked Sendable {
    private static let lock = NSLock()
    private static var fixtures: [String: FabricAPIFixture] = [:]
    static func register(_ fixture: FabricAPIFixture) { lock.lock(); fixtures[fixture.id] = fixture; lock.unlock() }
    static func unregister(_ fixture: FabricAPIFixture) { lock.lock(); fixtures[fixture.id] = nil; lock.unlock() }
    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        Self.lock.lock(); let fixture = request.value(forHTTPHeaderField: "X-PCL-Fabric-APIFixture").flatMap { Self.fixtures[$0] }; Self.lock.unlock()
        guard let fixture, let url = request.url, let data = try? fixture.response(url) else { client?.urlProtocol(self, didFailWithError: URLError(.resourceUnavailable)); return }
        let response = HTTPURLResponse(url: url, statusCode: 200, httpVersion: nil, headerFields: ["Content-Length": String(data.count)])!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data); client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() { }
}
private func fabricAPIZIP(_ files: [(String, Data)]) throws -> Data {
    var output = Data(), central = Data()
    func word<T: FixedWidthInteger>(_ value: T) -> Data { withUnsafeBytes(of: value.littleEndian) { Data($0) } }
    for (name, bytes) in files {
        let nameBytes = Data(name.utf8), crc = bytes.withUnsafeBytes { UInt32(zlib.crc32(0, $0.bindMemory(to: Bytef.self).baseAddress, uInt(bytes.count))) }, offset = UInt32(output.count)
        output += word(UInt32(0x04034b50)); output += word(UInt16(20)); output += word(UInt16(0x800)); output += word(UInt16(0))
        output += word(UInt16(0)); output += word(UInt16(0)); output += word(crc); output += word(UInt32(bytes.count)); output += word(UInt32(bytes.count))
        output += word(UInt16(nameBytes.count)); output += word(UInt16(0)); output += nameBytes; output += bytes
        central += word(UInt32(0x02014b50)); central += word(UInt16(20)); central += word(UInt16(20)); central += word(UInt16(0x800)); central += word(UInt16(0))
        central += word(UInt16(0)); central += word(UInt16(0)); central += word(crc); central += word(UInt32(bytes.count)); central += word(UInt32(bytes.count))
        central += word(UInt16(nameBytes.count)); central += word(UInt16(0)); central += word(UInt16(0)); central += word(UInt16(0)); central += word(UInt16(0)); central += word(UInt32(0)); central += word(offset); central += nameBytes
    }
    let start = UInt32(output.count); output += central
    output += word(UInt32(0x06054b50)); output += word(UInt16(0)); output += word(UInt16(0)); output += word(UInt16(files.count)); output += word(UInt16(files.count)); output += word(UInt32(central.count)); output += word(start); output += word(UInt16(0))
    return output
}

private final class FabricAPIMessageRelay: @unchecked Sendable {
    private let lock = NSLock()
    private var values: [String] = []
    var messages: [String] { lock.lock(); defer { lock.unlock() }; return values }
    func append(_ value: String) { lock.lock(); values.append(value); lock.unlock() }
}
