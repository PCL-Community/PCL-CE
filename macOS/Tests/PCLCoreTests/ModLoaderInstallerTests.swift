import XCTest
import Foundation
@testable import PCLCore

final class ModLoaderInstallerTests: XCTestCase {
    private func directory() throws -> URL {
        let value = FileManager.default.temporaryDirectory.appendingPathComponent("PCLLoaderTests-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: value, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: value) }
        return value
    }

    private func fixtureSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [LoaderFixtureProtocol.self]
        let session = URLSession(configuration: configuration)
        addTeardownBlock { session.invalidateAndCancel() }
        return session
    }

    private func cachedVanilla(_ root: URL) throws {
        let jar = root.appendingPathComponent("versions/1.20.4/1.20.4.jar")
        let index = root.appendingPathComponent("assets/indexes/loader-fixture.json")
        let library = root.appendingPathComponent("libraries/example/loader/0.16.10/loader-0.16.10.jar")
        for file in [jar, index, library] { try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true) }
        try Data("client".utf8).write(to: jar)
        try Data(#"{"objects":{}}"#.utf8).write(to: index)
        try Data("verified-loader".utf8).write(to: library)
    }

    private var base: RemoteVersion { RemoteVersion(id: "1.20.4", type: "release", url: URL(string: "https://pcl-loader-fixture.invalid/base")!) }
    private var loader: ModLoaderVersion { ModLoaderVersion(kind: .fabric, minecraftVersion: "1.20.4", version: "0.16.10") }

    func testOfficialFabricAndQuiltCatalogsSortDeduplicateAndRetainStability() async throws {
        let installer = ModLoaderInstaller(session: fixtureSession())
        for kind in [ModLoaderKind.fabric, .quilt] {
            let values = try await installer.fetchVersions(kind: kind, minecraftVersion: "1.20.4")
            XCTAssertEqual(values.map(\.version), ["0.17.0-beta.1", "0.16.10", "0.16.9"])
            XCTAssertFalse(try XCTUnwrap(values.first).stable)
            XCTAssertTrue(values[1].stable)
            XCTAssertEqual(values[1].kind, kind)
            XCTAssertEqual(values[1].minecraftVersion, "1.20.4")
        }
    }

    func testForgeCatalogFallbackRequiresInstallerDigest() async throws {
        let values = try await ModLoaderInstaller(session: fixtureSession()).fetchVersions(kind: .forge, minecraftVersion: "1.20.4")
        XCTAssertEqual(values.map(\.version), ["49.2.13", "49.1.0"])
        XCTAssertTrue(values.allSatisfy { $0.kind == .forge })
    }

    func testNeoForgeCatalogFiltersMinecraftSeriesIncludingLegacyCoordinate() async throws {
        let installer = ModLoaderInstaller(session: fixtureSession())
        let current = try await installer.fetchVersions(kind: .neoForge, minecraftVersion: "1.20.4")
        XCTAssertEqual(current.map(\.version), ["20.4.251", "20.4.10-beta"])
        XCTAssertFalse(current[1].stable)
        let legacy = try await installer.fetchVersions(kind: .neoForge, minecraftVersion: "1.20.1")
        XCTAssertEqual(legacy.map(\.version), ["47.1.106"])
        XCTAssertEqual(ModLoaderInstaller.neoForgePrefix("1.21"), "21.0.")
        XCTAssertEqual(ModLoaderInstaller.neoForgePrefix("26.3"), "26.3.")
        XCTAssertNil(ModLoaderInstaller.neoForgePrefix("snapshot"))
    }

