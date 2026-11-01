import XCTest
import Foundation
import Darwin
@testable import PCLCore

final class InstallerTests: XCTestCase {
    private func temporaryDirectory() throws -> URL {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent("PCLInstallerTests-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: directory) }
        return directory
    }

    func testRuleOrderAndArchitectureFiltering() {
        XCTAssertTrue(InstallPlatform.libraryAllowed(["rules": [["action": "allow", "os": ["name": "osx"]]]]))
        XCTAssertFalse(InstallPlatform.libraryAllowed(["rules": [["action": "allow", "os": ["name": "windows"]]]]))
        XCTAssertFalse(InstallPlatform.rulesAllowed([
            ["action": "allow"], ["action": "disallow", "os": ["name": "osx"]]
        ]))
        XCTAssertFalse(InstallPlatform.rulesAllowed([["action": "allow", "features": ["is_demo_user": true]]]))
        XCTAssertTrue(InstallPlatform.rulesAllowed([["action": "allow", "features": ["has_custom_resolution": true]]], features: ["has_custom_resolution": true]))
        let intel = ["name": "org.lwjgl:lwjgl:3.3.3:natives-macos"]
        let arm = ["name": "org.lwjgl:lwjgl:3.3.3:natives-macos-arm64"]
        XCTAssertEqual(InstallPlatform.libraryAllowed(arm), InstallPlatform.architecture == "arm64")
        XCTAssertEqual(InstallPlatform.libraryAllowed(intel), InstallPlatform.architecture != "arm64")
    }

    func testDestinationRejectsTraversalAndExistingEscapingSymlink() throws {
        let root = try temporaryDirectory()
        for relative in ["../escape.jar", "/tmp/escape.jar", "a/../../escape", "a\\escape", "a//b", "a/./b"] {
            XCTAssertThrowsError(try MinecraftInstaller.safeDestination(root: root, relative: relative))
        }
        let outside = try temporaryDirectory()
        try FileManager.default.createSymbolicLink(at: root.appendingPathComponent("linked"), withDestinationURL: outside)
        XCTAssertThrowsError(try MinecraftInstaller.safeDestination(root: root, relative: "linked/escape.jar"))
        let canonicalRoot = try XCTUnwrap(realpath(root.path, nil))
        defer { free(canonicalRoot) }
        XCTAssertEqual(try MinecraftInstaller.safeDestination(root: root, relative: "assets/objects/ab/hash").path,
                       String(cString: canonicalRoot) + "/assets/objects/ab/hash")
        XCTAssertNoThrow(try MinecraftInstaller.validateComponent("1.14 Pre-Release 1"))
        XCTAssertThrowsError(try MinecraftInstaller.validateComponent("../1.14"))
    }

    func testCachedArtifactVerifiesContentAndSize() throws {
        let root = try temporaryDirectory()
        let file = root.appendingPathComponent("example.jar")
        let data = Data("verified-client".utf8)
        try data.write(to: file)
        let expected = MinecraftInstaller.hash(data)
        XCTAssertTrue(try MinecraftInstaller.matches(file, sha1: expected, size: Int64(data.count)))
        XCTAssertFalse(try MinecraftInstaller.matches(file, sha1: expected, size: Int64(data.count + 1)))
        XCTAssertFalse(try MinecraftInstaller.matches(file, sha1: String(repeating: "0", count: 40), size: Int64(data.count)))
        try Data("corrupt-client!".utf8).write(to: file)
        XCTAssertFalse(try MinecraftInstaller.matches(file, sha1: expected, size: nil))
    }

    func testManifestDecodesRemoteDigestAndSnapshotDate() throws {
        let data = Data(#"{"latest":{"release":"1.20.4","snapshot":"24w01a"},"versions":[{"id":"1.20.4","type":"release","url":"https://example.invalid/1.20.4.json","releaseTime":"2023-12-07T12:00:00+00:00","sha1":"0123456789012345678901234567890123456789"}]}"#.utf8)
        let manifest = try JSONDecoder().decode(VersionManifest.self, from: data)
        XCTAssertEqual(manifest.latest.release, "1.20.4")
        XCTAssertEqual(manifest.versions.first?.sha1, "0123456789012345678901234567890123456789")
        XCTAssertEqual(manifest.versions.first?.releaseTime, "2023-12-07T12:00:00+00:00")
    }

    func testInvalidArtifactMetadataRejectedBeforeDownload() throws {
        let target = try temporaryDirectory().appendingPathComponent("unsafe.jar")
        let validHash = String(repeating: "a", count: 40)
        XCTAssertThrowsError(try MinecraftInstaller.artifact(["url": "http://example.invalid/file", "sha1": validHash], destination: target))
        XCTAssertThrowsError(try MinecraftInstaller.artifact(["url": "https://example.invalid/file", "sha1": "abc"], destination: target))
        XCTAssertThrowsError(try MinecraftInstaller.artifact(["url": "https://example.invalid/file", "sha1": validHash, "size": -1], destination: target))
    }

    func testManifestHTTPFailureAndUncommittedInvalidInstall() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [InstallerFixtureProtocol.self]
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }
        let failed = MinecraftInstaller(session: session, manifestURL: URL(string: "https://pcl-fixture.invalid/failure")!)
        do {
            _ = try await failed.fetchManifest()
            XCTFail("HTTP errors must not decode as a successful manifest")
        } catch MinecraftInstallError.http(let code, _) { XCTAssertEqual(code, 503) }
        let root = try temporaryDirectory()
        let version = RemoteVersion(id: "1.20.4", type: "release", url: URL(string: "https://pcl-fixture.invalid/invalid-version")!)
        let installer = MinecraftInstaller(session: session)
        do {
            try await installer.install(version: version, gameDirectory: root) { _ in }
            XCTFail("Incomplete metadata must not create an installed version")
        } catch MinecraftInstallError.invalidMetadata { }
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/1.20.4/1.20.4.json").path))
    }

