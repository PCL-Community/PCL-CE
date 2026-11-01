import XCTest
import Foundation
@testable import PCLCore

final class ModpackImporterTests: XCTestCase {
    private func temporaryDirectory() throws -> URL {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("PCLModpackTests-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        return root
    }
    private func archive(_ members: [PackMember], root: URL? = nil) throws -> URL {
        let file = try (root ?? temporaryDirectory()).appendingPathComponent("fixture-\(UUID().uuidString).zip")
        try packZIP(members).write(to: file)
        return file
    }
    private func json(_ value: [String: Any]) throws -> Data { try JSONSerialization.data(withJSONObject: value, options: [.sortedKeys]) }
    private func modrinth(files: [[String: Any]] = [], dependencies: [String: String] = ["minecraft": "1.20.4"]) throws -> Data {
        try json(["formatVersion": 1, "game": "minecraft", "name": "测试整合包", "versionId": "1", "dependencies": dependencies, "files": files])
    }
    private func fixtureSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [ModpackFixtureProtocol.self]
        return URLSession(configuration: configuration)
    }
    private func fixtureImporter(session: URLSession) -> ModpackImporter {
        ModpackImporter(session: session, installer: MinecraftInstaller(session: session, manifestURL: URL(string: "https://pcl-pack.invalid/manifest")!))
    }
    private func seedVanilla(_ root: URL) throws {
        let profile = root.appendingPathComponent("versions/1.20.4")
        let index = root.appendingPathComponent("assets/indexes/fixture.json")
        try FileManager.default.createDirectory(at: profile, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: index.deletingLastPathComponent(), withIntermediateDirectories: true)
        try ModpackFixtureProtocol.clientData.write(to: profile.appendingPathComponent("1.20.4.jar"))
        try ModpackFixtureProtocol.metadataData.write(to: profile.appendingPathComponent("1.20.4.json"))
        try ModpackFixtureProtocol.indexData.write(to: index)
    }

    func testZIPRejectsTraversalSymlinksAndFilesystemCollisions() throws {
        for path in ["../escape", "/absolute", "C:/escape", "folder/../../escape", "a//b", "a/./b", "a\u{0}b"] {
            let file = try archive([PackMember(path, Data("bad".utf8))])
            XCTAssertThrowsError(try ModpackZIPArchive(url: file), path)
        }
        let symbolic = try archive([PackMember("mods/link", Data("../../elsewhere".utf8), attributes: UInt32(0o120777) << 16)])
        XCTAssertThrowsError(try ModpackZIPArchive(url: symbolic))
        for paths in [["mods/A.jar", "mods/a.jar"], ["é.txt", "e\u{301}.txt"], ["mods", "mods/a.jar"]] {
            let file = try archive(paths.map { PackMember($0, Data()) })
            XCTAssertThrowsError(try ModpackZIPArchive(url: file))
        }
    }

    func testZIPLimitsAndCRCRejectCorruption() throws {
        let file = try archive([PackMember("manifest.json", Data("123456789".utf8))])
        XCTAssertThrowsError(try ModpackZIPArchive(url: file, limits: .init(maximumEntryBytes: 8)))
        let zip = try ModpackZIPArchive(url: file)
        XCTAssertEqual(try zip.data(for: XCTUnwrap(zip.member("manifest.json"))), Data("123456789".utf8))
        let corrupt = try archive([PackMember("manifest.json", Data("abc".utf8), checksum: 0)])
        let broken = try ModpackZIPArchive(url: corrupt)
        XCTAssertThrowsError(try broken.data(for: XCTUnwrap(broken.member("manifest.json"))))
        var mismatch = packZIP([PackMember("original", Data("a".utf8))])
        mismatch[30] = UInt8(ascii: "x")
        let mismatchURL = try temporaryDirectory().appendingPathComponent("bad.zip")
        try mismatch.write(to: mismatchURL)
        let mismatchZIP = try ModpackZIPArchive(url: mismatchURL)
        XCTAssertThrowsError(try mismatchZIP.data(for: XCTUnwrap(mismatchZIP.member("original"))))
    }

