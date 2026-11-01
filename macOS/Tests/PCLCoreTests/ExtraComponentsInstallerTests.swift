import CryptoKit
import Foundation
import XCTest
import zlib
@testable import PCLCore

final class ExtraComponentsInstallerTests: XCTestCase {
    private func root() throws -> URL {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-ExtraComponents-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        return root
    }

    private func jar(_ name: String, in folder: URL, modID: String? = nil, optiFine: Bool = false, marker: String = "") throws -> URL {
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        var members = [("marker", Data(marker.utf8))]
        if let modID {
            members.append(("fabric.mod.json", try JSONSerialization.data(withJSONObject: ["schemaVersion": 1, "id": modID, "name": modID, "version": "1.0"])))
        }
        if optiFine { members.append(("optifine/OptiFineTweaker.class", Data([0xca, 0xfe, 0xba, 0xbe]))) }
        let file = folder.appendingPathComponent(name)
        try extraComponentsZIP(members).write(to: file)
        return file
    }

    private func bridge(minecraft: String = "1.20.1", source: CommunitySource = .curseForge, projectID: String = "322385") -> CommunityVersion {
        CommunityVersion(id: "bridge-file", source: source, projectID: projectID, name: "OptiFabric", versionNumber: "1.14.3",
                         gameVersions: [minecraft], loaders: ["fabric"])
    }

    func testFabricOptiFineRequiresBridgeFromCorrectProjectAndMinecraft() {
        let loader = ModLoaderVersion(kind: .fabric, minecraftVersion: "1.20.1", version: "0.18.4")
        let opti = OptiFineVersion(minecraftVersion: "1.20.1", version: "HD_U_I6")
        XCTAssertNotNil(ExtraComponentsInstaller.compatibilityError(minecraftVersion: "1.20.1", loader: loader, selection: .init(optiFine: opti)))
        XCTAssertNil(ExtraComponentsInstaller.compatibilityError(minecraftVersion: "1.20.1", loader: loader, selection: .init(optiFine: opti, optiFabric: bridge())))
        XCTAssertNotNil(ExtraComponentsInstaller.compatibilityError(minecraftVersion: "1.20.1", loader: loader, selection: .init(optiFine: opti, optiFabric: bridge(minecraft: "1.19.4"))))
        XCTAssertNotNil(ExtraComponentsInstaller.compatibilityError(minecraftVersion: "1.20.1", loader: loader, selection: .init(optiFine: opti, optiFabric: bridge(source: .modrinth, projectID: "other"))))
        XCTAssertNotNil(ExtraComponentsInstaller.compatibilityError(minecraftVersion: "1.20.1", loader: nil, selection: .init(optiFabric: bridge())))
    }

    func testForgeCompatibilityUsesOriginalExactAndRevisionRules() {
        let forge = ModLoaderVersion(kind: .forge, minecraftVersion: "1.12.2", version: "14.23.5.2859")
        func error(_ required: String?) -> String? {
            ExtraComponentsInstaller.compatibilityError(minecraftVersion: "1.12.2", loader: forge,
                selection: .init(optiFine: .init(minecraftVersion: "1.12.2", version: "HD_U_G5", requiredForgeVersion: required)))
        }
        XCTAssertNil(error("14.23.5.2859"))
        XCTAssertNil(error("2859"))
        XCTAssertNil(error(""))
        XCTAssertNotNil(error(nil))
        XCTAssertNotNil(error("14.23.5.2860"))
        XCTAssertNotNil(error("2860"))
        let oldForge = ModLoaderVersion(kind: .forge, minecraftVersion: "1.14.3", version: "27.0.60")
        XCTAssertNotNil(ExtraComponentsInstaller.compatibilityError(minecraftVersion: "1.14.3", loader: oldForge,
            selection: .init(optiFine: .init(minecraftVersion: "1.14.3", version: "HD_U_F1"))))
    }

