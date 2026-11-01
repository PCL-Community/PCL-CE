import Foundation
import XCTest
@testable import PCLCore

final class LegacyOpenGLCompatibilityTests: XCTestCase {
    private let legacy: [String: Any] = ["libraries": [["name": "org.lwjgl.lwjgl:lwjgl:2.9.2-nightly-20140822"]]]

    private func directory() throws -> URL {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("pcl-opengl-" + UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        return root
    }

    func testOnlyJava8LegacyLWJGLOnAffectedMacOSReceivesAgent() throws {
        let root = try directory()
        for java in [7, 9, 17, 21, 25] {
            XCTAssertNil(try LegacyOpenGLCompatibility.javaAgentArgument(metadata: legacy, javaMajorVersion: java,
                                                                        gameDirectory: root, macOSMajorVersion: 27))
        }
        XCTAssertNil(try LegacyOpenGLCompatibility.javaAgentArgument(metadata: legacy, javaMajorVersion: 8,
                                                                    gameDirectory: root, macOSMajorVersion: 26))
        XCTAssertNil(try LegacyOpenGLCompatibility.javaAgentArgument(metadata: ["libraries": [["name": "org.lwjgl:lwjgl:3.3.1"]]],
                                                                    javaMajorVersion: 8, gameDirectory: root, macOSMajorVersion: 27))
        XCTAssertNil(try LegacyOpenGLCompatibility.javaAgentArgument(metadata: [:], javaMajorVersion: 8,
                                                                    gameDirectory: root, macOSMajorVersion: 27))
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("PCL").path))
        let argument = try XCTUnwrap(try LegacyOpenGLCompatibility.javaAgentArgument(metadata: legacy, javaMajorVersion: 8,
                                                                                   gameDirectory: root, macOSMajorVersion: 27))
        XCTAssertTrue(argument.hasPrefix("-javaagent:"))
        XCTAssertTrue(argument.hasSuffix(LegacyOpenGLCompatibility.filename))
    }

    func testVerifiedCacheIsReusedAndCorruptBytesAreRepairedBeforeLaunch() throws {
        let root = try directory()
        let first = try XCTUnwrap(try LegacyOpenGLCompatibility.javaAgentArgument(metadata: legacy, javaMajorVersion: 8,
                                                                                gameDirectory: root, macOSMajorVersion: 27))
        let file = URL(fileURLWithPath: String(first.dropFirst("-javaagent:".count)))
        let original = try Data(contentsOf: file)
        XCTAssertEqual(original.prefix(4), Data([0x50, 0x4b, 0x03, 0x04]))
        let modified = Date(timeIntervalSince1970: 1_000_000)
        try FileManager.default.setAttributes([.modificationDate: modified], ofItemAtPath: file.path)
        XCTAssertEqual(try LegacyOpenGLCompatibility.javaAgentArgument(metadata: legacy, javaMajorVersion: 8,
                                                                       gameDirectory: root, macOSMajorVersion: 27), first)
        XCTAssertEqual(try file.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate, modified)
        try Data("truncated or replaced cache".utf8).write(to: file)
        XCTAssertEqual(try LegacyOpenGLCompatibility.javaAgentArgument(metadata: legacy, javaMajorVersion: 8,
                                                                       gameDirectory: root, macOSMajorVersion: 27), first)
        XCTAssertEqual(try Data(contentsOf: file), original)
    }

    func testLinkedCacheCannotReadOrOverwriteAnotherFile() throws {
        let root = try directory(), outside = try directory()
        let original = Data("keep external file".utf8), external = outside.appendingPathComponent("external.jar")
        try original.write(to: external)
        try FileManager.default.createSymbolicLink(at: root.appendingPathComponent("PCL"), withDestinationURL: outside)
        XCTAssertThrowsError(try LegacyOpenGLCompatibility.javaAgentArgument(metadata: legacy, javaMajorVersion: 8,
                                                                            gameDirectory: root, macOSMajorVersion: 27))
        XCTAssertFalse(FileManager.default.fileExists(atPath: outside.appendingPathComponent("tools").path))
        try FileManager.default.removeItem(at: root.appendingPathComponent("PCL"))
        let tools = root.appendingPathComponent("PCL/tools")
        try FileManager.default.createDirectory(at: tools, withIntermediateDirectories: true)
        let cache = tools.appendingPathComponent(LegacyOpenGLCompatibility.filename)
        try FileManager.default.createSymbolicLink(at: cache, withDestinationURL: external)
        XCTAssertThrowsError(try LegacyOpenGLCompatibility.javaAgentArgument(metadata: legacy, javaMajorVersion: 8,
                                                                            gameDirectory: root, macOSMajorVersion: 27))
        try FileManager.default.removeItem(at: cache)
        try FileManager.default.linkItem(at: external, to: cache)
        XCTAssertThrowsError(try LegacyOpenGLCompatibility.javaAgentArgument(metadata: legacy, javaMajorVersion: 8,
                                                                            gameDirectory: root, macOSMajorVersion: 27))
        XCTAssertEqual(try Data(contentsOf: external), original)
    }

    func testLaunchArgumentsPreserveExistingAgentsAndDoNotInjectIntoJava17() throws {
        let root = try directory()
        var metadata = legacy
        metadata["mainClass"] = "net.minecraft.client.main.Main"
        metadata["minecraftArguments"] = "--username ${auth_player_name}"
        metadata["pclOriginalVersion"] = "1.12.2"
        var config = LaunchConfiguration(gameDirectory: root, versionID: "1.12.2", playerName: "PCLSmoke",
                                        javaExecutable: URL(fileURLWithPath: "/unused/java"), memoryMB: 1024,
                                        extraJVMArguments: "-javaagent:/tmp/user-owned-agent.jar -Duser.setting=keep", javaMajorVersion: 8)
        let arguments = try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root)
        XCTAssertTrue(arguments.contains("-javaagent:/tmp/user-owned-agent.jar"))
        XCTAssertTrue(arguments.contains("-Duser.setting=keep"))
        if ProcessInfo.processInfo.operatingSystemVersion.majorVersion >= 27 {
            XCTAssertEqual(arguments.filter { $0.hasSuffix(LegacyOpenGLCompatibility.filename) }.count, 1)
        }
        config.javaMajorVersion = 17
        let modernJava = try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root)
        XCTAssertFalse(modernJava.contains { $0.hasSuffix(LegacyOpenGLCompatibility.filename) })
        XCTAssertTrue(modernJava.contains("-javaagent:/tmp/user-owned-agent.jar"))
    }
}
