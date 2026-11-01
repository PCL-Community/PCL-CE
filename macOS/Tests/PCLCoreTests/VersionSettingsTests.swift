import Foundation
import XCTest
@testable import PCLCore

final class VersionSettingsTests: XCTestCase {
    private func temporaryDirectory() throws -> URL {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-VersionSettings-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        return root
    }

    private func profile(_ id: String = "1.20.4", root: URL, extra: [String: Any] = [:]) throws -> InstalledVersion {
        let directory = root.appendingPathComponent("versions/\(id)")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        var metadata: [String: Any] = ["id": id, "type": "release", "mainClass": "example.Main", "javaVersion": ["majorVersion": 17],
                                     "libraries": [], "arguments": ["jvm": [], "game": ["--gameDir", "${game_directory}", "--assetsDir", "${assets_root}", "--width", "${resolution_width}", "--height", "${resolution_height}"]]]
        metadata.merge(extra) { _, value in value }
        try JSONSerialization.data(withJSONObject: metadata).write(to: directory.appendingPathComponent("\(id).json"))
        try Data("client fixture".utf8).write(to: directory.appendingPathComponent("\(id).jar"))
        return InstalledVersion(id: id, type: "release", requiredJava: 17, directory: directory)
    }

    func testSettingsRoundTripResetAndCorruptionAreExplicit() throws {
        let root = try temporaryDirectory(), version = try profile(root: root)
        XCTAssertEqual(try VersionSettingsRepository.load(version: version), VersionProfileSettings())
        let oldINI = version.directory.appendingPathComponent("PCL.ini")
        try Data("original Windows settings".utf8).write(to: oldINI)
        var settings = VersionProfileSettings()
        settings.displayDescription = "独立存档测试"
        settings.iconName = "GoldBlock"
        settings.category = "rare"
        settings.isFavorite = true
        settings.useGlobalLaunchSettings = false
        settings.memoryMB = 3072
        settings.extraJVMArguments = "-Dsome.path=\"a path\""
        settings.isolateGameDirectory = true
        settings.width = 1280; settings.height = 720
        try VersionSettingsRepository.save(settings, for: version)
        XCTAssertEqual(try VersionSettingsRepository.load(version: version), settings)
        XCTAssertEqual(try String(contentsOf: oldINI), "original Windows settings")
        try VersionSettingsRepository.reset(version: version)
        XCTAssertEqual(try VersionSettingsRepository.load(version: version), VersionProfileSettings())
        XCTAssertTrue(FileManager.default.fileExists(atPath: oldINI.path))
        let settingsFile = try VersionSettingsRepository.settingsURL(version: version)
        try Data("corrupt JSON".utf8).write(to: settingsFile)
        XCTAssertThrowsError(try VersionSettingsRepository.load(version: version))
    }

    func testInvalidSettingsAndEscapingSymlinkNeverWriteOutsideProfile() throws {
        let root = try temporaryDirectory(), version = try profile(root: root)
        var settings = VersionProfileSettings()
        settings.width = 1
        XCTAssertThrowsError(try VersionSettingsRepository.save(settings, for: version))
        settings = VersionProfileSettings(); settings.extraJVMArguments = "-Dvalue=\"unterminated"
        XCTAssertThrowsError(try VersionSettingsRepository.save(settings, for: version))
        let external = try temporaryDirectory()
        try FileManager.default.createSymbolicLink(at: version.directory.appendingPathComponent("PCL"), withDestinationURL: external)
        XCTAssertThrowsError(try VersionSettingsRepository.save(VersionProfileSettings(), for: version))
        XCTAssertFalse(FileManager.default.fileExists(atPath: external.appendingPathComponent("macos-settings.json").path))
        let internalLink = try profile("internal-link", root: root)
        let target = internalLink.directory.appendingPathComponent("keep.json")
        try Data("do not delete".utf8).write(to: target)
        let linkedSettings = internalLink.directory.appendingPathComponent("PCL/macos-settings.json")
        try FileManager.default.createDirectory(at: linkedSettings.deletingLastPathComponent(), withIntermediateDirectories: true)
        try FileManager.default.createSymbolicLink(at: linkedSettings, withDestinationURL: target)
        XCTAssertThrowsError(try VersionSettingsRepository.reset(version: internalLink))
        XCTAssertEqual(try String(contentsOf: target), "do not delete")
    }

