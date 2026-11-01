import XCTest
@testable import PCLCore

final class LauncherTests: XCTestCase {
    private func temporaryDirectory() throws -> URL {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-LauncherTests-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: directory) }
        return directory
    }

    func testOfflineUUIDMatchesOriginalPCL() {
        XCTAssertEqual(MinecraftArguments.offlineUUID(for: "Notch"), "0000000000003005998F503098015B54")
        XCTAssertEqual(MinecraftArguments.offlineUUID(for: "Steve"), "0000000000003005998F503099DCF29B")
    }

    func testLegacySubstitutionPreservesPathsContainingSpaces() throws {
        let root = try temporaryDirectory().appendingPathComponent("Game Directory")
        let config = LaunchConfiguration(gameDirectory: root, versionID: "1.12.2", playerName: "Player", javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), memoryMB: 2048)
        let metadata: [String: Any] = ["id": "1.12.2", "type": "release", "mainClass": "net.minecraft.client.main.Main", "minecraftArguments": "--username ${auth_player_name} --gameDir ${game_directory} --uuid ${auth_uuid} --userProperties ${user_properties}"]
        let arguments = try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [root.appendingPathComponent("test.jar")], nativesDirectory: root.appendingPathComponent("natives"))
        let gameDirectoryIndex = try XCTUnwrap(arguments.firstIndex(of: "--gameDir"))
        XCTAssertEqual(arguments[gameDirectoryIndex + 1], root.path)
        XCTAssertTrue(arguments.contains("-XstartOnFirstThread"))
        XCTAssertFalse(arguments.contains(where: { $0.contains("${") }))
        let propertiesIndex = try XCTUnwrap(arguments.firstIndex(of: "--userProperties"))
        XCTAssertEqual(arguments[propertiesIndex + 1], "{}")
    }

    func testModernFeatureRulesOnlyIncludeCustomResolution() throws {
        let variables = ["resolution_width": "1100", "resolution_height": "700"]
        let arguments: [Any] = [
            ["rules": [["action": "allow", "features": ["is_demo_user": true]]], "value": "--demo"],
            ["rules": [["action": "allow", "features": ["has_custom_resolution": true]]], "value": ["--width", "${resolution_width}", "--height", "${resolution_height}"]]
        ]
        XCTAssertEqual(try MinecraftArguments.resolve(arguments, variables: variables, features: ["has_custom_resolution": true]), ["--width", "1100", "--height", "700"])
    }

    func testUnresolvedVariablesAndMalformedQuotesAreErrors() {
        XCTAssertThrowsError(try MinecraftArguments.resolve(["${unknown_variable}"], variables: [:], features: [:]))
        XCTAssertThrowsError(try MinecraftArguments.tokenize("-Dexample=\"unterminated"))
        XCTAssertThrowsError(try MinecraftArguments.tokenize("tail\\"))
    }

    func testExtraJVMArgumentsAreParsedWithoutShellExpansion() throws {
        let parsed = try MinecraftArguments.tokenize(#"-Dsome.path="/a path/with spaces" '-Dliteral=$(touch forbidden)' -XX:+UseG1GC"#)
        XCTAssertEqual(parsed, ["-Dsome.path=/a path/with spaces", "-Dliteral=$(touch forbidden)", "-XX:+UseG1GC"])
    }

    func testPathsCannotEscapeLibraryRoot() throws {
        let root = try temporaryDirectory()
        XCTAssertThrowsError(try MinecraftVersionMetadata.safeFile(relativePath: "../outside.jar", under: root))
        XCTAssertThrowsError(try MinecraftVersionMetadata.safeFile(relativePath: "/tmp/outside.jar", under: root))
        XCTAssertThrowsError(try MinecraftVersionMetadata.validateVersionID("../version"))
        let safe = try MinecraftVersionMetadata.safeFile(relativePath: "org/example/test.jar", under: root)
        try FileManager.default.createDirectory(at: safe.deletingLastPathComponent(), withIntermediateDirectories: true)
        try Data("inside-root".utf8).write(to: safe)
        XCTAssertEqual(try Data(contentsOf: root.appendingPathComponent("org/example/test.jar")), Data("inside-root".utf8))
        let external = try temporaryDirectory()
        try FileManager.default.createSymbolicLink(at: root.appendingPathComponent("linked"), withDestinationURL: external)
        XCTAssertThrowsError(try MinecraftVersionMetadata.safeFile(relativePath: "linked/outside.jar", under: root))
    }

    func testScannerReadsMetadataAndIgnoresMalformedEntries() throws {
        let root = try temporaryDirectory()
        let versions = root.appendingPathComponent("versions")
        for id in ["1.20.4", "1.21.1", "bad"] {
            try FileManager.default.createDirectory(at: versions.appendingPathComponent(id), withIntermediateDirectories: true)
        }
        let java17: [String: Any] = ["id": "1.20.4", "type": "release", "javaVersion": ["majorVersion": 17]]
        let java21: [String: Any] = ["id": "1.21.1", "type": "release", "javaVersion": ["majorVersion": 21]]
        try JSONSerialization.data(withJSONObject: java17).write(to: versions.appendingPathComponent("1.20.4/1.20.4.json"))
        try JSONSerialization.data(withJSONObject: java21).write(to: versions.appendingPathComponent("1.21.1/1.21.1.json"))
        try Data("malformed".utf8).write(to: versions.appendingPathComponent("bad/bad.json"))
        let installed = try LocalVersionScanner.scan(gameDirectory: root)
        XCTAssertEqual(installed.map(\.id), ["1.21.1", "1.20.4"])
        XCTAssertEqual(installed.map(\.requiredJava), [21, 17])
    }

    func testRequiredJavaFallbackForOlderMetadata() {
        XCTAssertEqual(MinecraftVersionMetadata.requiredJava(["id": "1.16.5"]), 8)
        XCTAssertEqual(MinecraftVersionMetadata.requiredJava(["id": "1.17.1"]), 16)
        XCTAssertEqual(MinecraftVersionMetadata.requiredJava(["id": "1.20.4"]), 17)
        XCTAssertEqual(MinecraftVersionMetadata.requiredJava(["id": "1.20.5"]), 21)
        XCTAssertEqual(MinecraftVersionMetadata.requiredJava(["id": "26.1", "javaVersion": ["majorVersion": 25]]), 25)
    }

    func testMavenCoordinateFallbackAndArtifactPath() {
        XCTAssertEqual(MinecraftVersionMetadata.libraryPath(["name": "org.lwjgl:lwjgl:3.3.3"]), "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar")
        XCTAssertEqual(MinecraftVersionMetadata.libraryPath(["name": "org.lwjgl:lwjgl:3.3.3:natives-macos-arm64"]), "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-macos-arm64.jar")
        XCTAssertEqual(MinecraftVersionMetadata.libraryPath(["name": "unused", "downloads": ["artifact": ["path": "exact/path.jar"]]]), "exact/path.jar")
    }

    func testAssetIndexCannotEscapeOrFollowExternalIndexLink() throws {
        let root = try temporaryDirectory()
        let config = LaunchConfiguration(gameDirectory: root, versionID: "pack", playerName: "Player", javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), memoryMB: 1024)
        var metadata: [String: Any] = ["id": "pack", "mainClass": "example.Main", "arguments": ["game": []]]
        for id in ["../outside", "/tmp/index", "nested/index", ".."] {
            metadata["assetIndex"] = ["id": id]
            XCTAssertThrowsError(try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root))
        }
        try FileManager.default.createDirectory(at: root.appendingPathComponent("assets"), withIntermediateDirectories: true)
        let external = try temporaryDirectory()
        try FileManager.default.createSymbolicLink(at: root.appendingPathComponent("assets/indexes"), withDestinationURL: external)
        metadata["assetIndex"] = ["id": "12"]
        XCTAssertThrowsError(try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root))
    }

    func testNativeCleanupRejectsInternalSymlinkWithoutDeletingUserFiles() throws {
        let root = try temporaryDirectory()
        let java = root.appendingPathComponent("test-java")
        try Data("#!/bin/sh\necho 'java.version = 17.0.1'\necho 'os.arch = aarch64'\n".utf8).write(to: java)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: java.path)
        let directory = root.appendingPathComponent("versions/pack")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try JSONSerialization.data(withJSONObject: ["id": "pack", "javaVersion": ["majorVersion": 17], "mainClass": "example.Main", "arguments": ["game": []]]).write(to: directory.appendingPathComponent("pack.json"))
        let userFiles = root.appendingPathComponent("user-files")
        try FileManager.default.createDirectory(at: userFiles, withIntermediateDirectories: true)
        let importantFile = userFiles.appendingPathComponent("keep.dylib")
        try Data("keep".utf8).write(to: importantFile)
        let nativesLink = directory.appendingPathComponent("natives-macos-arm64")
        try FileManager.default.createSymbolicLink(at: nativesLink, withDestinationURL: userFiles)
        let config = LaunchConfiguration(gameDirectory: root, versionID: "pack", playerName: "Player", javaExecutable: java, memoryMB: 1024)
        XCTAssertThrowsError(try MinecraftLauncher.makeLaunchPlan(configuration: config))
        XCTAssertEqual(try Data(contentsOf: importantFile), Data("keep".utf8))
        try FileManager.default.removeItem(at: nativesLink)
        let alias = root.appendingPathComponent("versions/linked-pack")
        try FileManager.default.createSymbolicLink(at: alias, withDestinationURL: directory)
        try FileManager.default.copyItem(at: directory.appendingPathComponent("pack.json"), to: directory.appendingPathComponent("linked-pack.json"))
        var linkedConfig = config
        linkedConfig.versionID = "linked-pack"
        XCTAssertThrowsError(try MinecraftLauncher.makeLaunchPlan(configuration: linkedConfig))
        XCTAssertFalse(FileManager.default.fileExists(atPath: directory.appendingPathComponent("natives-macos-arm64").path))
    }

    func testActualProcessStreamsUTF8AndReportsExit() throws {
        let root = try temporaryDirectory()
        let java = root.appendingPathComponent("test-java")
        let script = """
        #!/bin/sh
        if [ "$1" = "-XshowSettings:properties" ]; then
            echo 'java.version = 17.0.1'
            echo 'os.arch = aarch64'
            echo 'java.vendor = PCLTest'
            exit 0
        fi
        echo 'Minecraft 输出测试'
        exit 0
        """
        try Data(script.utf8).write(to: java)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: java.path)
        let version = root.appendingPathComponent("versions/test-version")
        try FileManager.default.createDirectory(at: version, withIntermediateDirectories: true)
        let metadata: [String: Any] = ["id": "test-version", "type": "release", "javaVersion": ["majorVersion": 17], "mainClass": "example.Test", "arguments": ["jvm": [], "game": []], "libraries": []]
        try JSONSerialization.data(withJSONObject: metadata).write(to: version.appendingPathComponent("test-version.json"))
        try Data().write(to: version.appendingPathComponent("test-version.jar"))
        let receivedOutput = expectation(description: "UTF8 game process output")
        let exited = expectation(description: "Game process exit")
        let launcher = MinecraftLauncher()
        try launcher.launch(configuration: LaunchConfiguration(gameDirectory: root, versionID: "test-version", playerName: "Player", javaExecutable: java, memoryMB: 512), onOutput: { line in
            if line == "Minecraft 输出测试" { receivedOutput.fulfill() }
        }, onExit: { status in
            XCTAssertEqual(status, 0)
            exited.fulfill()
        })
        wait(for: [receivedOutput, exited], timeout: 8)
        XCTAssertFalse(launcher.isRunning)
    }
    func testExtraGameArgumentsCustomInfoAndModernServerReachLaunchArguments() throws {
        let root = try temporaryDirectory()
        let config = LaunchConfiguration(gameDirectory: root, versionID: "My Pack", playerName: "Player", javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), memoryMB: 2048,
            extraGameArguments: #"--demo --resourcePackDir "${game_directory}/custom packs" '--literal=$(touch forbidden)'"#,
            serverAddress: "[::1]:25566", customInfo: "My Pack 1.0")
        let metadata: [String: Any] = ["id": "My Pack", "pclOriginalVersion": "1.20.4", "type": "release", "mainClass": "example.Main", "arguments": ["jvm": [], "game": ["--versionType", "${version_type}"]]]
        let args = try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root)
        XCTAssertTrue(args.contains("My Pack 1.0")); XCTAssertTrue(args.contains(root.appendingPathComponent("custom packs").path))
        XCTAssertTrue(args.contains("--literal=$(touch forbidden)")); XCTAssertTrue(args.contains("--demo"))
        let server = try XCTUnwrap(args.firstIndex(of: "--quickPlayMultiplayer")); XCTAssertEqual(args[server + 1], "[::1]:25566")
        XCTAssertFalse(args.contains("--server")); XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("forbidden").path))
    }

    func testLegacyServerUsesHostPortAndSnapshotUsesReleaseTime() throws {
        let root = try temporaryDirectory()
        let config = LaunchConfiguration(gameDirectory: root, versionID: "1.19.4", playerName: "Player", javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), memoryMB: 1024, serverAddress: "example.org:25566")
        var metadata: [String: Any] = ["id": "1.19.4", "mainClass": "example.Main", "releaseTime": "2023-03-14T10:00:00+00:00", "minecraftArguments": "--versionType ${version_type}"]
        let legacy = try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root)
        let server = try XCTUnwrap(legacy.firstIndex(of: "--server")); XCTAssertEqual(Array(legacy[server...]), ["--server", "example.org", "--port", "25566"])
        metadata["id"] = "23w14a"; metadata["releaseTime"] = "2023-04-05T12:00:00+00:00"
        let snapshot = try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root)
        XCTAssertTrue(snapshot.contains("--quickPlayMultiplayer")); XCTAssertFalse(snapshot.contains("--server"))
    }

    func testCollectorOverrideAvoidsConflictsAndHandlesNewerJava() throws {
        let root = try temporaryDirectory()
        var config = LaunchConfiguration(gameDirectory: root, versionID: "1.20.4", playerName: "Player", javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), memoryMB: 1024,
            extraJVMArguments: "-XX:+UseSerialGC -Dkept=true", garbageCollector: "zgc", javaMajorVersion: 21)
        let metadata: [String: Any] = ["id": "1.20.4", "mainClass": "example.Main", "arguments": ["jvm": ["-XX:+UseG1GC"], "game": []]]
        let zgc = try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root)
        XCTAssertTrue(zgc.contains("-XX:+UseZGC")); XCTAssertTrue(zgc.contains("-XX:+ZGenerational"))
        XCTAssertFalse(zgc.contains("-XX:+UseG1GC")); XCTAssertFalse(zgc.contains("-XX:+UseSerialGC")); XCTAssertTrue(zgc.contains("-Dkept=true"))
        config.javaMajorVersion = 25
        XCTAssertFalse(try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root).contains("-XX:+ZGenerational"))
        config.javaMajorVersion = 17; config.garbageCollector = "generational-zgc"
        XCTAssertTrue(try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root).contains("-XX:+UseG1GC"))
        config.garbageCollector = "none"
        let custom = try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root)
        XCTAssertFalse(custom.contains("-XX:+UseG1GC")); XCTAssertTrue(custom.contains("-XX:+UseSerialGC"))
    }

    func testOriginalG1TuningReplacesExistingGcTuning() throws {
        let root = try temporaryDirectory()
        let config = LaunchConfiguration(gameDirectory: root, versionID: "1.20.4", playerName: "Player", javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), memoryMB: 1024,
            extraJVMArguments: "-XX:G1ReservePercent=5", garbageCollector: "g1tuned", javaMajorVersion: 17)
        let metadata: [String: Any] = ["id": "1.20.4", "mainClass": "example.Main", "arguments": ["jvm": ["-XX:MaxGCPauseMillis=200", "-XX:+UseParallelGC"], "game": []]]
        let args = try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root)
        XCTAssertTrue(args.contains("-XX:+UnlockExperimentalVMOptions")); XCTAssertTrue(args.contains("-XX:+UseG1GC"))
        XCTAssertTrue(args.contains("-XX:G1ReservePercent=20")); XCTAssertTrue(args.contains("-XX:MaxGCPauseMillis=50"))
        XCTAssertTrue(args.contains("-XX:MinHeapFreeRatio=25")); XCTAssertTrue(args.contains("-XX:MaxHeapFreeRatio=40"))
        XCTAssertFalse(args.contains("-XX:MaxGCPauseMillis=200")); XCTAssertFalse(args.contains("-XX:G1ReservePercent=5")); XCTAssertFalse(args.contains("-XX:+UseParallelGC"))
    }

}