    func testInstallerCoordinatesAndMirrorPreserveArtifactIdentity() throws {
        let forge = try ModLoaderInstaller.installerDescriptor(.init(kind: .forge, minecraftVersion: "1.20.4", version: "1.20.4-49.2.13"))
        XCTAssertEqual(forge.coordinate, "1.20.4-49.2.13")
        XCTAssertEqual(forge.file, "forge-1.20.4-49.2.13-installer.jar")
        XCTAssertEqual(ModLoaderInstaller.mirrorURL(forge.url)?.path, "/maven/net/minecraftforge/forge/1.20.4-49.2.13/forge-1.20.4-49.2.13-installer.jar")
        let neo = try ModLoaderInstaller.installerDescriptor(.init(kind: .neoForge, minecraftVersion: "1.20.4", version: "20.4.251"))
        XCTAssertEqual(neo.url.host, "maven.neoforged.net")
        XCTAssertEqual(neo.file, "neoforge-20.4.251-installer.jar")
        let legacy = try ModLoaderInstaller.installerDescriptor(.init(kind: .neoForge, minecraftVersion: "1.20.1", version: "47.1.106"))
        XCTAssertEqual(legacy.file, "forge-1.20.1-47.1.106-installer.jar")
        XCTAssertNil(ModLoaderInstaller.mirrorURL(URL(string: "https://example.invalid/forge.jar")!))
        XCTAssertThrowsError(try ModLoaderInstaller.installerDescriptor(.init(kind: .forge, minecraftVersion: "1.20.4", version: "../unsafe")))
        XCTAssertThrowsError(try ModLoaderInstaller.installerDescriptor(loader))
        XCTAssertThrowsError(try ModLoaderInstaller.mavenVersions(Data("<metadata><broken>".utf8)))
    }

    func testCompleteCachedFabricInstallPublishesIndependentInheritedProfileAndPreservesSettingsOnRepair() async throws {
        let root = try directory()
        try cachedVanilla(root)
        let installer = ModLoaderInstaller(session: fixtureSession())
        let name = "我的 Fabric 1.20.4"
        let monitor = InstallTaskMonitor(title: "cached Fabric", stages: [
            .init(id: "vanillaJson", title: "json"), .init(id: "vanillaLibraries", title: "libraries"),
            .init(id: "vanillaAssets", title: "assets"), .init(id: "loaderMetadata", title: "loader metadata"),
            .init(id: "loaderFiles", title: "loader files"), .init(id: "install", title: "install")
        ], progress: { _ in })
        try await InstallProgressContext.$current.withValue(monitor) {
            try await installer.install(version: loader, minecraftVersion: base, gameDirectory: root, profileID: name,
                javaExecutable: URL(fileURLWithPath: "/usr/bin/false")) { _ in }
        }
        XCTAssertTrue(monitor.snapshot.stages.allSatisfy { $0.state == .completed })
        XCTAssertEqual(monitor.snapshot.remainingFiles, 0)
        XCTAssertEqual(monitor.snapshot.downloadedBytes, 0)
        XCTAssertEqual(monitor.snapshot.bytesPerSecond, 0)
        let profile = try MinecraftVersionMetadata.load(versionID: name, gameDirectory: root)
        XCTAssertEqual(profile["id"] as? String, name)
        XCTAssertEqual(profile["inheritsFrom"] as? String, "1.20.4")
        XCTAssertEqual(profile["jar"] as? String, "1.20.4")
        XCTAssertEqual(profile["pclOriginalVersion"] as? String, "1.20.4")
        XCTAssertEqual(profile["pclLoaderKind"] as? String, "fabric")
        XCTAssertEqual(profile["pclLoaderVersion"] as? String, "0.16.10")
        XCTAssertEqual((profile["pclLoader"] as? [String: String])?["kind"], "fabric")
        let local = InstalledVersion(id: name, type: "release", requiredJava: 17, directory: root.appendingPathComponent("versions/\(name)"), inheritsFrom: "1.20.4")
        var settings = try VersionSettingsRepository.load(version: local)
        XCTAssertTrue(settings.isolateGameDirectory)
        XCTAssertEqual(settings.category, "modded")
        XCTAssertEqual(settings.iconName, "Fabric")
        XCTAssertTrue(settings.displayDescription.contains("0.16.10"))
        settings.memoryMB = 6144
        settings.displayDescription = "我自己的说明"
        try VersionSettingsRepository.save(settings, for: local)
        let mods = local.directory.appendingPathComponent("mods")
        try FileManager.default.createDirectory(at: mods, withIntermediateDirectories: true)
        let userMod = mods.appendingPathComponent("my-mod.jar")
        try Data("my-mod".utf8).write(to: userMod)
        var importedProfile = profile
        importedProfile["pclModpackName"] = "My Imported Pack"
        importedProfile.removeValue(forKey: "pclLoader") // Flat provenance is sufficient for safe repair.
        let profileFile = local.directory.appendingPathComponent(name + ".json")
        try JSONSerialization.data(withJSONObject: importedProfile).write(to: profileFile, options: .atomic)
        try await installer.install(version: loader, minecraftVersion: base, gameDirectory: root, profileID: name, javaExecutable: URL(fileURLWithPath: "/usr/bin/false")) { _ in }
        XCTAssertEqual(try VersionSettingsRepository.load(version: local), settings)
        XCTAssertEqual(try MinecraftVersionMetadata.load(versionID: name, gameDirectory: root)["pclModpackName"] as? String, "My Imported Pack")
        XCTAssertEqual(try Data(contentsOf: userMod), Data("my-mod".utf8))
        XCTAssertEqual(try Data(contentsOf: root.appendingPathComponent("versions/1.20.4/1.20.4.jar")), Data("client".utf8))
    }