    func testRenamePreservesClientSettingsWorldsAndOriginalMinecraftVersion() throws {
        let root = try temporaryDirectory(), version = try profile(root: root, extra: ["jar": "1.20.4"])
        var settings = VersionProfileSettings(); settings.isFavorite = true; settings.displayDescription = "keep me"
        try VersionSettingsRepository.save(settings, for: version)
        let save = version.directory.appendingPathComponent("saves/Test World/level.dat")
        try FileManager.default.createDirectory(at: save.deletingLastPathComponent(), withIntermediateDirectories: true)
        try Data("world fixture".utf8).write(to: save)
        let shared = root.appendingPathComponent("libraries/shared.jar")
        try FileManager.default.createDirectory(at: shared.deletingLastPathComponent(), withIntermediateDirectories: true)
        try Data("shared library".utf8).write(to: shared)
        let newID = try VersionManager.rename(version: version, to: "我的原版 1.20.4", gameDirectory: root)
        let renamed = try XCTUnwrap(LocalVersionScanner.scan(gameDirectory: root).first { $0.id == newID })
        let metadata = try MinecraftVersionMetadata.load(versionID: newID, gameDirectory: root)
        XCTAssertEqual(metadata["id"] as? String, newID)
        XCTAssertEqual(metadata["pclOriginalVersion"] as? String, "1.20.4")
        XCTAssertEqual(metadata["jar"] as? String, newID)
        XCTAssertFalse(FileManager.default.fileExists(atPath: version.directory.path))
        XCTAssertEqual(try String(contentsOf: renamed.directory.appendingPathComponent("\(newID).jar")), "client fixture")
        XCTAssertEqual(try String(contentsOf: renamed.directory.appendingPathComponent("saves/Test World/level.dat")), "world fixture")
        XCTAssertEqual(try String(contentsOf: shared), "shared library")
        XCTAssertEqual(try VersionSettingsRepository.load(version: renamed), settings)
    }

