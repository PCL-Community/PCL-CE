import Foundation
import XCTest
@testable import PCLCore

final class LegacyComponentLaunchArgumentsTests: XCTestCase {
    private func arguments(libraries: [String], tweaks: [String]) throws -> [String] {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-Legacy-Arguments-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let metadata: [String: Any] = ["id": "1.12.2", "mainClass": "net.minecraft.launchwrapper.Launch",
            "libraries": libraries.map { ["name": $0] },
            "minecraftArguments": "--username ${auth_player_name} --version ${version_name} " + tweaks.map { "--tweakClass " + $0 }.joined(separator: " ")]
        return try MinecraftArguments.build(metadata: metadata, configuration: .init(gameDirectory: root, versionID: "Test", playerName: "PCLSmoke",
            javaExecutable: URL(fileURLWithPath: "/fixture/java"), memoryMB: 1024), classpath: [], nativesDirectory: root)
    }
    func testLiteLoaderUsesCooperativeOptiFineTweakerLast() throws {
        let args = try arguments(libraries: ["com.mumfrey:liteloader:1.12.2-SNAPSHOT"], tweaks: ["optifine.OptiFineTweaker", "com.mumfrey.liteloader.launch.LiteLoaderTweaker"])
        XCTAssertFalse(args.contains("optifine.OptiFineTweaker"))
        XCTAssertEqual(Array(args.suffix(2)), ["--tweakClass", "optifine.OptiFineForgeTweaker"])
        XCTAssertTrue(args.contains("com.mumfrey.liteloader.launch.LiteLoaderTweaker"))
        XCTAssertEqual(args.filter { $0 == "--version" }.count, 1)
    }
    func testForgeNormalizesAndDeduplicatesOptiFineTweakers() throws {
        let args = try arguments(libraries: ["net.minecraftforge:forge:1.12.2-14.23.5.2859"], tweaks: ["optifine.OptiFineForgeTweaker", "net.minecraftforge.fml.common.launcher.FMLTweaker", "optifine.OptiFineTweaker"])
        XCTAssertEqual(args.filter { $0 == "optifine.OptiFineForgeTweaker" }.count, 1)
        XCTAssertEqual(args.last, "optifine.OptiFineForgeTweaker")
    }
    func testStandaloneOptiFineKeepsItsOwnTweaker() throws {
        let args = try arguments(libraries: ["optifine:OptiFine:1.12.2_HD_U_G5"], tweaks: ["optifine.OptiFineTweaker"])
        XCTAssertTrue(args.contains("optifine.OptiFineTweaker"))
        XCTAssertFalse(args.contains("optifine.OptiFineForgeTweaker"))
    }
}
