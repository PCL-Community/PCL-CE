import Foundation
import XCTest
import CryptoKit
import zlib
@testable import PCLCore

final class CommunityResourceInstallerTests: XCTestCase {
    private func directory() throws -> URL {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("PCLCommunityInstall-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        return root
    }
    private func profile(_ root: URL, name: String = "Fabric", minecraft: String = "1.20.1", loader: String? = "fabric", isolated: Bool = true) throws -> InstalledVersion {
        let path = root.appendingPathComponent("versions/\(name)")
        try FileManager.default.createDirectory(at: path, withIntermediateDirectories: true)
        var json: [String: Any] = ["id": name, "pclOriginalVersion": minecraft, "mainClass": "example.Main", "libraries": []]
        if let loader { json["pclLoaderKind"] = loader; json["pclLoaderVersion"] = "fixture" }
        try JSONSerialization.data(withJSONObject: json).write(to: path.appendingPathComponent(name + ".json"))
        let value = InstalledVersion(id: name, type: "custom", requiredJava: 17, directory: path)
        var settings = VersionProfileSettings(); settings.isolateGameDirectory = isolated
        try VersionSettingsRepository.save(settings, for: value)
        return value
    }
    private func project(_ id: String = "main", kind: CommunityResourceKind = .mod) -> CommunityProject {
        CommunityProject(projectID: id, source: .modrinth, kind: kind, name: id, websiteURL: URL(string: "https://modrinth.com/mod/" + id)!)
    }
    private func file(_ name: String, bytes: Data, primary: Bool = true, hash: String? = nil) -> CommunityResourceFile {
        CommunityResourceFile(filename: name, size: Int64(bytes.count), urls: [URL(string: "https://community-fixture.invalid/" + name)!],
            sha1: hash ?? MinecraftInstaller.hash(bytes), sha512: SHA512.hash(data: bytes).map { String(format: "%02x", $0) }.joined(), primary: primary)
    }
    private func version(_ id: String = "main-v1", projectID: String = "main", files: [CommunityResourceFile], dependencies: [CommunityDependency] = [], loaders: [String] = ["fabric"], minecraft: [String] = ["1.20.1"]) -> CommunityVersion {
        CommunityVersion(id: id, source: .modrinth, projectID: projectID, name: id, versionNumber: "1.0", gameVersions: minecraft,
            loaders: loaders, files: files, dependencies: dependencies)
    }
    private func setup(_ fixture: CommunityInstallFixture) -> (CommunityResourceInstaller, URLSession) {
        CommunityInstallProtocol.register(fixture)
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [CommunityInstallProtocol.self]
        configuration.httpAdditionalHeaders = ["X-PCL-Community-Fixture": fixture.id]
        let session = URLSession(configuration: configuration)
        addTeardownBlock { session.invalidateAndCancel(); CommunityInstallProtocol.unregister(fixture) }
        return (CommunityResourceInstaller(session: session), session)
    }

    func testPrimaryClientAndRequiredPinnedDependencyInstallIntoIsolatedMods() async throws {
        let root = try directory(), target = try profile(root)
        let main = Data("main client".utf8), dep = Data("required dependency".utf8)
        let fixture = CommunityInstallFixture(files: ["main.jar": main, "dep.jar": dep], versions: ["dep-v1": [
            "id": "dep-v1", "project_id": "dep", "name": "Dependency", "version_number": "1", "version_type": "release",
            "game_versions": ["1.20.1"], "loaders": ["fabric"], "files": [["filename": "dep.jar", "url": "https://cdn.modrinth.com/dep.jar", "size": dep.count, "primary": true,
                "hashes": ["sha1": MinecraftInstaller.hash(dep)]]], "dependencies": []]])
        let (installer, _) = setup(fixture)
        let v = version(files: [file("server.jar", bytes: Data("server".utf8), primary: false), file("main.jar", bytes: main)], dependencies: [
            CommunityDependency(projectID: "dep", versionID: "dep-v1"), CommunityDependency(projectID: "optional", versionID: "optional-v1", required: false)])
        let result = try await installer.install(project: project(), version: v, gameDirectory: root, targetVersion: target, progress: { _ in })
        XCTAssertEqual(result.downloadedFiles, 2); XCTAssertEqual(result.reusedFiles, 0)
        XCTAssertEqual(Set(result.files.map(\.lastPathComponent)), ["dep.jar", "main.jar"])
        XCTAssertEqual(try Data(contentsOf: target.directory.appendingPathComponent("mods/main.jar")), main)
        XCTAssertEqual(try Data(contentsOf: target.directory.appendingPathComponent("mods/dep.jar")), dep)
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("mods").path))
        XCTAssertFalse(fixture.paths.contains { $0.contains("optional") || $0.contains("server.jar") })
    }

    func testUnpinnedDependencyCycleReusesAlreadySelectedRoot() async throws {
        let root = try directory(), target = try profile(root)
        let main = Data("root file".utf8), dep = Data("dependency".utf8)
        let fixture = CommunityInstallFixture(files: ["main.jar": main, "dep.jar": dep], versions: ["dep-v1": [
            "id": "dep-v1", "project_id": "dep", "name": "Dependency", "version_number": "1", "game_versions": ["1.20.1"], "loaders": ["fabric"],
            "files": [["filename": "dep.jar", "url": "https://cdn.modrinth.com/dep.jar", "size": dep.count, "hashes": ["sha1": MinecraftInstaller.hash(dep)]]],
            "dependencies": [["project_id": "main", "dependency_type": "required"]]]])
        let (installer, _) = setup(fixture)
        let result = try await installer.install(project: project(), version: version(files: [file("main.jar", bytes: main)], dependencies: [
            CommunityDependency(projectID: "dep", versionID: "dep-v1")]), gameDirectory: root, targetVersion: target, progress: { _ in })
        XCTAssertEqual(result.files.count, 2)
        XCTAssertEqual(fixture.paths.filter { $0.contains("/project/") }.count, 0, "The dependency's unpinned back-edge must reuse the root already selected")
    }

    func testConflictingPinnedDependencyVersionsFailBeforeFileDownloads() async throws {
        let root = try directory(), target = try profile(root), bytes = Data("dependency".utf8)
        func dep(_ id: String) -> [String: Any] {
            ["id": id, "project_id": "dep", "name": "Dependency", "version_number": id, "game_versions": ["1.20.1"], "loaders": ["fabric"],
             "files": [["filename": id + ".jar", "url": "https://cdn.modrinth.com/" + id + ".jar", "size": bytes.count, "hashes": ["sha1": MinecraftInstaller.hash(bytes)]]], "dependencies": []]
        }
        let fixture = CommunityInstallFixture(versions: ["dep-v1": dep("dep-v1"), "dep-v2": dep("dep-v2")])
        let (installer, _) = setup(fixture)
        do {
            _ = try await installer.install(project: project(), version: version(files: [file("main.jar", bytes: Data("root".utf8))], dependencies: [
                CommunityDependency(projectID: "dep", versionID: "dep-v1"), CommunityDependency(projectID: "dep", versionID: "dep-v2")]),
                gameDirectory: root, targetVersion: target, progress: { _ in })
            XCTFail("Conflicting pinned dependencies cannot be installed together")
        } catch CommunityInstallError.missingDependency { }
        XCTAssertTrue(fixture.paths.allSatisfy { $0.hasPrefix("/v2/version/") })
        XCTAssertFalse(FileManager.default.fileExists(atPath: target.directory.appendingPathComponent("mods").path))
    }

    func testRenamedWholeJarReuseCreatesNoDuplicateOrNetworkTransfer() async throws {
        let root = try directory(), target = try profile(root), bytes = Data("exact existing full file".utf8)
        let mods = target.directory.appendingPathComponent("mods")
        try FileManager.default.createDirectory(at: mods, withIntermediateDirectories: true)
        try bytes.write(to: mods.appendingPathComponent("自定义名称.jar"))
        let fixture = CommunityInstallFixture(); let (installer, _) = setup(fixture)
        let result = try await installer.install(project: project(), version: version(files: [file("canonical.jar", bytes: bytes)]),
            gameDirectory: root, targetVersion: target, progress: { _ in })
        XCTAssertEqual(result.reusedFiles, 1); XCTAssertEqual(result.downloadedFiles, 0)
        XCTAssertEqual(result.files.first?.lastPathComponent, "自定义名称.jar")
        XCTAssertEqual(try FileManager.default.contentsOfDirectory(atPath: mods.path), ["自定义名称.jar"])
        XCTAssertTrue(fixture.paths.isEmpty)
    }

    func testVerifiedCacheReuseAcrossTwoVersionsDoesNotDownloadAgain() async throws {
        let root = try directory(), first = try profile(root), second = try profile(root, name: "Second")
        let bytes = Data("cache artifact".utf8), fixture = CommunityInstallFixture(files: ["main.jar": Data("cache artifact".utf8)])
        let (installer, _) = setup(fixture), v = version(files: [file("main.jar", bytes: bytes)])
        let one = try await installer.install(project: project(), version: v, gameDirectory: root, targetVersion: first, progress: { _ in })
        let two = try await installer.install(project: project(), version: v, gameDirectory: root, targetVersion: second, progress: { _ in })
        XCTAssertEqual(one.downloadedFiles, 1); XCTAssertEqual(two.downloadedFiles, 0); XCTAssertEqual(two.reusedFiles, 1)
        XCTAssertEqual(fixture.paths.filter { $0 == "/main.jar" }.count, 1)
        XCTAssertEqual(try Data(contentsOf: second.directory.appendingPathComponent("mods/main.jar")), bytes)
    }

    func testExistingDifferentFileIsNotOverwritten() async throws {
        let root = try directory(), target = try profile(root), original = Data("user file".utf8)
        let folder = target.directory.appendingPathComponent("mods")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        try original.write(to: folder.appendingPathComponent("main.jar"))
        let fixture = CommunityInstallFixture(); let (installer, _) = setup(fixture)
        do {
            _ = try await installer.install(project: project(), version: version(files: [file("main.jar", bytes: Data("replacement".utf8))]),
                gameDirectory: root, targetVersion: target, progress: { _ in })
            XCTFail("Must retain the user's different file")
        } catch CommunityInstallError.collision { }
        XCTAssertEqual(try Data(contentsOf: folder.appendingPathComponent("main.jar")), original)
        XCTAssertTrue(fixture.paths.isEmpty)
    }

    func testHashFailureCommitsNoRuntimeFilesAndKeepsVerifiedDependencyCache() async throws {
        let root = try directory(), target = try profile(root), dep = Data("good dependency".utf8), expected = Data("good root".utf8)
        let fixture = CommunityInstallFixture(files: ["dep.jar": dep, "main.jar": Data("bad  root".utf8)], versions: ["dep-v1": [
            "id": "dep-v1", "project_id": "dep", "name": "Dependency", "version_number": "1", "version_type": "release", "game_versions": ["1.20.1"], "loaders": ["fabric"],
            "files": [["filename": "dep.jar", "url": "https://cdn.modrinth.com/dep.jar", "size": dep.count, "hashes": ["sha1": MinecraftInstaller.hash(dep)]]], "dependencies": []]])
        let (installer, _) = setup(fixture)
        let v = version(files: [file("main.jar", bytes: expected)], dependencies: [CommunityDependency(projectID: "dep", versionID: "dep-v1")])
        do { _ = try await installer.install(project: project(), version: v, gameDirectory: root, targetVersion: target, progress: { _ in }); XCTFail("Hash mismatch") }
        catch CommunityInstallError.invalidFile { }
        XCTAssertEqual(try FileManager.default.contentsOfDirectory(atPath: target.directory.appendingPathComponent("mods").path), [])
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: root.path).contains { $0.hasPrefix(".pcl-community-") })
        XCTAssertEqual(try FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent("PCL/download-cache/community-files").path).count, 1)
        XCTAssertFalse(FileManager.default.fileExists(atPath: target.directory.appendingPathComponent("PCL/community-resources.json").path))
    }

    func testIncompatibleMinecraftLoaderAndVanillaRejectBeforeDownload() async throws {
        let root = try directory(), fabric = try profile(root), vanilla = try profile(root, name: "Vanilla", loader: nil)
        let fixture = CommunityInstallFixture(); let (installer, _) = setup(fixture)
        let bytes = Data("incompatible".utf8)
        for (v, target) in [(version(files: [file("main.jar", bytes: bytes)], minecraft: ["1.21.1"]), fabric),
                            (version(files: [file("main.jar", bytes: bytes)], loaders: ["forge"]), fabric),
                            (version(files: [file("main.jar", bytes: bytes)], loaders: ["paper"]), fabric),
                            (version(files: [file("main.jar", bytes: bytes)]), vanilla)] {
            do { _ = try await installer.install(project: project(), version: v, gameDirectory: root, targetVersion: target, progress: { _ in }); XCTFail("Incompatible resource") }
            catch CommunityInstallError.incompatible { }
        }
        XCTAssertTrue(fixture.paths.isEmpty)
    }

    func testDatapackUsesSelectedExistingWorldAndNeverCreatesWorld() async throws {
        let root = try directory(), target = try profile(root), world = target.directory.appendingPathComponent("saves/我的世界")
        try FileManager.default.createDirectory(at: world, withIntermediateDirectories: true)
        let level = Data("user level".utf8); try level.write(to: world.appendingPathComponent("level.dat"))
        let bytes = Data("datapack".utf8), fixture = CommunityInstallFixture(files: ["data.zip": Data("datapack".utf8)])
        let (installer, _) = setup(fixture), v = version(files: [file("data.zip", bytes: bytes)], loaders: ["datapack"])
        let result = try await installer.install(project: project(kind: .datapack), version: v, gameDirectory: root, targetVersion: target, worldDirectory: world, progress: { _ in })
        XCTAssertEqual(result.files.first?.lastPathComponent, "data.zip")
        XCTAssertEqual(try Data(contentsOf: world.appendingPathComponent("datapacks/data.zip")), bytes)
        XCTAssertEqual(try Data(contentsOf: world.appendingPathComponent("level.dat")), level)
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("datapacks").path))
    }

    func testDatapackRejectsExternalAndSymlinkWorld() async throws {
        let root = try directory(), target = try profile(root), outside = try directory()
        try Data("level".utf8).write(to: outside.appendingPathComponent("level.dat"))
        let saves = target.directory.appendingPathComponent("saves")
        try FileManager.default.createDirectory(at: saves, withIntermediateDirectories: true)
        let linked = saves.appendingPathComponent("Linked")
        try FileManager.default.createSymbolicLink(at: linked, withDestinationURL: outside)
        let fixture = CommunityInstallFixture(); let (installer, _) = setup(fixture)
        let v = version(files: [file("data.zip", bytes: Data("data".utf8))], loaders: ["datapack"])
        for world in [outside, linked] {
            do { _ = try await installer.install(project: project(kind: .datapack), version: v, gameDirectory: root, targetVersion: target, worldDirectory: world, progress: { _ in }); XCTFail("Outside world") }
            catch CommunityInstallError.unsafePath { }
        }
        XCTAssertFalse(FileManager.default.fileExists(atPath: outside.appendingPathComponent("datapacks").path))
        XCTAssertTrue(fixture.paths.isEmpty)
    }

    func testCancellationStopsDownloadAndCleansStagingBeforeReturning() async throws {
        let root = try directory(), target = try profile(root), fixture = CommunityInstallFixture(holdDownloads: true)
        let (installer, _) = setup(fixture)
        let task = Task {
            try await installer.install(project: project(), version: version(files: [file("main.jar", bytes: Data("held".utf8))]),
                gameDirectory: root, targetVersion: target, progress: { _ in })
        }
        await fulfillment(of: [fixture.downloadStarted], timeout: 3)
        task.cancel()
        do { _ = try await task.value; XCTFail("Cancellation") } catch is CancellationError { }
        XCTAssertTrue(fixture.stopped)
        XCTAssertFalse(FileManager.default.fileExists(atPath: target.directory.appendingPathComponent("mods/main.jar").path))
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: root.path).contains { $0.hasPrefix(".pcl-community-") })
    }

    func testStandaloneDownloadReusesIdenticalFileAndRejectsDifferentDestination() async throws {
        let root = try directory(), bytes = Data("verified destination".utf8), target = root.appendingPathComponent("save.jar")
        try bytes.write(to: target)
        let fixture = CommunityInstallFixture(); let (installer, _) = setup(fixture)
        let v = version(files: [file("main.jar", bytes: bytes)])
        let reused = try await installer.download(version: v, to: target, progress: { _ in })
        XCTAssertEqual(reused.lastPathComponent, target.lastPathComponent)
        XCTAssertEqual(try Data(contentsOf: reused), bytes)
        try Data("different".utf8).write(to: target)
        do { _ = try await installer.download(version: v, to: target, progress: { _ in }); XCTFail("Retain different file") }
        catch CommunityInstallError.collision { }
        XCTAssertEqual(try Data(contentsOf: target), Data("different".utf8))
        XCTAssertTrue(fixture.paths.isEmpty)
    }

    func testDuplicateFabricModIDIsRejectedWithoutChangingExistingJar() async throws {
        let root = try directory(), target = try profile(root)
        let old = try communityZIP([("fabric.mod.json", Data(#"{"schemaVersion":1,"id":"same_mod","version":"1"}"#.utf8))])
        let new = try communityZIP([("fabric.mod.json", Data(#"{"schemaVersion":1,"id":"same_mod","version":"2"}"#.utf8))])
        let mods = target.directory.appendingPathComponent("mods")
        try FileManager.default.createDirectory(at: mods, withIntermediateDirectories: true)
        try old.write(to: mods.appendingPathComponent("old.jar"))
        let fixture = CommunityInstallFixture(files: ["new.jar": new]); let (installer, _) = setup(fixture)
        do {
            _ = try await installer.install(project: project(), version: version(files: [file("new.jar", bytes: new)]), gameDirectory: root, targetVersion: target, progress: { _ in })
            XCTFail("Cannot add another version of same Mod ID")
        } catch CommunityInstallError.collision { }
        XCTAssertEqual(try Data(contentsOf: mods.appendingPathComponent("old.jar")), old)
        XCTAssertFalse(FileManager.default.fileExists(atPath: mods.appendingPathComponent("new.jar").path))
    }

    func testInvalidFilenameAndSymlinkRuntimeAreRejected() async throws {
        let root = try directory(), target = try profile(root), outside = try directory(), fixture = CommunityInstallFixture()
        let (installer, _) = setup(fixture), bytes = Data("safe".utf8)
        do {
            _ = try await installer.install(project: project(), version: version(files: [file("../evil.jar", bytes: bytes)]), gameDirectory: root, targetVersion: target, progress: { _ in })
            XCTFail("No traversal")
        } catch { }
        try FileManager.default.createSymbolicLink(at: target.directory.appendingPathComponent("mods"), withDestinationURL: outside)
        do {
            _ = try await installer.install(project: project(), version: version(files: [file("main.jar", bytes: bytes)]), gameDirectory: root, targetVersion: target, progress: { _ in })
            XCTFail("No symlink runtime")
        } catch CommunityInstallError.unsafePath { }
        XCTAssertTrue(fixture.paths.isEmpty)
        XCTAssertEqual(try FileManager.default.contentsOfDirectory(atPath: outside.path), [])
    }
}

private final class CommunityInstallFixture: @unchecked Sendable {
    let id = UUID().uuidString
    let files: [String: Data]
    let versions: [String: [String: Any]]
    let holdDownloads: Bool
    let downloadStarted = XCTestExpectation(description: "resource network download started")
    private let lock = NSLock()
    private var requests: [String] = []
    private var didStop = false
    init(files: [String: Data] = [:], versions: [String: [String: Any]] = [:], holdDownloads: Bool = false) {
        self.files = files; self.versions = versions; self.holdDownloads = holdDownloads
    }
    var paths: [String] { lock.lock(); defer { lock.unlock() }; return requests }
    var stopped: Bool { lock.lock(); defer { lock.unlock() }; return didStop }
    func started(_ path: String) { lock.lock(); requests.append(path); lock.unlock() }
    func stop() { lock.lock(); didStop = true; lock.unlock() }
}
private final class CommunityInstallProtocol: URLProtocol, @unchecked Sendable {
    private static let lock = NSLock()
    private static var fixtures: [String: CommunityInstallFixture] = [:]
    static func register(_ fixture: CommunityInstallFixture) { lock.lock(); fixtures[fixture.id] = fixture; lock.unlock() }
    static func unregister(_ fixture: CommunityInstallFixture) { lock.lock(); fixtures[fixture.id] = nil; lock.unlock() }
    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        Self.lock.lock(); let fixture = request.value(forHTTPHeaderField: "X-PCL-Community-Fixture").flatMap { Self.fixtures[$0] }; Self.lock.unlock()
        guard let fixture, let url = request.url else { client?.urlProtocol(self, didFailWithError: URLError(.badURL)); return }
        fixture.started(url.path)
        let isVersion = url.host == "api.modrinth.com" && url.path.hasPrefix("/v2/version/")
        if fixture.holdDownloads && !isVersion { fixture.downloadStarted.fulfill(); return }
        let data: Data?
        if isVersion { data = fixture.versions[url.lastPathComponent].flatMap { try? JSONSerialization.data(withJSONObject: $0) } }
        else { data = fixture.files[url.lastPathComponent] }
        guard let data else {
            let response = HTTPURLResponse(url: url, statusCode: 404, httpVersion: nil, headerFields: nil)!
            client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed); client?.urlProtocolDidFinishLoading(self); return
        }
        let response = HTTPURLResponse(url: url, statusCode: 200, httpVersion: nil, headerFields: ["Content-Length": String(data.count)])!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data); client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() {
        Self.lock.lock(); let fixture = request.value(forHTTPHeaderField: "X-PCL-Community-Fixture").flatMap { Self.fixtures[$0] }; Self.lock.unlock()
        fixture?.stop()
    }
}

private func communityZIP(_ files: [(String, Data)]) throws -> Data {
    var result = Data(), central = Data()
    func append16(_ value: Int, to output: inout Data) { var raw = UInt16(value).littleEndian; withUnsafeBytes(of: &raw) { output.append(contentsOf: $0) } }
    func append32(_ value: UInt32, to output: inout Data) { var raw = value.littleEndian; withUnsafeBytes(of: &raw) { output.append(contentsOf: $0) } }
    for (name, bytes) in files {
        let encoded = Data(name.utf8), offset = result.count
        let crc = bytes.withUnsafeBytes { UInt32(zlib.crc32(0, $0.bindMemory(to: Bytef.self).baseAddress, uInt(bytes.count))) }
        append32(0x04034b50, to: &result)
        for value in [20, 0x800, 0, 0, 0] { append16(value, to: &result) }
        for value in [crc, UInt32(bytes.count), UInt32(bytes.count)] { append32(value, to: &result) }
        append16(encoded.count, to: &result); append16(0, to: &result); result.append(encoded); result.append(bytes)
        append32(0x02014b50, to: &central)
        for value in [20, 20, 0x800, 0, 0, 0] { append16(value, to: &central) }
        for value in [crc, UInt32(bytes.count), UInt32(bytes.count)] { append32(value, to: &central) }
        for value in [encoded.count, 0, 0, 0, 0] { append16(value, to: &central) }
        append32(0, to: &central); append32(UInt32(offset), to: &central); central.append(encoded)
    }
    let centralOffset = result.count; result.append(central); append32(0x06054b50, to: &result)
    for value in [0, 0, files.count, files.count] { append16(value, to: &result) }
    append32(UInt32(central.count), to: &result); append32(UInt32(centralOffset), to: &result); append16(0, to: &result)
    return result
}