    func testRenameRejectsCollisionDependencyAndTraversalWithoutMovingOriginal() throws {
        let root = try temporaryDirectory(), version = try profile(root: root)
        _ = try profile("already there", root: root)
        XCTAssertThrowsError(try VersionManager.rename(version: version, to: "already there", gameDirectory: root))
        XCTAssertThrowsError(try VersionManager.rename(version: version, to: "../escaped", gameDirectory: root))
        for invalid in [".hidden", "Hello!", "Hello;", "name?", "trailing.", "outer "] {
            XCTAssertThrowsError(try VersionManager.validateName(invalid))
        }
        _ = try profile("dependent", root: root, extra: ["inheritsFrom": version.id])
        XCTAssertThrowsError(try VersionManager.rename(version: version, to: "renamed", gameDirectory: root))
        XCTAssertTrue(FileManager.default.fileExists(atPath: version.directory.appendingPathComponent("1.20.4.jar").path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/renamed").path))
    }

    func testCaseOnlyRenameUsesNewJSONAndJarNamesWithoutDataLoss() throws {
        let root = try temporaryDirectory(), version = try profile("Vanilla", root: root, extra: ["pclOriginalVersion": "1.20.4"])
        let id = try VersionManager.rename(version: version, to: "vanilla", gameDirectory: root)
        let directory = root.appendingPathComponent("versions/\(id)")
        let files = try FileManager.default.contentsOfDirectory(atPath: directory.path)
        XCTAssertTrue(files.contains("vanilla.json"))
        XCTAssertTrue(files.contains("vanilla.jar"))
        XCTAssertFalse(files.contains("Vanilla.json"))
        XCTAssertFalse(files.contains("Vanilla.jar"))
        XCTAssertEqual(try String(contentsOf: directory.appendingPathComponent("vanilla.jar")), "client fixture")
        let metadata = try MinecraftVersionMetadata.load(versionID: id, gameDirectory: root)
        XCTAssertEqual(metadata["pclOriginalVersion"] as? String, "1.20.4")
    }

    func testExportedScriptPreservesArgumentsWithoutShellExpansion() throws {
        let root = try temporaryDirectory()
        let program = root.appendingPathComponent("test executable")
        try Data("#!/bin/sh\nprintf '%s\\n' \"$PWD\" \"$@\"\n".utf8).write(to: program)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: program.path)
        let work = root.appendingPathComponent("game's world")
        try FileManager.default.createDirectory(at: work, withIntermediateDirectories: true)
        let arguments = ["a b", "it's quoted", "$(touch SHELL_EXPANDED)", "; touch INJECTED"]
        let plan = LaunchPlan(executable: program, arguments: arguments, workingDirectory: work)
        let script = root.appendingPathComponent("launch.command")
        try VersionManager.exportLaunchScript(plan: plan, to: script)
        let output = try NativeProcess.capture(script, [], timeout: 5)
        XCTAssertEqual(output.status, 0)
        let lines = output.text.split(separator: "\n").map(String.init)
        XCTAssertEqual(Array(lines.dropFirst()), arguments)
        XCTAssertEqual(URL(fileURLWithPath: try XCTUnwrap(lines.first)).resolvingSymlinksInPath(), work.resolvingSymlinksInPath())
        XCTAssertFalse(FileManager.default.fileExists(atPath: work.appendingPathComponent("SHELL_EXPANDED").path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: work.appendingPathComponent("INJECTED").path))
    }

    func testIsolatedLaunchKeepsSharedAssetsAndLibrariesAndChangesWorkingDirectory() throws {
        let root = try temporaryDirectory(), version = try profile(root: root)
        let java = root.appendingPathComponent("fake-java")
        try Data("#!/bin/sh\necho 'java.version = 17.0.1'\necho 'os.arch = \(InstallPlatform.architecture)'\necho 'java.vendor = PCLTest'\n".utf8).write(to: java)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: java.path)
        let config = LaunchConfiguration(gameDirectory: root, versionID: version.id, playerName: "Player", javaExecutable: java,
                                         memoryMB: 2048, width: 1280, height: 720, extraJVMArguments: "-Dtest.override=true",
                                         runtimeGameDirectory: version.directory)
        let plan = try MinecraftLauncher.makeLaunchPlan(configuration: config)
        let gameIndex = try XCTUnwrap(plan.arguments.firstIndex(of: "--gameDir"))
        let assetsIndex = try XCTUnwrap(plan.arguments.firstIndex(of: "--assetsDir"))
        XCTAssertEqual(URL(fileURLWithPath: plan.arguments[gameIndex + 1]).resolvingSymlinksInPath(), version.directory.resolvingSymlinksInPath())
        XCTAssertEqual(plan.arguments[assetsIndex + 1], try MinecraftVersionMetadata.safeFile(relativePath: "assets", under: root).path)
        XCTAssertEqual(plan.workingDirectory.resolvingSymlinksInPath(), version.directory.resolvingSymlinksInPath())
        XCTAssertTrue(plan.arguments.contains("-Xmx2048M"))
        XCTAssertTrue(plan.arguments.contains("-Dtest.override=true"))
        XCTAssertTrue(plan.arguments.contains("1280"))
        XCTAssertTrue(plan.arguments.contains("720"))
        var escaping = config
        escaping.runtimeGameDirectory = try temporaryDirectory()
        XCTAssertThrowsError(try MinecraftLauncher.makeLaunchPlan(configuration: escaping))
    }