    func testCompleteCachedInstallCommitsProfileAndCancellationDoesNot() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [InstallerFixtureProtocol.self]
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }
        let root = try temporaryDirectory()
        let profileDirectory = root.appendingPathComponent("versions/1.20.4")
        let indexDirectory = root.appendingPathComponent("assets/indexes")
        try FileManager.default.createDirectory(at: profileDirectory, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: indexDirectory, withIntermediateDirectories: true)
        try Data("client".utf8).write(to: profileDirectory.appendingPathComponent("1.20.4.jar"))
        try Data(#"{"objects":{}}"#.utf8).write(to: indexDirectory.appendingPathComponent("fixture.json"))
        let installer = MinecraftInstaller(session: session)
        let version = RemoteVersion(id: "1.20.4", type: "release", url: URL(string: "https://pcl-fixture.invalid/valid-version")!)
        let monitor = InstallTaskMonitor(title: "cached base", stages: [
            .init(id: "vanillaJson", title: "json"), .init(id: "vanillaLibraries", title: "libraries"),
            .init(id: "vanillaAssets", title: "assets")
        ], progress: { _ in })
        try await InstallProgressContext.$current.withValue(monitor) {
            try await installer.install(version: version, gameDirectory: root) { _ in }
        }
        XCTAssertTrue(monitor.snapshot.stages.allSatisfy { $0.state == .completed })
        XCTAssertEqual(monitor.snapshot.remainingFiles, 0)
        XCTAssertEqual(monitor.snapshot.downloadedBytes, 0, "Cached files must not invent network traffic")
        XCTAssertEqual(monitor.snapshot.bytesPerSecond, 0)
        let committed = profileDirectory.appendingPathComponent("1.20.4.json")
        let metadata = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: committed)) as? [String: Any])
        XCTAssertEqual(metadata["id"] as? String, "1.20.4")
        try FileManager.default.removeItem(at: committed)
        let cancelled = Task {
            try await installer.install(version: version, gameDirectory: root) { _ in
                withUnsafeCurrentTask { $0?.cancel() }
            }
        }
        do { try await cancelled.value; XCTFail("Cancelled install must fail before committing the profile") }
        catch { XCTAssertTrue(cancelled.isCancelled) }
        XCTAssertFalse(FileManager.default.fileExists(atPath: committed.path))
    }

    func testCustomUnicodeProfileKeepsOriginalSourceAndCanBeRepaired() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [InstallerFixtureProtocol.self]
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }
        let root = try temporaryDirectory()
        let profileID = "我的原版 1.20.4"
        let profile = root.appendingPathComponent("versions/\(profileID)")
        let index = root.appendingPathComponent("assets/indexes/fixture.json")
        try FileManager.default.createDirectory(at: profile, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: index.deletingLastPathComponent(), withIntermediateDirectories: true)
        try Data("client".utf8).write(to: profile.appendingPathComponent("\(profileID).jar"))
        try Data(#"{"objects":{}}"#.utf8).write(to: index)
        let version = RemoteVersion(id: "1.20.4", type: "release", url: URL(string: "https://pcl-fixture.invalid/valid-version")!)
        let installer = MinecraftInstaller(session: session)
        try await installer.install(version: version, gameDirectory: root, profileID: profileID) { _ in }
        let data = try Data(contentsOf: profile.appendingPathComponent("\(profileID).json"))
        let metadata = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertEqual(metadata["id"] as? String, profileID)
        XCTAssertEqual(metadata["pclOriginalVersion"] as? String, "1.20.4")
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/1.20.4").path))
        XCTAssertEqual(try LocalVersionScanner.scan(gameDirectory: root).first?.id, profileID)
        try await installer.install(version: version, gameDirectory: root, profileID: profileID) { _ in }
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("\(profileID).jar")), Data("client".utf8))
    }

    func testProfileNameValidationAndExistingSourceCollision() async throws {
        XCTAssertNoThrow(try MinecraftInstaller.validateProfileID("我的游戏 1.20.4"))
        for name in ["", ".", "..", ".hidden", "../escape", "with/child", "a\\b", "a:b", "a?b", "a!b", "a;b", "name.", "name ", "\nname", String(repeating: "中", count: 81)] {
            XCTAssertThrowsError(try MinecraftInstaller.validateProfileID(name), name)
        }
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [InstallerFixtureProtocol.self]
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }
        let root = try temporaryDirectory()
        let profile = root.appendingPathComponent("versions/Custom")
        try FileManager.default.createDirectory(at: profile, withIntermediateDirectories: true)
        let old = Data(#"{"id":"Custom","pclOriginalVersion":"1.19.4","mainClass":"example.Main"}"#.utf8)
        let file = profile.appendingPathComponent("Custom.json")
        try old.write(to: file)
        let version = RemoteVersion(id: "1.20.4", type: "release", url: URL(string: "https://pcl-fixture.invalid/valid-version")!)
        do {
            try await MinecraftInstaller(session: session).install(version: version, gameDirectory: root, profileID: "Custom") { _ in }
            XCTFail("Existing unrelated profiles must not be overwritten")
        } catch MinecraftInstallError.invalidMetadata { }
        XCTAssertEqual(try Data(contentsOf: file), old)
    }
}