    func testLiteLoaderCombinesWithForgeButNotFabricOrDifferentMinecraft() {
        let lite = LiteLoaderVersion(minecraftVersion: "1.12.2", version: "1.12.2-SNAPSHOT", filename: "liteloader.jar", metadata: Data("{}".utf8), md5: String(repeating: "a", count: 32))
        let forge = ModLoaderVersion(kind: .forge, minecraftVersion: "1.12.2", version: "14.23.5.2859")
        XCTAssertNil(ExtraComponentsInstaller.compatibilityError(minecraftVersion: "1.12.2", loader: forge, selection: .init(liteLoader: lite)))
        XCTAssertNil(ExtraComponentsInstaller.compatibilityError(minecraftVersion: "1.12.2", loader: nil, selection: .init(liteLoader: lite)))
        XCTAssertNotNil(ExtraComponentsInstaller.compatibilityError(minecraftVersion: "1.12.2", loader: .init(kind: .fabric, minecraftVersion: "1.12.2", version: "0.18.4"), selection: .init(liteLoader: lite)))
        XCTAssertNotNil(ExtraComponentsInstaller.compatibilityError(minecraftVersion: "1.20.1", loader: nil, selection: .init(liteLoader: lite)))
    }

    func testNewInstallCannotTreatKeepOriginalFlagAsExistingOptiFabric() async throws {
        let root = try root()
        do {
            try await ExtraComponentsInstaller().install(minecraft: .init(id: "1.20.1", type: "release", url: URL(string: "https://example.invalid/never-requested")!),
                loader: .init(kind: .fabric, minecraftVersion: "1.20.1", version: "0.18.4"),
                selection: .init(optiFine: .init(minecraftVersion: "1.20.1", version: "HD_U_I6"), keepOriginalOptiFabric: true),
                fabricAPI: nil, gameDirectory: root, profileID: "must-not-publish", javaExecutable: URL(fileURLWithPath: "/not-a-java"), progress: { _ in })
            XCTFail("An initial install has no existing OptiFabric to preserve")
        } catch {
            XCTAssertTrue(error.localizedDescription.contains("OptiFabric"), "\(error)")
        }
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/must-not-publish").path))
    }

    func testSnapshotClaimsMarkersInsteadOfFilenamesAndIgnoresDisabledMods() throws {
        let root = try root(), mods = root.appendingPathComponent("mods")
        _ = try jar("OptiFine-lookalike.jar", in: mods, modID: "unrelated")
        _ = try jar("renamed-real.jar", in: mods, optiFine: true)
        _ = try jar("bridge.jar", in: mods, modID: "optifabric")
        _ = try jar("disabled.jar.disabled", in: mods, modID: "optifabric")
        let snapshot = try ExtraModSnapshot.capture(folder: mods)
        XCTAssertEqual(Set(snapshot.files.map { $0.url.lastPathComponent }), ["renamed-real.jar", "bridge.jar"])
        XCTAssertEqual(Set(snapshot.files.map(\.kind)), [.optiFine, .optiFabric])
    }

    func testReplacementRollbackRestoresBothComponentsAndLeavesOtherFilesAlone() throws {
        let root = try root(), mods = root.appendingPathComponent("versions/Game/mods"), prepared = root.appendingPathComponent("prepared/mods")
        let oldOpti = try jar("old-opti.jar", in: mods, optiFine: true, marker: "old")
        let oldBridge = try jar("old-bridge.jar", in: mods, modID: "optifabric", marker: "old")
        let other = try jar("other.jar", in: mods, modID: "other"), disabled = try jar("bridge.jar.disabled", in: mods, modID: "optifabric")
        let before = try [oldOpti, oldBridge, other, disabled].map { ($0, try Data(contentsOf: $0)) }
        let newOpti = try jar("new-opti.jar", in: prepared, optiFine: true, marker: "new")
        let newBridge = try jar("new-bridge.jar", in: prepared, modID: "optifabric", marker: "new")
        let transaction = try ExtraModTransaction.apply(expected: ExtraModSnapshot.capture(folder: mods), desired: ExtraModSnapshot.capture(folder: prepared), gameDirectory: root)
        XCTAssertTrue(FileManager.default.fileExists(atPath: mods.appendingPathComponent(newOpti.lastPathComponent).path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: mods.appendingPathComponent(newBridge.lastPathComponent).path))
        try transaction.rollback()
        for (file, bytes) in before { XCTAssertEqual(try Data(contentsOf: file), bytes) }
        XCTAssertFalse(FileManager.default.fileExists(atPath: mods.appendingPathComponent(newOpti.lastPathComponent).path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: mods.appendingPathComponent(newBridge.lastPathComponent).path))
    }