    func testCustomProfileCannotBypassOriginalVersionUsernameLimit() throws {
        let root = try temporaryDirectory(), version = try profile("My Alias", root: root, extra: ["pclOriginalVersion": "1.20.4"])
        let config = LaunchConfiguration(gameDirectory: root, versionID: version.id, playerName: "12345678901234567", javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), memoryMB: 1024)
        XCTAssertThrowsError(try MinecraftLauncher.makeLaunchPlan(configuration: config)) { error in
            XCTAssertEqual(error as? OfflineLoginError, .nameTooLong)
        }
    }
    func testIndependentSettingsMigrateLegacyProfilesWithoutCouplingModes() throws {
        let data = Data(#"{"useGlobalLaunchSettings":false,"memoryMB":3072,"javaPath":"/custom/java","extraJVMArguments":"-Dkept=true","width":960,"height":600}"#.utf8)
        var settings = try JSONDecoder().decode(VersionProfileSettings.self, from: data)
        XCTAssertEqual(settings.memoryConfigurationMode, "custom")
        XCTAssertEqual(settings.javaConfigurationMode, "custom")
        XCTAssertEqual(settings.jvmConfigurationMode, "custom")
        XCTAssertEqual(settings.windowConfigurationMode, "custom")
        settings.memoryMode = "global"; settings.javaMode = "auto"
        settings.extraGameArguments = "--demo"; settings.customInfo = "My Pack"; settings.serverAddress = "example.org:25566"
        let restored = try JSONDecoder().decode(VersionProfileSettings.self, from: JSONEncoder().encode(settings))
        XCTAssertEqual(restored, settings)
        XCTAssertEqual(restored.memoryConfigurationMode, "global")
        XCTAssertEqual(restored.javaConfigurationMode, "auto")
        XCTAssertEqual(restored.jvmConfigurationMode, "custom")
        XCTAssertEqual(restored.windowConfigurationMode, "custom")
        XCTAssertEqual(restored.memoryMB, 3072); XCTAssertEqual(restored.extraJVMArguments, "-Dkept=true")
        try restored.validate()
    }

    func testAutomaticMemoryRespectsAvailableBudgetAndModCount() {
        var settings = VersionProfileSettings(); settings.memoryMode = "auto"
        XCTAssertEqual(settings.resolvedMemoryMB(globalValue: 2048, availableMemoryMB: 16384, modCount: 0), 3072)
        XCTAssertEqual(settings.resolvedMemoryMB(globalValue: 2048, availableMemoryMB: 16384, modCount: 300), 8192)
        XCTAssertLessThanOrEqual(settings.resolvedMemoryMB(globalValue: 2048, availableMemoryMB: 4096, modCount: 300), 3072)
        XCTAssertEqual(settings.resolvedMemoryMB(globalValue: 2048, availableMemoryMB: 1024, modCount: 300), 512)
        settings.memoryMode = "global"
        XCTAssertEqual(settings.resolvedMemoryMB(globalValue: 6144, availableMemoryMB: 1024, modCount: 300), 6144)
    }

    func testJavaRangeUsesInclusiveExclusiveAndLegacyJavaBounds() throws {
        let range = try VersionJavaRange("[17.0.1, 25.0)")
        XCTAssertFalse(range.contains(version: "17.0.0")); XCTAssertTrue(range.contains(version: "17.0.1"))
        XCTAssertTrue(range.contains(version: "21.0.8")); XCTAssertFalse(range.contains(version: "25.0"))
        let legacy = try VersionJavaRange("[8.0.81, 8.0.141]")
        XCTAssertTrue(legacy.contains(version: "1.8.0_141")); XCTAssertFalse(legacy.contains(version: "1.8.0_142"))
        XCTAssertTrue(try VersionJavaRange("[21.0, )").contains(version: "25.0.1"))
        for invalid in ["17", "[25, 17)", "[abc, 21)", "[17,21,25]"] { XCTAssertThrowsError(try VersionJavaRange(invalid)) }
    }

    func testServerAddressRetainsIPv6AndRejectsMalformedPorts() throws {
        let ipv6 = try MinecraftServerAddress(" [::1]:25566 ")
        XCTAssertEqual(ipv6.address, "[::1]:25566"); XCTAssertEqual(ipv6.host, "::1"); XCTAssertEqual(ipv6.port, 25566)
        XCTAssertEqual(try MinecraftServerAddress("example.org").port, 25565)
        for invalid in ["https://example.org", "a b", "host:invalid", "host:0", "host:65536", "[::1", ":25565"] { XCTAssertThrowsError(try MinecraftServerAddress(invalid), invalid) }
    }

}
