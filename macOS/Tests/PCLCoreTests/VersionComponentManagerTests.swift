import CryptoKit
import XCTest
@testable import PCLCore

final class VersionComponentManagerTests: XCTestCase {
    private func root() throws -> URL {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-Components-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        return root
    }
    private func profile(_ id: String, _ json: [String: Any], jar: String? = nil, root: URL) throws -> InstalledVersion {
        let folder = root.appendingPathComponent("versions/\(id)")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        try JSONSerialization.data(withJSONObject: json, options: [.sortedKeys]).write(to: folder.appendingPathComponent("\(id).json"))
        if let jar { try Data(jar.utf8).write(to: folder.appendingPathComponent("\(id).jar")) }
        return InstalledVersion(id: id, type: "release", requiredJava: 17, directory: folder)
    }
    private func prepared(_ root: URL) throws {
        let bytes = Data("new vanilla client".utf8)
        let hash = Insecure.SHA1.hash(data: bytes).map { String(format: "%02x", $0) }.joined()
        _ = try profile("1.20.4", ["id": "1.20.4", "mainClass": "net.minecraft.client.main.Main", "javaVersion": ["majorVersion": 17],
            "downloads": ["client": ["sha1": hash, "size": bytes.count]]], jar: "new vanilla client", root: root)
        _ = try profile("prepared", ["id": "prepared", "inheritsFrom": "1.20.4", "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
            "pclLoaderKind": "fabric", "pclLoaderVersion": "0.18.4", "libraries": [["name": "net.fabricmc:fabric-loader:0.18.4"]]], root: root)
    }
    func testComponentChangeFlattensProfileAndPreservesModsSavesSettingsAndSource() throws {
        let root = try root(); try prepared(root)
        let old = try profile("我的整合包", ["id": "我的整合包", "mainClass": "old.Main", "pclOriginalVersion": "1.20.1", "pclModpackName": "Original pack"], jar: "old client", root: root)
        let kept = ["mods/existing.jar", "saves/My World/level.dat", "config/example.json", "PCL/macos-settings.json"]
        for path in kept {
            let file = old.directory.appendingPathComponent(path)
            try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
            try Data(path.utf8).write(to: file)
        }
        let fingerprint = try VersionComponentManager.fingerprint(version: old, gameDirectory: root)
        XCTAssertEqual(try VersionComponentManager.applyPreparedProfile(version: old, preparedID: "prepared", gameDirectory: root, expectedFingerprint: fingerprint), old.id)
        let json = try MinecraftVersionMetadata.load(versionID: old.id, gameDirectory: root)
        XCTAssertNil(json["inheritsFrom"])
        XCTAssertEqual(json["id"] as? String, old.id)
        XCTAssertEqual(json["jar"] as? String, old.id)
        XCTAssertEqual(json["pclOriginalVersion"] as? String, "1.20.4")
        XCTAssertEqual(json["pclModpackName"] as? String, "Original pack")
        XCTAssertEqual(MinecraftVersionMetadata.modLoader(in: json)?.version, "0.18.4")
        XCTAssertEqual(try String(contentsOf: old.directory.appendingPathComponent("\(old.id).jar")), "new vanilla client")
        for path in kept { XCTAssertEqual(try Data(contentsOf: old.directory.appendingPathComponent(path)), Data(path.utf8)) }
    }
    func testRemovingLoaderDoesNotRetainOldLoaderMarker() throws {
        let root = try root(); try prepared(root)
        let old = try profile("modded", ["id": "modded", "pclLoaderKind": "forge", "pclLoaderVersion": "49.2.13", "pclLoader": ["kind": "forge", "version": "49.2.13"]], jar: "old", root: root)
        let fingerprint = try VersionComponentManager.fingerprint(version: old, gameDirectory: root)
        try VersionComponentManager.applyPreparedProfile(version: old, preparedID: "1.20.4", gameDirectory: root, expectedFingerprint: fingerprint)
        let json = try MinecraftVersionMetadata.load(versionID: old.id, gameDirectory: root)
        XCTAssertNil(MinecraftVersionMetadata.modLoader(in: json))
        XCTAssertNil(json["pclLoader"])
    }
    func testChangedOriginalAndCorruptPreparedClientRejectBeforeCommit() throws {
        let root = try root(); try prepared(root)
        let old = try profile("old", ["id": "old", "mainClass": "old.Main"], jar: "old", root: root)
        let json = old.directory.appendingPathComponent("old.json")
        let initial = try Data(contentsOf: json)
        XCTAssertThrowsError(try VersionComponentManager.applyPreparedProfile(version: old, preparedID: "prepared", gameDirectory: root, expectedFingerprint: "stale"))
        XCTAssertEqual(try Data(contentsOf: json), initial)
        try Data("corrupt".utf8).write(to: root.appendingPathComponent("versions/1.20.4/1.20.4.jar"))
        XCTAssertThrowsError(try VersionComponentManager.applyPreparedProfile(version: old, preparedID: "prepared", gameDirectory: root, expectedFingerprint: try VersionComponentManager.fingerprint(version: old, gameDirectory: root)))
        XCTAssertEqual(try Data(contentsOf: json), initial)
        XCTAssertEqual(try String(contentsOf: old.directory.appendingPathComponent("old.jar")), "old")
    }
    func testRenameFailureRollsBackBothClientAndMetadata() throws {
        let root = try root(); try prepared(root)
        let old = try profile("old", ["id": "old", "mainClass": "old.Main"], jar: "old client", root: root)
        try Data("keep existing auxiliary file".utf8).write(to: old.directory.appendingPathComponent("renamed.json"))
        let initial = try Data(contentsOf: old.directory.appendingPathComponent("old.json"))
        XCTAssertThrowsError(try VersionComponentManager.applyPreparedProfile(version: old, preparedID: "prepared", gameDirectory: root, newName: "renamed", expectedFingerprint: try VersionComponentManager.fingerprint(version: old, gameDirectory: root)))
        XCTAssertEqual(try Data(contentsOf: old.directory.appendingPathComponent("old.json")), initial)
        XCTAssertEqual(try String(contentsOf: old.directory.appendingPathComponent("old.jar")), "old client")
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/renamed").path))
    }
    func testDependentVersionsPreventChangingSharedClient() throws {
        let root = try root(); try prepared(root)
        let old = try profile("old", ["id": "old", "mainClass": "old.Main"], jar: "old client", root: root)
        _ = try profile("dependent", ["id": "dependent", "inheritsFrom": "old"], root: root)
        let initial = try Data(contentsOf: old.directory.appendingPathComponent("old.json"))
        XCTAssertThrowsError(try VersionComponentManager.applyPreparedProfile(version: old, preparedID: "prepared", gameDirectory: root,
            expectedFingerprint: try VersionComponentManager.fingerprint(version: old, gameDirectory: root)))
        XCTAssertEqual(try Data(contentsOf: old.directory.appendingPathComponent("old.json")), initial)
        XCTAssertEqual(try String(contentsOf: old.directory.appendingPathComponent("old.jar")), "old client")
    }
    func testModificationAndRenamePreserveFolderData() throws {
        let root = try root(); try prepared(root)
        let old = try profile("old", ["id": "old", "mainClass": "old.Main"], jar: "old", root: root)
        try Data("world".utf8).write(to: old.directory.appendingPathComponent("world.txt"))
        let final = try VersionComponentManager.applyPreparedProfile(version: old, preparedID: "prepared", gameDirectory: root, newName: "new name", expectedFingerprint: try VersionComponentManager.fingerprint(version: old, gameDirectory: root))
        XCTAssertEqual(final, "new name")
        let json = try MinecraftVersionMetadata.load(versionID: final, gameDirectory: root)
        XCTAssertEqual(json["id"] as? String, final)
        XCTAssertEqual(json["jar"] as? String, final)
        XCTAssertEqual(try String(contentsOf: root.appendingPathComponent("versions/new name/world.txt")), "world")
    }
    func testSymlinkMainFileCannotOverwriteAnotherFile() throws {
        let root = try root(); try prepared(root)
        let old = try profile("old", ["id": "old", "mainClass": "old.Main"], jar: "old", root: root)
        let jar = old.directory.appendingPathComponent("old.jar")
        let other = old.directory.appendingPathComponent("other.jar")
        try FileManager.default.moveItem(at: jar, to: other)
        try FileManager.default.createSymbolicLink(at: jar, withDestinationURL: other)
        XCTAssertThrowsError(try VersionComponentManager.applyPreparedProfile(version: old, preparedID: "prepared", gameDirectory: root, expectedFingerprint: try VersionComponentManager.fingerprint(version: old, gameDirectory: root)))
        XCTAssertEqual(try String(contentsOf: other), "old")
    }
}