    func testDeflateFromSystemZIPIsDecodedAndVerified() throws {
        let root = try temporaryDirectory(), file = root.appendingPathComponent("deflated.txt")
        let content = Data(String(repeating: "Minecraft 整合包 fixture ", count: 500).utf8)
        try content.write(to: file)
        let result = root.appendingPathComponent("deflated.zip")
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/zip")
        process.arguments = ["-q", "-9", "-j", result.path, file.path]
        try process.run(); process.waitUntilExit()
        XCTAssertEqual(process.terminationStatus, 0)
        let zip = try ModpackZIPArchive(url: result)
        let entry = try XCTUnwrap(zip.member("deflated.txt"))
        XCTAssertEqual(entry.method, 8)
        XCTAssertEqual(try zip.data(for: entry), content)
    }

    func testInspectModrinthLoaderClientEnvironmentAndUnsafePaths() async throws {
        let file = try archive([PackMember("wrapped/modrinth.index.json", try modrinth(dependencies: ["minecraft": "1.20.4", "fabric-loader": "0.16.0"])), PackMember("wrapped/overrides/config/example.txt", Data())])
        let descriptor = try await ModpackImporter().inspect(archive: file)
        XCTAssertEqual(descriptor.format, .modrinth)
        XCTAssertEqual(descriptor.loaderKind, .fabric)
        XCTAssertEqual(descriptor.loaderVersion, "0.16.0")
        XCTAssertEqual(descriptor.minecraftVersion, "1.20.4")
        XCTAssertFalse(descriptor.needsAPIKey)
        let server = try archive([PackMember("modrinth.index.json", try modrinth(files: [["env": ["client": "unsupported", "server": "required"]]]))])
        _ = try await ModpackImporter().inspect(archive: server)
        let unsafe: [String: Any] = ["path": "../escape.jar", "hashes": ["sha1": String(repeating: "a", count: 40), "sha512": String(repeating: "b", count: 128)], "fileSize": 1, "downloads": ["https://cdn.modrinth.com/fixture.jar"]]
        let unsafeZIP = try archive([PackMember("modrinth.index.json", try modrinth(files: [unsafe]))])
        do { _ = try await ModpackImporter().inspect(archive: unsafeZIP); XCTFail("Unsafe download destinations must be rejected during inspection") }
        catch ModpackImportError.unsafePath { }
    }