private final class InstallerFixtureProtocol: URLProtocol {
    override class func canInit(with request: URLRequest) -> Bool { request.url?.host == "pcl-fixture.invalid" }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        guard let url = request.url else { return }
        let status = url.lastPathComponent == "failure" ? 503 : 200
        let response = HTTPURLResponse(url: url, statusCode: status, httpVersion: "HTTP/1.1", headerFields: ["Content-Type": "application/json"])!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        let body: Data
        if url.lastPathComponent == "valid-version" {
            let clientHash = MinecraftInstaller.hash(Data("client".utf8))
            let indexData = Data(#"{"objects":{}}"#.utf8)
            let metadata: [String: Any] = [
                "id": "1.20.4", "type": "release", "mainClass": "net.minecraft.client.main.Main", "libraries": [],
                "downloads": ["client": ["url": "https://pcl-fixture.invalid/client.jar", "sha1": clientHash, "size": 6]],
                "assetIndex": ["id": "fixture", "url": "https://pcl-fixture.invalid/fixture.json", "sha1": MinecraftInstaller.hash(indexData), "size": indexData.count]
            ]
            body = try! JSONSerialization.data(withJSONObject: metadata)
        } else {
            body = Data(#"{"id":"1.20.4","mainClass":"net.minecraft.client.main.Main"}"#.utf8)
        }
        client?.urlProtocol(self, didLoad: body)
        client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() { }
}
