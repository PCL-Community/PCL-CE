import XCTest
@testable import PCLCore

final class VersionInheritanceTests: XCTestCase {
    private func root() throws -> URL {
        let value = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-Inheritance-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: value, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: value) }
        return value
    }

    private func profile(_ id: String, _ metadata: [String: Any], root: URL) throws {
        let directory = root.appendingPathComponent("versions/\(id)")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try JSONSerialization.data(withJSONObject: metadata).write(to: directory.appendingPathComponent("\(id).json"))
    }

    func testFabricProfileInheritsGameArgumentsJavaAssetsAndClientJar() throws {
        let directory = try root()
        try profile("1.20.4", ["id": "1.20.4", "mainClass": "net.minecraft.client.main.Main",
            "javaVersion": ["majorVersion": 17], "assetIndex": ["id": "12"],
            "libraries": [["name": "example:shared:1"], ["name": "org.lwjgl:lwjgl:3:natives-macos-arm64"]],
            "arguments": ["game": ["--username", "${auth_player_name}"], "jvm": ["-cp", "${classpath}"]]], root: directory)
        try profile("我的 Fabric", ["id": "我的 Fabric", "inheritsFrom": "1.20.4", "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
            "libraries": [["name": "example:shared:2"], ["name": "net.fabricmc:fabric-loader:0.16.14"]],
            "arguments": ["jvm": ["-Dfabric.example=true"]]], root: directory)
        let resolved = try MinecraftVersionMetadata.resolve(versionID: "我的 Fabric", gameDirectory: directory)
        XCTAssertEqual(resolved["mainClass"] as? String, "net.fabricmc.loader.impl.launch.knot.KnotClient")
        XCTAssertEqual(resolved["jar"] as? String, "1.20.4")
        XCTAssertEqual(resolved["pclOriginalVersion"] as? String, "1.20.4")
        XCTAssertEqual(MinecraftVersionMetadata.requiredJava(resolved), 17)
        XCTAssertEqual((resolved["assetIndex"] as? [String: String])?["id"], "12")
        XCTAssertEqual((resolved["libraries"] as? [[String: String]])?.compactMap { $0["name"] },
                       ["example:shared:2", "net.fabricmc:fabric-loader:0.16.14", "org.lwjgl:lwjgl:3:natives-macos-arm64"])
        let arguments = try XCTUnwrap(resolved["arguments"] as? [String: Any])
        XCTAssertEqual(arguments["jvm"] as? [String], ["-cp", "${classpath}", "-Dfabric.example=true"])
        XCTAssertEqual(arguments["game"] as? [String], ["--username", "${auth_player_name}"])
        XCTAssertNil(try MinecraftVersionMetadata.load(versionID: "我的 Fabric", gameDirectory: directory)["jar"])
    }

    func testWindowsOnlyLibraryOverrideDoesNotRemoveMacDependency() throws {
        let directory = try root()
        try profile("base", ["id": "base", "libraries": [["name": "example:platform:1"]]], root: directory)
        try profile("child", ["id": "child", "inheritsFrom": "base", "libraries": [["name": "example:platform:2",
            "rules": [["action": "allow", "os": ["name": "windows"]]]]]], root: directory)
        let libraries = try XCTUnwrap(MinecraftVersionMetadata.resolve(versionID: "child", gameDirectory: directory)["libraries"] as? [[String: Any]])
        XCTAssertEqual(libraries.filter { InstallPlatform.libraryAllowed($0) }.compactMap { $0["name"] as? String }, ["example:platform:1"])
    }

    func testInheritanceCycleAndEscapingParentAreRejected() throws {
        let directory = try root()
        try profile("a", ["id": "a", "inheritsFrom": "b"], root: directory)
        try profile("b", ["id": "b", "inheritsFrom": "a"], root: directory)
        XCTAssertThrowsError(try MinecraftVersionMetadata.resolve(versionID: "a", gameDirectory: directory))
        try profile("escape", ["id": "escape", "inheritsFrom": "../outside"], root: directory)
        XCTAssertThrowsError(try MinecraftVersionMetadata.resolve(versionID: "escape", gameDirectory: directory))
        try profile("bad", ["id": "bad", "inheritsFrom": 12], root: directory)
        XCTAssertThrowsError(try MinecraftVersionMetadata.resolve(versionID: "bad", gameDirectory: directory))
    }

    func testLegacyForgeArgumentsReplaceInheritedModernGameArguments() throws {
        let directory = try root()
        try profile("base", ["id": "1.12.2", "arguments": ["game": ["--old"], "jvm": ["-cp", "${classpath}"]]], root: directory)
        try profile("forge", ["id": "forge", "inheritsFrom": "base", "minecraftArguments": "--tweakClass net.minecraftforge.fml.common.launcher.FMLTweaker"], root: directory)
        let resolved = try MinecraftVersionMetadata.resolve(versionID: "forge", gameDirectory: directory)
        let args = try XCTUnwrap(resolved["arguments"] as? [String: Any])
        XCTAssertNil(args["game"])
        XCTAssertEqual(args["jvm"] as? [String], ["-cp", "${classpath}"])
        XCTAssertTrue((resolved["minecraftArguments"] as? String ?? "").contains("FMLTweaker"))
    }

    func testLaunchPlanUsesParentJarAndChildMainClass() throws {
        let directory = try root()
        let java = directory.appendingPathComponent("java")
        try Data("#!/bin/sh\necho 'java.version = 17.0.1'\necho 'os.arch = \(InstallPlatform.architecture)'\necho 'java.vendor = Test'\n".utf8).write(to: java)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: java.path)
        try profile("1.20.4", ["id": "1.20.4", "javaVersion": ["majorVersion": 17], "libraries": [],
            "mainClass": "net.minecraft.client.main.Main", "arguments": ["jvm": ["-cp", "${classpath}"], "game": ["--username", "${auth_player_name}"]]], root: directory)
        try Data("client".utf8).write(to: directory.appendingPathComponent("versions/1.20.4/1.20.4.jar"))
        try profile("fabric-test", ["id": "fabric-test", "inheritsFrom": "1.20.4", "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient"], root: directory)
        let plan = try MinecraftLauncher.makeLaunchPlan(configuration: LaunchConfiguration(gameDirectory: directory,
            versionID: "fabric-test", playerName: "Player", javaExecutable: java, memoryMB: 1024))
        XCTAssertTrue(plan.arguments.contains("net.fabricmc.loader.impl.launch.knot.KnotClient"))
        let client = try MinecraftVersionMetadata.safeFile(relativePath: "versions/1.20.4/1.20.4.jar", under: directory)
        XCTAssertTrue(plan.arguments.contains(client.path))
        XCTAssertFalse(plan.arguments.contains(where: { $0.contains("fabric-test.jar") }))
    }

    func testRenameInheritedProfilePreservesBaseAndPreventsRenamingDependency() throws {
        let directory = try root()
        try profile("1.20.4", ["id": "1.20.4", "javaVersion": ["majorVersion": 17]], root: directory)
        try profile("old", ["id": "old", "inheritsFrom": "1.20.4", "mainClass": "example.Loader"], root: directory)
        let child = InstalledVersion(id: "old", type: "release", requiredJava: 17, directory: directory.appendingPathComponent("versions/old"), inheritsFrom: "1.20.4")
        try VersionManager.rename(version: child, to: "新加载器", gameDirectory: directory)
        let metadata = try MinecraftVersionMetadata.load(versionID: "新加载器", gameDirectory: directory)
        XCTAssertEqual(metadata["inheritsFrom"] as? String, "1.20.4")
        XCTAssertEqual(metadata["pclOriginalVersion"] as? String, "1.20.4")
        let base = InstalledVersion(id: "1.20.4", type: "release", requiredJava: 17, directory: directory.appendingPathComponent("versions/1.20.4"))
        XCTAssertThrowsError(try VersionManager.rename(version: base, to: "renamed", gameDirectory: directory))
    }

    func testMavenClassifierExtensionsAreParsedCorrectly() {
        XCTAssertEqual(MinecraftVersionMetadata.libraryPath(["name": "group:artifact:1.0:natives@zip"]), "group/artifact/1.0/artifact-1.0-natives.zip")
        XCTAssertEqual(MinecraftVersionMetadata.libraryPath(["name": "group:artifact:1.0@zip"]), "group/artifact/1.0/artifact-1.0.zip")
        XCTAssertNil(MinecraftVersionMetadata.libraryPath(["name": ""]))
    }
}