    func testCurseForgeImportsWithoutUserKeyReusesRenamedModAndClassifiesResourcePack() async throws {
        let root = try temporaryDirectory(); try seedVanilla(root)
        let manifest: [String: Any] = ["manifestType": "minecraftModpack", "manifestVersion": 1, "name": "CF fixture",
            "minecraft": ["version": "1.20.4", "modLoaders": [], "recommendedRam": 4096],
            "files": [["projectID": 123, "fileID": 456, "required": true], ["projectID": 124, "fileID": 457, "required": true],
                      ["projectID": 125, "fileID": 458, "required": true]], "overrides": "overrides"]
        let file = try archive([PackMember("manifest.json", try json(manifest)),
            PackMember("overrides/mods/中文命名.jar", ModpackFixtureProtocol.bundledMod)])
        let session = fixtureSession(); defer { session.invalidateAndCancel() }
        let importer = fixtureImporter(session: session)
        let descriptor = try await importer.inspect(archive: file)
        XCTAssertFalse(descriptor.needsAPIKey)
        ModpackFixtureProtocol.resetDownloads()
        try await importer.importPack(archive: file, gameDirectory: root, profileID: "CF fixture",
            javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { _ in })
        let profile = root.appendingPathComponent("versions/CF fixture")
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("mods/中文命名.jar")), ModpackFixtureProtocol.bundledMod)
        XCTAssertFalse(FileManager.default.fileExists(atPath: profile.appendingPathComponent("mods/bundled.jar").path), "An exact renamed Mod must not be duplicated")
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("mods/downloaded.jar")), ModpackFixtureProtocol.downloadedMod)
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("resourcepacks/resource.zip")), ModpackFixtureProtocol.resourcePack)
        XCTAssertFalse(FileManager.default.fileExists(atPath: profile.appendingPathComponent("mods/resource.zip").path))
        let settings = try VersionSettingsRepository.load(version: InstalledVersion(id: "CF fixture", type: "release", requiredJava: 17, directory: profile))
        XCTAssertFalse(settings.useGlobalLaunchSettings)
        XCTAssertEqual(settings.memoryMB, 4096)
        XCTAssertEqual(ModpackFixtureProtocol.downloads.filter { $0.hasSuffix("bundled.jar") }.count, 0)
        let report = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: profile.appendingPathComponent("PCL/import-report.json"))) as? [String: Any])
        XCTAssertEqual(report["reusedFiles"] as? Int, 1)
        XCTAssertEqual(report["downloadedFiles"] as? Int, 2)
        XCTAssertEqual(report["bundledModJars"] as? Int, 1)
        XCTAssertEqual(try FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent("PCL/download-cache/modpack-files").path).filter { !$0.hasPrefix(".") }.count, 2)
        // A second profile reuses those verified files even under different names.
        ModpackFixtureProtocol.resetDownloads()
        try await importer.importPack(archive: file, gameDirectory: root, profileID: "CF cached",
            javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { _ in })
        XCTAssertEqual(ModpackFixtureProtocol.downloads.count, 0)
    }

    func testCurseForgeExportRetainsFabricLoaderAndRequiresUnresolvedReferences() async throws {
        let manifest: [String: Any] = ["manifestType": "minecraftModpack", "manifestVersion": 1,
            "name": "Fabric CF fixture", "minecraft": ["version": "1.20.1",
                "modLoaders": [["id": "fabric-0.18.4", "primary": true]]],
            "files": [["projectID": 123, "fileID": 456, "required": true],
                      ["projectID": 124, "fileID": 457, "required": true]], "overrides": "overrides"]
        for prefix in ["", "包装目录/"] {
            let file = try archive([
                PackMember(prefix + "manifest.json", try json(manifest)),
                PackMember(prefix + "overrides/mods/included.jar", Data("included mod".utf8)),
                PackMember("__MACOSX/._manifest.json", Data()),
                PackMember(".DS_Store", Data())
            ])
            let descriptor = try await ModpackImporter().inspect(archive: file)
            XCTAssertEqual(descriptor.format, .curseForge)
            XCTAssertEqual(descriptor.loaderKind, .fabric)
            XCTAssertEqual(descriptor.loaderVersion, "0.18.4")
            XCTAssertEqual(descriptor.minecraftVersion, "1.20.1")
            XCTAssertFalse(descriptor.needsAPIKey, "Required references are resolved through PCL's public mirror")
        }
    }

    func testDownloadCacheLinksCannotOverwriteExistingVersionFiles() async throws {
        for linkDirectory in [false, true] {
            let root = try temporaryDirectory(); try seedVanilla(root)
            let originalDirectory = root.appendingPathComponent("versions/Keep/mods")
            try FileManager.default.createDirectory(at: originalDirectory, withIntermediateDirectories: true)
            let original = originalDirectory.appendingPathComponent("keep.jar")
            let bytes = Data("user's original version file".utf8)
            try bytes.write(to: original)
            let cache = root.appendingPathComponent("PCL/download-cache/modpack-files")
            try FileManager.default.createDirectory(at: cache.deletingLastPathComponent(), withIntermediateDirectories: true)
            if linkDirectory {
                try FileManager.default.createSymbolicLink(at: cache, withDestinationURL: originalDirectory)
            } else {
                try FileManager.default.createDirectory(at: cache, withIntermediateDirectories: true)
                let leaf = cache.appendingPathComponent(MinecraftInstaller.hash(ModpackFixtureProtocol.downloadedMod) + ".jar")
                try FileManager.default.createSymbolicLink(at: leaf, withDestinationURL: original)
            }
            let manifest: [String: Any] = ["manifestType": "minecraftModpack", "manifestVersion": 1,
                "minecraft": ["version": "1.20.4", "modLoaders": []],
                "files": [["projectID": 124, "fileID": 457, "required": true]], "overrides": "overrides"]
            let archive = try archive([PackMember("manifest.json", try json(manifest))])
            let session = fixtureSession(); defer { session.invalidateAndCancel() }
            do {
                try await fixtureImporter(session: session).importPack(archive: archive, gameDirectory: root, profileID: "New pack",
                    javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { _ in })
                XCTFail("Private download caches must reject links into an existing version")
            } catch ModpackImportError.unsafePath { }
            XCTAssertEqual(try Data(contentsOf: original), bytes)
            XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/New pack/New pack.json").path))
        }
    }

    func testCompleteVersionIgnoresRuntimeCurseForgeManifest() async throws {
        let stale: [String: Any] = ["manifestType": "minecraftModpack", "manifestVersion": 1,
            "minecraft": ["version": "1.20.1", "modLoaders": [["id": "forge-47.0.0", "primary": true]]],
            "files": [["projectID": 123, "fileID": 456, "required": true]]]
        let file = try archive([
            PackMember("game/versions/1.20.4/1.20.4.json", try json(["id": "1.20.4", "mainClass": "Main", "libraries": []])),
            PackMember("game/versions/Fabric/Fabric.json", try json(["id": "Fabric", "inheritsFrom": "1.20.4", "mainClass": "FabricMain", "libraries": [["name": "net.fabricmc:fabric-loader:0.16.0"]]])),
            PackMember("game/versions/Fabric/config/old-export/manifest.json", try json(stale))
        ])
        let descriptor = try await ModpackImporter().inspect(archive: file)
        XCTAssertEqual(descriptor.format, .completeVersion)
        XCTAssertEqual(descriptor.minecraftVersion, "1.20.4")
        XCTAssertEqual(descriptor.loaderKind, .fabric)
        XCTAssertEqual(descriptor.loaderVersion, "0.16.0")
        XCTAssertFalse(descriptor.needsAPIKey)
    }

    func testRuntimeDirectoriesCannotBecomeExportRoots() async throws {
        let nested: [String: Any] = ["manifestType": "minecraftModpack", "manifestVersion": 1,
            "minecraft": ["version": "1.20.1", "modLoaders": [["id": "fabric-0.18.4", "primary": true]]],
            "files": [["projectID": 123, "fileID": 456, "required": true]]]
        for directory in ["overrides", "client-overrides", "config", "versions"] {
            let file = try archive([
                PackMember("包/\(directory)/export/manifest.json", try json(nested)),
                PackMember("包/\(directory)/export/data.txt", Data())
            ])
            do {
                _ = try await ModpackImporter().inspect(archive: file)
                XCTFail("A manifest in \(directory) is payload, even if every file is in that directory")
            } catch ModpackImportError.unsupported { }
        }
    }

    func testRootModrinthMetadataIgnoresNestedPayloadMarkers() async throws {
        let nested: [String: Any] = ["manifestType": "minecraftModpack", "manifestVersion": 1,
            "minecraft": ["version": "1.12.2", "modLoaders": [["id": "forge-14.23.5.2860", "primary": true]]],
            "files": [["projectID": 123, "fileID": 456, "required": true]]]
        let file = try archive([
            PackMember("modrinth.index.json", try modrinth(dependencies: ["minecraft": "1.20.4", "fabric-loader": "0.16.0"])),
            PackMember("overrides/config/old-export/manifest.json", try json(nested)),
            PackMember("client-overrides/config/manifest.json", Data("unrelated non-JSON payload".utf8)),
            PackMember("overrides/config/old-export/modrinth.index.json", Data("unrelated non-JSON payload".utf8))
        ])
        let descriptor = try await ModpackImporter().inspect(archive: file)
        XCTAssertEqual(descriptor.format, .modrinth)
        XCTAssertEqual(descriptor.minecraftVersion, "1.20.4")
        XCTAssertEqual(descriptor.loaderKind, .fabric)
        XCTAssertFalse(descriptor.needsAPIKey)
    }

    func testInspectMultiMCAndMCBBSAndCompleteVersionInheritance() async throws {
        let mmc: [String: Any] = ["formatVersion": 1, "components": [["uid": "net.minecraft", "version": "1.20.4"], ["uid": "net.fabricmc.fabric-loader", "version": "0.16.0"]]]
        let file = try archive([PackMember("实例/mmc-pack.json", try json(mmc)), PackMember("实例/instance.cfg", Data("[General]\nname=我的实例\nMaxMemAlloc=3072\n".utf8)), PackMember("实例/.minecraft/mods/example.jar", Data())])
        let descriptor = try await ModpackImporter().inspect(archive: file)
        XCTAssertEqual(descriptor.format, .multiMC)
        XCTAssertEqual(descriptor.name, "我的实例")
        XCTAssertEqual(descriptor.loaderKind, .fabric)
        let mcbbs = try archive([PackMember("mcbbs.packmeta", try json(["name": "MCBBS fixture", "addons": [["id": "game", "version": "1.20.4"], ["id": "quilt", "version": "0.26.0"]]]))])
        let bbs = try await ModpackImporter().inspect(archive: mcbbs)
        XCTAssertEqual(bbs.format, .mcbbs)
        XCTAssertEqual(bbs.loaderKind, .quilt)
        let complete = try archive([PackMember("game/versions/1.20.4/1.20.4.json", try json(["id": "1.20.4", "mainClass": "Main", "libraries": []])), PackMember("game/versions/Fabric/Fabric.json", try json(["id": "Fabric", "inheritsFrom": "1.20.4", "mainClass": "FabricMain", "libraries": [["name": "net.fabricmc:fabric-loader:0.16.0"]]]))])
        let full = try await ModpackImporter().inspect(archive: complete)
        XCTAssertEqual(full.name, "Fabric")
        XCTAssertEqual(full.format, .completeVersion)
        XCTAssertEqual(full.minecraftVersion, "1.20.4")
        XCTAssertEqual(full.loaderKind, .fabric)
    }

    func testExistingVersionIsNeverOverwritten() async throws {
        let root = try temporaryDirectory(), target = root.appendingPathComponent("versions/Existing")
        try FileManager.default.createDirectory(at: target, withIntermediateDirectories: true)
        let saved = target.appendingPathComponent("keep.txt")
        try Data("saved".utf8).write(to: saved)
        let file = try archive([PackMember("modrinth.index.json", try modrinth())])
        do {
            try await ModpackImporter().importPack(archive: file, gameDirectory: root, profileID: "existing", javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { _ in })
            XCTFail("Case-insensitive collisions must not replace existing profiles")
        } catch ModpackImportError.destinationExists { }
        XCTAssertEqual(try Data(contentsOf: saved), Data("saved".utf8))
    }

    func testCloneCannotMutateExistingSharedCacheInPlace() throws {
        let root = try temporaryDirectory(), source = root.appendingPathComponent("original.jar"), copy = root.appendingPathComponent("staging.jar")
        let original = Data("source artifact".utf8)
        try original.write(to: source)
        try ModpackImporter.cloneOrCopy(source, to: copy)
        let handle = try FileHandle(forWritingTo: copy)
        try handle.truncate(atOffset: 0); try handle.write(contentsOf: Data("rewritten by Java processor".utf8)); try handle.close()
        XCTAssertEqual(try Data(contentsOf: source), original)
    }

    func testModrinthImportCommitsIsolatedRenamedProfileAndOverridesInOrder() async throws {
        let root = try temporaryDirectory(); try seedVanilla(root)
        let file = try archive([PackMember("modrinth.index.json", try modrinth()), PackMember("overrides/config/example.txt", Data("common".utf8)), PackMember("client-overrides/config/example.txt", Data("client wins".utf8)), PackMember("server-overrides/config/server-only.txt", Data("server".utf8))])
        let session = fixtureSession(); defer { session.invalidateAndCancel() }
        let id = "我的整合包 1.20.4"
        try await fixtureImporter(session: session).importPack(archive: file, gameDirectory: root, profileID: id, javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { _ in })
        let target = root.appendingPathComponent("versions/\(id)")
        let data = try Data(contentsOf: target.appendingPathComponent("\(id).json"))
        let metadata = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertEqual(metadata["id"] as? String, id)
        XCTAssertEqual(metadata["jar"] as? String, id)
        XCTAssertNil(metadata["inheritsFrom"])
        XCTAssertEqual(metadata["pclOriginalVersion"] as? String, "1.20.4")
        XCTAssertFalse(String(decoding: data, as: UTF8.self).contains(".pcl-import-"))
        XCTAssertEqual(try Data(contentsOf: target.appendingPathComponent("\(id).jar")), ModpackFixtureProtocol.clientData)
        XCTAssertEqual(try Data(contentsOf: target.appendingPathComponent("config/example.txt")), Data("client wins".utf8))
        XCTAssertFalse(FileManager.default.fileExists(atPath: target.appendingPathComponent("config/server-only.txt").path))
        let settings = try VersionSettingsRepository.load(version: InstalledVersion(id: id, type: "release", requiredJava: 17, directory: target))
        XCTAssertTrue(settings.isolateGameDirectory)
        XCTAssertTrue(settings.useGlobalLaunchSettings)
        XCTAssertEqual(try Data(contentsOf: root.appendingPathComponent("versions/1.20.4/1.20.4.jar")), ModpackFixtureProtocol.clientData)
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: root.path).contains { $0.hasPrefix(".pcl-import-") })
    }

    func testCancellationRemovesStagingAndNeverCommitsVersion() async throws {
        let root = try temporaryDirectory(); try seedVanilla(root)
        let file = try archive([PackMember("modrinth.index.json", try modrinth()), PackMember("overrides/config/example.txt", Data("config".utf8))])
        let session = fixtureSession(); defer { session.invalidateAndCancel() }
        let importer = fixtureImporter(session: session)
        let task = Task {
            try await importer.importPack(archive: file, gameDirectory: root, profileID: "Cancelled", javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { _ in withUnsafeCurrentTask { $0?.cancel() } })
        }
        do { try await task.value; XCTFail("Cancelled imports must fail") } catch { XCTAssertTrue(task.isCancelled) }
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/Cancelled").path))
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: root.path).contains { $0.hasPrefix(".pcl-import-") })
    }

    func testSharedConflictRollsBackWithoutChangingOriginalCache() async throws {
        let root = try temporaryDirectory(); try seedVanilla(root)
        let index = root.appendingPathComponent("assets/indexes/fixture.json")
        let original = Data("existing different cache bytes".utf8)
        try original.write(to: index)
        let file = try archive([PackMember("versions/1.20.4/1.20.4.json", ModpackFixtureProtocol.metadataData), PackMember("versions/1.20.4/1.20.4.jar", ModpackFixtureProtocol.clientData), PackMember("assets/indexes/fixture.json", ModpackFixtureProtocol.indexData), PackMember("versions/1.20.4/config/example.txt", Data("config".utf8))])
        let session = fixtureSession(); defer { session.invalidateAndCancel() }
        do {
            try await fixtureImporter(session: session).importPack(archive: file, gameDirectory: root, profileID: "Conflict", javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { _ in })
            XCTFail("Conflicting caches must not overwrite a user's existing files")
        } catch ModpackImportError.sharedConflict { }
        XCTAssertEqual(try Data(contentsOf: index), original)
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/Conflict").path))
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: root.path).contains { $0.hasPrefix(".pcl-import-") })
    }
}

