import Foundation
import XCTest
@testable import PCLCore

final class LegacyJavaSelectionTests: XCTestCase {
    private func temporaryDirectory() throws -> URL {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-LegacyJava-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: folder) }
        return folder
    }
    private func target(_ directory: URL = URL(fileURLWithPath: "/PCL-Java-Fixture"), architecture: String? = "x86_64") -> InstalledVersion {
        InstalledVersion(id: "Legacy", type: "custom", requiredJava: 8, directory: directory,
                         liteLoaderVersion: "1.12.2-SNAPSHOT", requiredJavaArchitecture: architecture)
    }
    private func java(_ arch: String, major: Int = 8, patch: String = "1.8.0_492") -> JavaRuntime {
        JavaRuntime(executable: URL(fileURLWithPath: "/PCL-Java-Fixture/\(arch)-\(major)/bin/java"), majorVersion: major,
                    architecture: arch, displayName: "Java \(patch) · \(arch) · Fixture")
    }
    private func javaScript(at folder: URL, architecture: String) throws -> URL {
        let java = folder.appendingPathComponent("bin/java")
        try FileManager.default.createDirectory(at: java.deletingLastPathComponent(), withIntermediateDirectories: true)
        let script = "#!/bin/sh\nprintf '%s\\n' 'java.version = 1.8.0_492' 'os.arch = \(architecture)' 'java.vendor = PCLFixture'\n"
        try Data(script.utf8).write(to: java)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: java.path)
        return java
    }

    func testAdditionalInstalledVersionFieldsRemainSourceCompatibleAndDistinguishOptiFineFromModLoaders() {
        let folder = URL(fileURLWithPath: "/PCL-Java-Fixture")
        let vanilla = InstalledVersion(id: "Vanilla", type: "release", requiredJava: 8, directory: folder)
        XCTAssertNil(vanilla.requiredJavaArchitecture); XCTAssertNil(vanilla.liteLoaderVersion); XCTAssertNil(vanilla.optiFineVersion)
        XCTAssertFalse(vanilla.canInstallMods)
        let opti = InstalledVersion(id: "OptiFine", type: "custom", requiredJava: 8, directory: folder, optiFineVersion: "HD_U_G5")
        XCTAssertFalse(opti.canInstallMods, "Standalone OptiFine does not by itself provide a general Mod loader")
        XCTAssertTrue(target().canInstallMods, "Standalone LiteLoader must enable Mod management")
    }

    func testAutomaticAndGlobalAutomaticPreferIntelJava8EvenWhenARMIsFirst() throws {
        let arm = java("arm64"), intel = java("x86_64"), newer = java("x86_64", major: 17, patch: "17.0.14")
        var settings = VersionProfileSettings(); settings.javaMode = "auto"
        let available = [arm, newer, intel]
        XCTAssertEqual(try VersionJavaSelection(version: target(), profile: settings, globalJavaPath: "", runtimes: available).resolve(), intel)
        settings.javaMode = "global"
        XCTAssertEqual(try VersionJavaSelection(version: target(), profile: settings, globalJavaPath: "", runtimes: available).resolve(), intel)
        settings.javaMode = "auto"
        XCTAssertEqual(try VersionJavaSelection(version: target(architecture: nil), profile: settings, globalJavaPath: "", runtimes: available).resolve(), arm,
                       "Modern/unrestricted profiles keep the existing same-major ordering")
        XCTAssertThrowsError(try VersionJavaSelection(version: target(), profile: settings, globalJavaPath: "", runtimes: [arm]).resolve(fallback: arm))
    }

    func testVersionRangeAppliesArchitectureAndDoesNotUseIncompatibleFallback() throws {
        var settings = VersionProfileSettings(); settings.javaMode = "range"; settings.javaVersionRange = "[8.0.400, 9.0)"
        let arm = java("arm64"), intel = java("x86_64"), outsideRange = java("x86_64", major: 17, patch: "17.0.14")
        XCTAssertEqual(try VersionJavaSelection(version: target(), profile: settings, globalJavaPath: "", runtimes: [arm, outsideRange, intel]).resolve(), intel)
        XCTAssertThrowsError(try VersionJavaSelection(version: target(), profile: settings, globalJavaPath: "", runtimes: [arm]).resolve(fallback: outsideRange)) { error in
            XCTAssertTrue(error.localizedDescription.contains("x86_64"))
        }
    }

    func testExplicitARMPathReturnsClearErrorInsteadOfSilentlySwitchingToIntel() throws {
        let arm = java("arm64"), intel = java("x86_64")
        var settings = VersionProfileSettings(); settings.javaMode = "custom"; settings.javaPath = arm.executable.path
        XCTAssertThrowsError(try VersionJavaSelection(version: target(), profile: settings, globalJavaPath: intel.executable.path, runtimes: [arm, intel]).resolve()) { error in
            XCTAssertTrue(error.localizedDescription.contains("x86_64")); XCTAssertTrue(error.localizedDescription.contains("arm64"))
        }
        settings.javaPath = intel.executable.path
        XCTAssertEqual(try VersionJavaSelection(version: target(), profile: settings, globalJavaPath: arm.executable.path, runtimes: [arm, intel]).resolve(), intel)
        settings.javaMode = "global"
        XCTAssertThrowsError(try VersionJavaSelection(version: target(), profile: settings, globalJavaPath: arm.executable.path, runtimes: [arm, intel]).resolve())
    }

    func testFolderModeSkipsARMRuntimeAndReportsMissingIntelWithoutGlobalFallback() throws {
        let folder = try temporaryDirectory()
        _ = try javaScript(at: folder, architecture: "aarch64")
        let intel = try javaScript(at: folder.appendingPathComponent("BundledIntel"), architecture: "amd64")
        var settings = VersionProfileSettings(); settings.javaMode = "folder"
        let selected = try VersionJavaSelection(version: target(folder), profile: settings, globalJavaPath: "", runtimes: []).resolve()
        XCTAssertEqual(selected.architecture, "x86_64")
        XCTAssertEqual(selected.executable.standardizedFileURL, intel.standardizedFileURL)
        try FileManager.default.removeItem(at: intel.deletingLastPathComponent().deletingLastPathComponent())
        XCTAssertThrowsError(try VersionJavaSelection(version: target(folder), profile: settings, globalJavaPath: "", runtimes: [java("x86_64")]).resolve(fallback: java("x86_64"))) { error in
            XCTAssertTrue(error.localizedDescription.contains("x86_64"))
        }
    }

    func testScannerRecognizesInheritedLegacyNativeArchitectureAndAdditionalComponentsOffline() throws {
        let root = try temporaryDirectory()
        func write(_ id: String, _ metadata: [String: Any]) throws {
            let folder = root.appendingPathComponent("versions/" + id)
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            try JSONSerialization.data(withJSONObject: metadata).write(to: folder.appendingPathComponent(id + ".json"))
        }
        try write("1.12.2", ["id": "1.12.2", "type": "release", "libraries": [["name": "org.lwjgl.lwjgl:lwjgl:2.9.4-nightly-20150209"]]])
        let lite = LiteLoaderVersion(minecraftVersion: "1.12.2", version: "1.12.2-SNAPSHOT", filename: "liteloader-1.12.2-SNAPSHOT.jar", metadata: Data("{}".utf8), md5: String(repeating: "0", count: 32))
        let marker = try JSONSerialization.jsonObject(with: JSONEncoder().encode(lite))
        try write("Lite", ["id": "Lite", "inheritsFrom": "1.12.2", "pclLiteLoader": marker])
        try write("OldLite", ["id": "OldLite", "inheritsFrom": "1.12.2", "libraries": [["name": "com.mumfrey:liteloader:1.12.2-SNAPSHOT"]]])
        try write("OptiFine", ["id": "OptiFine", "inheritsFrom": "1.12.2", "libraries": [["name": "optifine:OptiFine:1.12.2_HD_U_G5"]]])
        try write("Modern", ["id": "Modern", "type": "release", "pclOriginalVersion": "1.20.1", "libraries": [["name": "org.lwjgl:lwjgl:3.3.1"]]])
        let versions = try LocalVersionScanner.scan(gameDirectory: root)
        let marked = try XCTUnwrap(versions.first { $0.id == "Lite" })
        XCTAssertEqual(marked.liteLoaderVersion, "1.12.2-SNAPSHOT"); XCTAssertEqual(marked.requiredJavaArchitecture, "x86_64")
        XCTAssertTrue(marked.canInstallMods); XCTAssertNil(marked.modLoader)
        XCTAssertEqual(versions.first { $0.id == "OldLite" }?.liteLoaderVersion, "1.12.2-SNAPSHOT")
        let opti = try XCTUnwrap(versions.first { $0.id == "OptiFine" })
        XCTAssertEqual(opti.optiFineVersion, "HD_U_G5"); XCTAssertEqual(opti.requiredJavaArchitecture, "x86_64"); XCTAssertFalse(opti.canInstallMods)
        XCTAssertNil(versions.first { $0.id == "Modern" }?.requiredJavaArchitecture)
    }

    func testPreviewSelectionSnapshotChangesWhenRequiredArchitectureChanges() {
        var settings = VersionProfileSettings(); settings.javaMode = "auto"
        let unrestricted = VersionJavaSelection(version: target(architecture: nil), profile: settings, globalJavaPath: "", runtimes: [java("arm64"), java("x86_64")])
        let intelOnly = VersionJavaSelection(version: target(), profile: settings, globalJavaPath: "", runtimes: [java("arm64"), java("x86_64")])
        XCTAssertNotEqual(unrestricted, intelOnly, "Java preview must recompute instead of keeping a prior ARM selection")
    }
}