    func testLoaderMismatchAndUnrelatedProfileRejectWithoutOverwrite() async throws {
        let root = try directory()
        let installer = ModLoaderInstaller(session: fixtureSession())
        do {
            try await installer.install(kind: .quilt, version: loader, minecraftVersion: base, gameDirectory: root, profileID: "Mismatch", javaExecutable: URL(fileURLWithPath: "/usr/bin/false")) { _ in }
            XCTFail("Kind mismatch must reject before writing")
        } catch MinecraftInstallError.invalidMetadata { }
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions").path))
        let existing = root.appendingPathComponent("versions/Existing/Existing.json")
        try FileManager.default.createDirectory(at: existing.deletingLastPathComponent(), withIntermediateDirectories: true)
        let original = Data(#"{"id":"Existing","mainClass":"my.Main","pclOriginalVersion":"1.20.4"}"#.utf8)
        try original.write(to: existing)
        do {
            try await installer.install(version: loader, minecraftVersion: base, gameDirectory: root, profileID: "Existing", javaExecutable: URL(fileURLWithPath: "/usr/bin/false")) { _ in }
            XCTFail("Unrelated existing profiles must be preserved")
        } catch MinecraftInstallError.invalidMetadata { }
        XCTAssertEqual(try Data(contentsOf: existing), original)
    }

    func testInvalidLoaderProfileAndCancellationDoNotPublishVersion() async throws {
        let root = try directory()
        try cachedVanilla(root)
        let installer = ModLoaderInstaller(session: fixtureSession())
        let bad = ModLoaderVersion(kind: .fabric, minecraftVersion: "1.20.4", version: "0.16.9")
        do {
            try await installer.install(version: bad, minecraftVersion: base, gameDirectory: root, profileID: "Broken", javaExecutable: URL(fileURLWithPath: "/usr/bin/false")) { _ in }
            XCTFail("Incorrect inheritsFrom must not publish a launcher profile")
        } catch MinecraftInstallError.invalidMetadata { }
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/Broken/Broken.json").path))
        let task = Task {
            try await installer.install(version: loader, minecraftVersion: base, gameDirectory: root, profileID: "Cancelled", javaExecutable: URL(fileURLWithPath: "/usr/bin/false")) { _ in
                withUnsafeCurrentTask { $0?.cancel() }
            }
        }
        do { try await task.value; XCTFail("Cancelled loader install must fail") } catch { XCTAssertTrue(task.isCancelled) }
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/Cancelled/Cancelled.json").path))
    }

    func testUnsafeRemoteLibraryPathDoesNotPublishOrEscapeCache() async throws {
        let root = try directory()
        try cachedVanilla(root)
        let unsafe = ModLoaderVersion(kind: .fabric, minecraftVersion: "1.20.4", version: "0.16.8")
        do {
            try await ModLoaderInstaller(session: fixtureSession()).install(version: unsafe, minecraftVersion: base, gameDirectory: root, profileID: "Unsafe", javaExecutable: URL(fileURLWithPath: "/usr/bin/false")) { _ in }
            XCTFail("Remote artifact paths must remain under the library directory")
        } catch MinecraftInstallError.unsafePath { }
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/Unsafe/Unsafe.json").path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("escaped.jar").path))
    }
}

private final class LoaderFixtureProtocol: URLProtocol {
    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        guard let url = request.url else { return }
        let body: Data
        var status = 200
        switch url.host {
        case "meta.fabricmc.net", "meta.quiltmc.org":
            if url.path.hasSuffix("/profile/json") {
                let unsafe = url.path.contains("/0.16.8/")
                let valid = url.path.contains("/0.16.10/") || unsafe
                let bytes = Data("verified-loader".utf8)
                body = try! JSONSerialization.data(withJSONObject: [
                    "id": "fabric-loader-0.16.10-1.20.4", "inheritsFrom": valid ? "1.20.4" : "1.19.4", "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
                    "libraries": [["name": "example:loader:0.16.10", "downloads": ["artifact": ["path": unsafe ? "../escaped.jar" : "example/loader/0.16.10/loader-0.16.10.jar", "url": "https://pcl-loader-fixture.invalid/library.jar", "sha1": MinecraftInstaller.hash(bytes), "size": bytes.count]]]]
                ])
            } else {
                body = Data(#"[{"loader":{"version":"0.16.9","stable":true}},{"loader":{"version":"0.17.0-beta.1","stable":false}},{"loader":{"version":"0.16.10","stable":true}},{"loader":{"version":"0.16.10","stable":true}}]"#.utf8)
            }
        case "maven.minecraftforge.net": status = 403; body = Data()
        case "bmclapi2.bangbang93.com":
            if url.path.hasPrefix("/forge/minecraft/") {
                let hash = String(repeating: "a", count: 40)
                body = try! JSONSerialization.data(withJSONObject: [
                    ["version": "49.1.0", "files": [["category": "installer", "format": "jar", "hash": hash]]],
                    ["version": "49.2.13", "files": [["category": "installer", "format": "jar", "hash": hash]]],
                    ["version": "49.9.0", "files": [["category": "installer", "format": "jar", "hash": "invalid"]]],
                    ["version": "49.9.1", "files": [["category": "universal", "format": "jar", "hash": hash]]]
                ])
            } else { status = 503; body = Data() }
        case "maven.neoforged.net":
            body = Data("<metadata><versioning><versions><version>20.3.12</version><version>20.4.10-beta</version><version>20.4.251</version><version>21.0.1</version><version>1.20.1-47.1.106</version></versions></versioning></metadata>".utf8)
        case "pcl-loader-fixture.invalid":
            let index = Data(#"{"objects":{}}"#.utf8)
            body = try! JSONSerialization.data(withJSONObject: [
                "id": "1.20.4", "type": "release", "mainClass": "net.minecraft.client.main.Main", "libraries": [],
                "downloads": ["client": ["url": "https://pcl-loader-fixture.invalid/client.jar", "sha1": MinecraftInstaller.hash(Data("client".utf8)), "size": 6]],
                "assetIndex": ["id": "loader-fixture", "url": "https://pcl-loader-fixture.invalid/index.json", "sha1": MinecraftInstaller.hash(index), "size": index.count]
            ])
        default: status = 404; body = Data()
        }
        client?.urlProtocol(self, didReceive: HTTPURLResponse(url: url, statusCode: status, httpVersion: "HTTP/1.1", headerFields: nil)!, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: body)
        client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() { }
}