private struct PackMember {
    let name: String, data: Data, attributes: UInt32, checksum: UInt32?
    init(_ name: String, _ data: Data, attributes: UInt32 = UInt32(0o100644) << 16, checksum: UInt32? = nil) {
        self.name = name; self.data = data; self.attributes = attributes; self.checksum = checksum
    }
}
private func packZIP(_ members: [PackMember]) -> Data {
    func crc(_ data: Data) -> UInt32 {
        var value = UInt32.max
        for byte in data {
            value ^= UInt32(byte)
            for _ in 0..<8 { value = value & 1 == 0 ? value >> 1 : (value >> 1) ^ 0xedb88320 }
        }
        return ~value
    }
    var result = Data(), central = Data()
    for member in members {
        let name = Data(member.name.utf8), count = UInt32(member.data.count), offset = UInt32(result.count), checksum = member.checksum ?? crc(member.data)
        result.pack32(0x04034b50); result.pack16(20); result.pack16(0x800); result.pack16(0); result.pack16(0); result.pack16(0)
        result.pack32(checksum); result.pack32(count); result.pack32(count); result.pack16(UInt16(name.count)); result.pack16(0)
        result.append(name); result.append(member.data)
        central.pack32(0x02014b50); central.pack16(0x0314); central.pack16(20); central.pack16(0x800); central.pack16(0); central.pack16(0); central.pack16(0)
        central.pack32(checksum); central.pack32(count); central.pack32(count); central.pack16(UInt16(name.count)); central.pack16(0); central.pack16(0)
        central.pack16(0); central.pack16(0); central.pack32(member.attributes); central.pack32(offset); central.append(name)
    }
    let offset = UInt32(result.count)
    result.append(central); result.pack32(0x06054b50); result.pack16(0); result.pack16(0); result.pack16(UInt16(members.count)); result.pack16(UInt16(members.count))
    result.pack32(UInt32(central.count)); result.pack32(offset); result.pack16(0)
    return result
}
private extension Data {
    mutating func pack16(_ value: UInt16) { append(UInt8(truncatingIfNeeded: value)); append(UInt8(truncatingIfNeeded: value >> 8)) }
    mutating func pack32(_ value: UInt32) { pack16(UInt16(truncatingIfNeeded: value)); pack16(UInt16(truncatingIfNeeded: value >> 16)) }
}
private final class ModpackFixtureProtocol: URLProtocol {
    static let clientData = Data("fixture client".utf8)
    static let bundledMod = Data("bundled Mod exact bytes".utf8)
    static let downloadedMod = Data("downloaded Mod exact bytes".utf8)
    static let resourcePack = Data("resource pack exact bytes".utf8)
    private static let lock = NSLock()
    private static var requestedDownloads: [String] = []
    static var downloads: [String] { lock.lock(); defer { lock.unlock() }; return requestedDownloads }
    static func resetDownloads() { lock.lock(); requestedDownloads = []; lock.unlock() }
    private static func noteDownload(_ path: String) { lock.lock(); requestedDownloads.append(path); lock.unlock() }
    private static func cfFile(_ project: Int, _ id: Int, _ name: String, _ data: Data, modules: [String]) -> [String: Any] {
        ["id": id, "modId": project, "fileName": name, "fileLength": data.count,
         "downloadUrl": "https://mediafilez.forgecdn.net/files/0/\(id)/\(name)",
         "hashes": [["algo": 1, "value": MinecraftInstaller.hash(data)]],
         "modules": modules.map { ["name": $0] }]
    }
    static let indexData = Data(#"{"objects":{}}"#.utf8)
    static var metadataData: Data {
        try! JSONSerialization.data(withJSONObject: ["id": "1.20.4", "type": "release", "mainClass": "net.minecraft.client.main.Main", "libraries": [], "javaVersion": ["majorVersion": 17], "downloads": ["client": ["url": "https://pcl-pack.invalid/client.jar", "sha1": MinecraftInstaller.hash(clientData), "size": clientData.count]], "assetIndex": ["id": "fixture", "url": "https://pcl-pack.invalid/fixture.json", "sha1": MinecraftInstaller.hash(indexData), "size": indexData.count]], options: [.sortedKeys])
    }
    override class func canInit(with request: URLRequest) -> Bool {
        ["pcl-pack.invalid", "mod.mcimirror.top", "mediafilez.forgecdn.net"].contains(request.url?.host ?? "")
    }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        guard let url = request.url else { return }
        let body: Data
        switch url.lastPathComponent {
        case "files" where url.host == "mod.mcimirror.top":
            let all = [
                Self.cfFile(125, 458, "resource.zip", Self.resourcePack, modules: ["pack.mcmeta"]),
                Self.cfFile(123, 456, "bundled.jar", Self.bundledMod, modules: ["fabric.mod.json"]),
                Self.cfFile(124, 457, "downloaded.jar", Self.downloadedMod, modules: ["fabric.mod.json"])]
            let payload = request.httpBody ?? request.httpBodyStream.map { stream -> Data in
                stream.open(); defer { stream.close() }; var result = Data(); var buffer = [UInt8](repeating: 0, count: 4096)
                while stream.hasBytesAvailable { let count = stream.read(&buffer, maxLength: buffer.count); if count <= 0 { break }; result.append(contentsOf: buffer.prefix(count)) }
                return result
            } ?? Data()
            let ids = ((try? JSONSerialization.jsonObject(with: payload)) as? [String: [Int]])?["fileIds"] ?? []
            body = try! JSONSerialization.data(withJSONObject: ["data": all.filter { ids.contains($0["id"] as! Int) }])
        case "bundled.jar": Self.noteDownload(url.path); body = Self.bundledMod
        case "downloaded.jar": Self.noteDownload(url.path); body = Self.downloadedMod
        case "resource.zip": Self.noteDownload(url.path); body = Self.resourcePack
        case "manifest": body = try! JSONSerialization.data(withJSONObject: ["latest": ["release": "1.20.4", "snapshot": "1.20.4"], "versions": [["id": "1.20.4", "type": "release", "url": "https://pcl-pack.invalid/version", "sha1": MinecraftInstaller.hash(Self.metadataData)]]])
        case "version": body = Self.metadataData
        case "client.jar": body = Self.clientData
        case "fixture.json": body = Self.indexData
        default: client?.urlProtocol(self, didFailWithError: URLError(.resourceUnavailable)); return
        }
        let response = HTTPURLResponse(url: url, statusCode: 200, httpVersion: "HTTP/1.1", headerFields: ["Content-Type": "application/json"])!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: body)
        client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() { }
}
