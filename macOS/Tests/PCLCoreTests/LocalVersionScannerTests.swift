import XCTest
@testable import PCLCore

final class LocalVersionScannerTests: XCTestCase {
    private func root() throws -> URL {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-LocalScanner-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: directory) }
        return directory
    }

    private func profile(_ id: String, _ metadata: [String: Any], root: URL) throws {
        let directory = root.appendingPathComponent("versions/\(id)")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try JSONSerialization.data(withJSONObject: metadata, options: [.sortedKeys])
            .write(to: directory.appendingPathComponent("\(id).json"))
    }

    private func scanReadOnly(_ root: URL) throws -> [InstalledVersion] {
        let versions = root.appendingPathComponent("versions")
        let directories = try FileManager.default.contentsOfDirectory(at: versions, includingPropertiesForKeys: nil)
        let files = directories.map { $0.appendingPathComponent("\($0.lastPathComponent).json") }
        let original = try files.map { try Data(contentsOf: $0) }
        let result = try LocalVersionScanner.scan(gameDirectory: root)
        for (file, bytes) in zip(files, original) {
            XCTAssertEqual(try Data(contentsOf: file), bytes, "Scanning must preserve \(file.lastPathComponent)")
        }
        return result
    }

    func testFlattenedFabricPackRetainsLoaderWithoutInheritance() throws {
        let directory = try root()
        try profile("乌托邦探险之旅", ["id": "乌托邦探险之旅", "type": "release", "pclOriginalVersion": "1.20.1",
            "pclLoaderKind": "fabric", "pclLoaderVersion": "0.18.4", "javaVersion": ["majorVersion": 17]], root: directory)
        let version = try XCTUnwrap(scanReadOnly(directory).first)
        XCTAssertNil(version.inheritsFrom)
        XCTAssertEqual(version.modLoader?.kind, .fabric)
        XCTAssertEqual(version.modLoader?.version, "0.18.4")
        XCTAssertEqual(version.modLoader?.minecraftVersion, "1.20.1")
        XCTAssertEqual(version.requiredJava, 17)
    }

    func testNestedLoaderMarkerSurvivesCustomVersionName() throws {
        let directory = try root()
        try profile("我的整合包", ["id": "我的整合包", "pclOriginalVersion": "1.20.4",
            "pclLoader": ["kind": "quilt", "version": "0.27.1", "minecraftVersion": "1.20.4"]], root: directory)
        let version = try XCTUnwrap(scanReadOnly(directory).first)
        XCTAssertEqual(version.modLoader?.kind, .quilt)
        XCTAssertEqual(version.modLoader?.version, "0.27.1")
        XCTAssertEqual(version.modLoader?.minecraftVersion, "1.20.4")
    }

    func testInheritedLibraryProfilesExposeEachLoaderAndVersion() throws {
        let directory = try root()
        try profile("1.20.4", ["id": "1.20.4", "type": "release", "javaVersion": ["majorVersion": 17]], root: directory)
        let loaders: [(ModLoaderKind, String, String)] = [
            (.fabric, "net.fabricmc:fabric-loader:0.16.14", "0.16.14"),
            (.forge, "net.minecraftforge:forge:1.20.4-49.2.13", "49.2.13"),
            (.neoForge, "net.neoforged:neoforge:20.4.251", "20.4.251"),
            (.quilt, "org.quiltmc:quilt-loader:0.27.1", "0.27.1")
        ]
        for (kind, library, _) in loaders {
            try profile(kind.rawValue, ["id": kind.rawValue, "inheritsFrom": "1.20.4",
                "libraries": [["name": library]]], root: directory)
        }
        let versions = try scanReadOnly(directory)
        for (kind, _, loaderVersion) in loaders {
            let version = try XCTUnwrap(versions.first { $0.id == kind.rawValue })
            XCTAssertEqual(version.modLoader?.kind, kind)
            XCTAssertEqual(version.modLoader?.version, loaderVersion)
            XCTAssertEqual(version.modLoader?.minecraftVersion, "1.20.4")
            XCTAssertEqual(version.requiredJava, 17)
        }
    }

    func testInheritedVanillaAndLoaderNamedVanillaRemainUnmodded() throws {
        let directory = try root()
        try profile("1.20.4", ["id": "1.20.4", "type": "release"], root: directory)
        try profile("Fabric themed vanilla", ["id": "Fabric themed vanilla", "type": "release",
            "inheritsFrom": "1.20.4", "mainClass": "net.minecraft.client.main.Main"], root: directory)
        let versions = try scanReadOnly(directory)
        XCTAssertEqual(versions.count, 2)
        XCTAssertTrue(versions.allSatisfy { $0.modLoader == nil })
        XCTAssertEqual(versions.first { $0.id == "Fabric themed vanilla" }?.inheritsFrom, "1.20.4")
    }

    func testChildCanInheritLoaderFromParentMetadata() throws {
        let directory = try root()
        try profile("base", ["id": "base", "pclOriginalVersion": "1.20.1",
            "libraries": [["name": "net.fabricmc:fabric-loader:0.18.4"]]], root: directory)
        try profile("child", ["id": "child", "inheritsFrom": "base"], root: directory)
        let child = try XCTUnwrap(scanReadOnly(directory).first { $0.id == "child" })
        XCTAssertEqual(child.modLoader?.kind, .fabric)
        XCTAssertEqual(child.modLoader?.version, "0.18.4")
        XCTAssertEqual(child.modLoader?.minecraftVersion, "1.20.1")
    }

    func testMissingParentStillReportsLocallyDeclaredLoader() throws {
        let directory = try root()
        try profile("legacy", ["id": "legacy", "inheritsFrom": "1.12.2",
            "libraries": [["name": "net.minecraftforge:minecraftforge:1.12.2-14.23.5.2860"]]], root: directory)
        let version = try XCTUnwrap(scanReadOnly(directory).first)
        XCTAssertEqual(version.modLoader?.kind, .forge)
        XCTAssertEqual(version.modLoader?.version, "14.23.5.2860")
        XCTAssertEqual(version.modLoader?.minecraftVersion, "1.12.2")
    }
}
