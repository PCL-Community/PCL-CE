import XCTest
@testable import PCLCore

final class LocalModManagerTests: XCTestCase {
    private func root() throws -> URL {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-LocalMod-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: folder) }
        return folder
    }
    private func jar(_ path: URL, files: [String: Data]) throws {
        var output = Data(), central = Data()
        func word<T: FixedWidthInteger>(_ value: T) -> Data { withUnsafeBytes(of: value.littleEndian) { Data($0) } }
        func checksum(_ data: Data) -> UInt32 {
            var value = UInt32.max
            for byte in data {
                value ^= UInt32(byte)
                for _ in 0..<8 { value = value & 1 == 0 ? value >> 1 : (value >> 1) ^ 0xedb88320 }
            }
            return ~value
        }
        for (name, bytes) in files.sorted(by: { $0.key < $1.key }) {
            let nameBytes = Data(name.utf8), crc = checksum(bytes), offset = UInt32(output.count)
            output += word(UInt32(0x04034b50)); output += word(UInt16(20)); output += word(UInt16(0x800)); output += word(UInt16(0))
            output += word(UInt16(0)); output += word(UInt16(0)); output += word(crc); output += word(UInt32(bytes.count)); output += word(UInt32(bytes.count))
            output += word(UInt16(nameBytes.count)); output += word(UInt16(0)); output += nameBytes; output += bytes
            central += word(UInt32(0x02014b50)); central += word(UInt16(20)); central += word(UInt16(20)); central += word(UInt16(0x800)); central += word(UInt16(0))
            central += word(UInt16(0)); central += word(UInt16(0)); central += word(crc); central += word(UInt32(bytes.count)); central += word(UInt32(bytes.count))
            central += word(UInt16(nameBytes.count)); central += word(UInt16(0)); central += word(UInt16(0)); central += word(UInt16(0)); central += word(UInt16(0)); central += word(UInt32(0)); central += word(offset); central += nameBytes
        }
        let start = UInt32(output.count); output += central
        output += word(UInt32(0x06054b50)); output += word(UInt16(0)); output += word(UInt16(0)); output += word(UInt16(files.count)); output += word(UInt16(files.count)); output += word(UInt32(central.count)); output += word(start); output += word(UInt16(0))
        try output.write(to: path)
    }
    private func json(_ value: [String: Any]) throws -> Data { try JSONSerialization.data(withJSONObject: value) }

    func testScansFabricQuiltForgeMetadataAndDisabledFilesReadOnly() throws {
        let folder = try root()
        try jar(folder.appendingPathComponent("fabric.jar"), files: ["fabric.mod.json": try json(["id":"sodium", "name":"Sodium", "version":"0.5.8", "description":"Renderer"])])
        try jar(folder.appendingPathComponent("quilt.jar.disabled"), files: ["quilt.mod.json": try json(["quilt_loader":["id":"quilt", "version":"2", "metadata":["name":"Quilt Mod"]]])])
        try jar(folder.appendingPathComponent("forge.jar"), files: ["META-INF/mods.toml": Data("[[mods]]\nmodId=\"forge_test\"\ndisplayName=\"Forge Test\"\nversion=\"${file.jarVersion}\"\n".utf8), "META-INF/MANIFEST.MF": Data("Implementation-Version: 3.5\n".utf8)])
        try jar(folder.appendingPathComponent("neo.jar.old"), files: ["META-INF/neoforge.mods.toml": Data("[[mods]]\nmodId='neo'\ndisplayName='Neo Test'\nversion='4'\n".utf8)])
        let files = try FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: nil)
        let snapshots = try files.map { try Data(contentsOf: $0) }
        let mods = try LocalModManager.scan(directory: folder)
        XCTAssertEqual(mods.count,4)
        XCTAssertEqual(mods.first { $0.modID == "sodium" }?.name,"Sodium")
        XCTAssertEqual(mods.first { $0.modID == "sodium" }?.summary,"Renderer")
        XCTAssertEqual(mods.first { $0.modID == "quilt" }?.enabled,false)
        XCTAssertEqual(mods.first { $0.modID == "forge_test" }?.version,"3.5")
        XCTAssertEqual(mods.first { $0.modID == "neo" }?.enabled,false)
        for (file,data) in zip(files,snapshots) { XCTAssertEqual(try Data(contentsOf:file),data) }
    }

    func testToggleRoundTripPreservesBytesAndEnablingCollisionChangesNothing() throws {
        let folder = try root(), file = folder.appendingPathComponent("mod.jar")
        try jar(file, files:["fabric.mod.json":try json(["id":"test","version":"1"])])
        let original = try Data(contentsOf:file)
        try LocalModManager.setEnabled(false, files:LocalModManager.scan(directory:folder), directory:folder)
        XCTAssertFalse(FileManager.default.fileExists(atPath:file.path))
        XCTAssertEqual(try Data(contentsOf:folder.appendingPathComponent("mod.jar.disabled")),original)
        try Data("conflict".utf8).write(to:file)
        let disabled = try LocalModManager.scan(directory:folder).filter { !$0.enabled }
        XCTAssertThrowsError(try LocalModManager.setEnabled(true,files:disabled,directory:folder))
        XCTAssertEqual(try Data(contentsOf:file),Data("conflict".utf8))
        XCTAssertEqual(try Data(contentsOf:folder.appendingPathComponent("mod.jar.disabled")),original)
        try FileManager.default.removeItem(at:file)
        try LocalModManager.setEnabled(true,files:disabled,directory:folder)
        XCTAssertEqual(try Data(contentsOf:file),original)
    }

    func testBatchCollisionRejectsBeforeRenamingAnyFile() throws {
        let folder = try root()
        for name in ["a.jar","b.jar"] { try jar(folder.appendingPathComponent(name),files:[:]) }
        try Data("existing".utf8).write(to:folder.appendingPathComponent("b.jar.disabled"))
        let enabled = try LocalModManager.scan(directory:folder).filter(\.enabled)
        XCTAssertThrowsError(try LocalModManager.setEnabled(false,files:enabled,directory:folder))
        XCTAssertTrue(FileManager.default.fileExists(atPath:folder.appendingPathComponent("a.jar").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath:folder.appendingPathComponent("b.jar").path))
    }

    func testImportRejectsConflictsBeforePublishingAnyFileAndPreservesSources() throws {
        let source=try root(), destination=try root()
        let a=source.appendingPathComponent("A.jar"), b=source.appendingPathComponent("b.jar")
        try jar(a,files:["fabric.mod.json":try json(["id":"a"])])
        try jar(b,files:[:]); try Data("existing".utf8).write(to:destination.appendingPathComponent("B.jar"))
        XCTAssertThrowsError(try LocalModManager.importFiles([a,b],directory:destination))
        XCTAssertFalse(FileManager.default.fileExists(atPath:destination.appendingPathComponent("A.jar").path))
        XCTAssertEqual(try LocalModManager.importFiles([a],directory:destination),1)
        XCTAssertEqual(try Data(contentsOf:a),try Data(contentsOf:destination.appendingPathComponent("A.jar")))
        XCTAssertTrue(FileManager.default.fileExists(atPath:b.path))
    }

    func testSymlinksAndOutsideProfileAreRejected() throws {
        let game=try root(), outside=try root()
        let profile=game.appendingPathComponent("versions/test");try FileManager.default.createDirectory(at:profile,withIntermediateDirectories:true)
        let version=InstalledVersion(id:"test",type:"release",requiredJava:17,directory:profile)
        try FileManager.default.createSymbolicLink(at:profile.appendingPathComponent("mods"),withDestinationURL:outside)
        XCTAssertThrowsError(try LocalModManager.directory(gameDirectory:game,version:version,isolate:true))
        let wrong=InstalledVersion(id:"test",type:"release",requiredJava:17,directory:outside)
        XCTAssertThrowsError(try LocalModManager.directory(gameDirectory:game,version:wrong,isolate:false))
        let mods=try root();try jar(outside.appendingPathComponent("real.jar"),files:[:]);try FileManager.default.createSymbolicLink(at:mods.appendingPathComponent("link.jar"),withDestinationURL:outside.appendingPathComponent("real.jar"))
        XCTAssertThrowsError(try LocalModManager.scan(directory:mods))
        let hard=try root();try FileManager.default.linkItem(at:outside.appendingPathComponent("real.jar"),to:hard.appendingPathComponent("hard.jar"))
        XCTAssertThrowsError(try LocalModManager.scan(directory:hard))
        XCTAssertThrowsError(try LocalModManager.importFiles([hard.appendingPathComponent("hard.jar")],directory:try root()))
    }

    func testBareDisabledAndLegacyMetadataCanBeEnabled() throws {
        let folder=try root(), file=folder.appendingPathComponent("legacy.disabled")
        let data=try JSONSerialization.data(withJSONObject:[["modid":"legacy", "name":"Legacy Mod", "version":"1.2"]])
        try jar(file,files:["mcmod.info":data])
        let mods=try LocalModManager.scan(directory:folder)
        XCTAssertEqual(mods.first?.name,"Legacy Mod");XCTAssertEqual(mods.first?.enabled,false)
        try LocalModManager.setEnabled(true,files:mods,directory:folder)
        XCTAssertTrue(FileManager.default.fileExists(atPath:folder.appendingPathComponent("legacy.jar").path))
    }

    func testEmbeddedIconIsReadWithoutExtractingAndOversizedIconIsIgnored() throws {
        let folder=try root(), icon=Data([1,2,3,4])
        try jar(folder.appendingPathComponent("icon.jar"),files:["fabric.mod.json":try json(["id":"icon", "icon":["16":"tiny.png","128":"icon.png"]]),"icon.png":icon,"tiny.png":Data([9])])
        try jar(folder.appendingPathComponent("large.jar"),files:["fabric.mod.json":try json(["id":"large", "icon":"huge.png"]),"huge.png":Data(repeating:7,count:600_000)])
        let mods=try LocalModManager.scan(directory:folder)
        XCTAssertEqual(mods.first { $0.modID == "icon" }?.iconData,icon)
        XCTAssertNil(mods.first { $0.modID == "large" }?.iconData)
        XCTAssertNil(mods.first { $0.modID == "large" }?.error)
        XCTAssertFalse(FileManager.default.fileExists(atPath:folder.appendingPathComponent("icon.png").path))
    }

    func testJavaCaseSensitiveResourcesAndGsonDescriptionNewlinesAreAcceptedReadOnly() throws {
        let folder=try root(), file=folder.appendingPathComponent("java.jar")
        let metadata=Data("{\"id\":\"java\",\"name\":\"Java Mod\",\"description\":\"First line\nSecond line\"}".utf8)
        try jar(file,files:["fabric.mod.json":metadata,"assets/Case.class":Data([1]),"assets/case.class":Data([2])])
        let before=try Data(contentsOf:file), mod=try XCTUnwrap(LocalModManager.scan(directory:folder).first)
        XCTAssertEqual(mod.name,"Java Mod");XCTAssertEqual(mod.summary,"First line\nSecond line");XCTAssertNil(mod.error)
        XCTAssertEqual(try Data(contentsOf:file),before)
        let destination=try root();XCTAssertEqual(try LocalModManager.importFiles([file],directory:destination),1)
        XCTAssertEqual(try Data(contentsOf:destination.appendingPathComponent("java.jar")),before)
    }

    func testRuntimeIsolationAndCorruptJarReporting() throws {
        let game=try root(), profile=game.appendingPathComponent("versions/test");try FileManager.default.createDirectory(at:profile,withIntermediateDirectories:true)
        let version=InstalledVersion(id:"test",type:"release",requiredJava:17,directory:profile)
        let common=try LocalModManager.directory(gameDirectory:game,version:version,isolate:false,create:true)
        let isolated=try LocalModManager.directory(gameDirectory:game,version:version,isolate:true,create:true)
        XCTAssertEqual(common.lastPathComponent,"mods");XCTAssertNotEqual(common,isolated)
        try Data("broken".utf8).write(to:isolated.appendingPathComponent("broken.jar"))
        XCTAssertEqual(try LocalModManager.scan(directory:common).count,0)
        let broken=try XCTUnwrap(LocalModManager.scan(directory:isolated).first)
        XCTAssertNotNil(broken.error);XCTAssertEqual(broken.filename,"broken.jar")
    }
}