    func testTransactionRejectsStaleComponentAndUnownedDestinationCollision() throws {
        let root = try root(), mods = root.appendingPathComponent("mods"), prepared = root.appendingPathComponent("prepared")
        let old = try jar("component.jar", in: mods, optiFine: true, marker: "first")
        let expected = try ExtraModSnapshot.capture(folder: mods)
        _ = try jar("component.jar", in: mods, optiFine: true, marker: "external change")
        _ = try jar("incoming.jar", in: prepared, optiFine: true, marker: "new")
        XCTAssertThrowsError(try ExtraModTransaction.apply(expected: expected, desired: ExtraModSnapshot.capture(folder: prepared), gameDirectory: root))
        XCTAssertTrue(try String(decoding: Data(contentsOf: old), as: UTF8.self).contains("external change"))
        let collision = try jar("incoming.jar", in: mods, modID: "unrelated")
        let collisionBytes = try Data(contentsOf: collision), oldBytes = try Data(contentsOf: old)
        XCTAssertThrowsError(try ExtraModTransaction.apply(expected: ExtraModSnapshot.capture(folder: mods), desired: ExtraModSnapshot.capture(folder: prepared), gameDirectory: root))
        XCTAssertEqual(try Data(contentsOf: collision), collisionBytes)
        XCTAssertEqual(try Data(contentsOf: old), oldBytes)
    }

    func testIdenticalComponentsDoNotRewriteRuntimeFiles() throws {
        let root = try root(), mods = root.appendingPathComponent("mods"), prepared = root.appendingPathComponent("prepared")
        let original = try jar("same.jar", in: mods, modID: "optifabric")
        let bytes = try Data(contentsOf: original), before = try original.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate
        try FileManager.default.createDirectory(at: prepared, withIntermediateDirectories: true)
        try bytes.write(to: prepared.appendingPathComponent("same.jar"))
        let transaction = try ExtraModTransaction.apply(expected: ExtraModSnapshot.capture(folder: mods), desired: ExtraModSnapshot.capture(folder: prepared), gameDirectory: root)
        transaction.finish()
        XCTAssertEqual(try original.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate, before)
        XCTAssertEqual(try Data(contentsOf: original), bytes)
    }

    func testRemovingLaunchComponentsDoesNotResurrectOldMetadataMarkers() throws {
        let root = try root()
        func profile(_ name: String, _ metadata: [String: Any], client: Data) throws -> InstalledVersion {
            let folder = root.appendingPathComponent("versions/" + name)
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            try JSONSerialization.data(withJSONObject: metadata).write(to: folder.appendingPathComponent(name + ".json"))
            try client.write(to: folder.appendingPathComponent(name + ".jar"))
            return .init(id: name, type: "release", requiredJava: 8, directory: folder)
        }
        let client = Data("vanilla client".utf8)
        let old = try profile("Existing", ["id": "Existing", "mainClass": "net.minecraft.launchwrapper.Launch", "pclOriginalVersion": "1.12.2",
            "pclOptiFine": ["version": "HD_U_G5"], "pclLiteLoader": ["version": "1.12.2-SNAPSHOT"], "pclOptiFabric": ["version": "old"],
            "pclExtraComponents": ["managed": true], "pclModpackName": "Preserved pack"], client: Data("old client".utf8))
        _ = try profile("Prepared", ["id": "Prepared", "pclOriginalVersion": "1.12.2", "jar": "Prepared", "mainClass": "net.minecraft.client.main.Main", "libraries": [],
            "downloads": ["client": ["sha1": MinecraftInstaller.hash(client), "size": client.count]]], client: client)
        try VersionComponentManager.applyPreparedProfile(version: old, preparedID: "Prepared", gameDirectory: root,
            expectedFingerprint: VersionComponentManager.fingerprint(version: old, gameDirectory: root))
        let metadata = try MinecraftVersionMetadata.load(versionID: old.id, gameDirectory: root)
        for key in ["pclOptiFine", "pclLiteLoader", "pclOptiFabric", "pclExtraComponents"] { XCTAssertNil(metadata[key], key) }
        XCTAssertEqual(metadata["pclModpackName"] as? String, "Preserved pack")
        XCTAssertEqual(try Data(contentsOf: old.directory.appendingPathComponent(old.id + ".jar")), client)
    }
}

private func extraComponentsZIP(_ files: [(String, Data)]) throws -> Data {
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
