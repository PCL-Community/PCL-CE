import XCTest
@testable import PCLCore

final class ModernModLoaderLaunchTests: XCTestCase {
    func testModernForgeLaunchPlanResolvesBootstrapArguments() throws {
        try verifyLaunchPlan(baseID: "1.20.1", javaVersion: 17, profileID: "我的 Forge 实例",
            launchTarget: "forgeclient", loaderVersion: "47.4.0",
            loaderCoordinate: "net.minecraftforge:forge:1.20.1-47.4.0:universal",
            bootstrapVersion: "1.1.2", secureJarVersion: "2.1.10")
    }

    func testModernNeoForgeLaunchPlanResolvesBootstrapArguments() throws {
        try verifyLaunchPlan(baseID: "1.21.1", javaVersion: 21, profileID: "我的 NeoForge 实例",
            launchTarget: "neoforgeclient", loaderVersion: "21.1.66",
            loaderCoordinate: "net.neoforged:neoforge:21.1.66:universal",
            bootstrapVersion: "2.0.2", secureJarVersion: "3.0.8")
    }

    /// These profiles keep the structure used by modern Forge installers: the
    /// vanilla parent supplies the client and classpath, while the child supplies
    /// bootstrap modules, JVM access flags and its launch target.
    private func verifyLaunchPlan(baseID: String, javaVersion: Int, profileID: String,
        launchTarget: String, loaderVersion: String, loaderCoordinate: String,
        bootstrapVersion: String, secureJarVersion: String) throws {
        let fm = FileManager.default
        let temporary = fm.temporaryDirectory.appendingPathComponent("PCL Modern Loader 测试 \(UUID().uuidString)")
        try fm.createDirectory(at: temporary, withIntermediateDirectories: true)
        addTeardownBlock { try? fm.removeItem(at: temporary) }
        // Foundation can rewrite /private/var to /var; use the launcher's canonical
        // root so path comparisons do not depend on that temporary-directory alias.
        let root = try MinecraftVersionMetadata.safeFile(relativePath: temporary.lastPathComponent, under: fm.temporaryDirectory)
        let java = root.appendingPathComponent("java")
        try Data("#!/bin/sh\necho 'java.version = \(javaVersion).0.1'\necho 'os.arch = \(InstallPlatform.architecture)'\necho 'java.vendor = Test'\n".utf8).write(to: java)
        try fm.setAttributes([.posixPermissions: 0o755], ofItemAtPath: java.path)

        let bootstrap = "cpw.mods:bootstraplauncher:\(bootstrapVersion)"
        let secureJar = "cpw.mods:securejarhandler:\(secureJarVersion)"
        let selectedCommon = "org.apache.commons:commons-lang3:3.14.0"
        let moduleCoordinates = [bootstrap, secureJar]
        let childCoordinates = moduleCoordinates + [loaderCoordinate, selectedCommon]
        var libraryPaths: [String: URL] = [:]
        for coordinate in childCoordinates {
            let relative = try XCTUnwrap(MinecraftVersionMetadata.libraryPath(["name": coordinate]))
            let file = root.appendingPathComponent("libraries").appendingPathComponent(relative)
            try fm.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
            try Data("fixture jar".utf8).write(to: file)
            libraryPaths[coordinate] = file
        }

        try writeProfile(baseID, metadata: [
            "id": baseID, "mainClass": "net.minecraft.client.main.Main",
            "javaVersion": ["majorVersion": javaVersion], "assetIndex": ["id": "17"],
            // The missing old artifact must be replaced by the child's version.
            "libraries": [["name": "org.apache.commons:commons-lang3:3.12.0"]],
            "arguments": [
                "jvm": ["-Djava.library.path=${natives_directory}", "-cp", "${classpath}"],
                "game": ["--username", "${auth_player_name}", "--version", "${version_name}",
                         "--gameDir", "${game_directory}", "--assetsDir", "${assets_root}"]
            ]
        ], root: root)
        let parentJar = root.appendingPathComponent("versions/\(baseID)/\(baseID).jar")
        try Data("vanilla client".utf8).write(to: parentJar)
        let modulePathTemplate = try moduleCoordinates.map {
            "${library_directory}/" + (try XCTUnwrap(MinecraftVersionMetadata.libraryPath(["name": $0])))
        }.joined(separator: "${classpath_separator}")
        let mainClass = "cpw.mods.bootstraplauncher.BootstrapLauncher"
        let opens = "java.base/java.util.jar=cpw.mods.securejarhandler"
        try writeProfile(profileID, metadata: [
            "id": profileID, "inheritsFrom": baseID, "mainClass": mainClass,
            "libraries": childCoordinates.map { ["name": $0] },
            "arguments": [
                "jvm": [
                    "-p", modulePathTemplate, "--add-modules", "ALL-MODULE-PATH",
                    "--add-opens", opens,
                    "-DignoreList=bootstraplauncher,securejarhandler,${version_name}.jar",
                    "-DlegacyClassPath=${classpath}", "-DlibraryDirectory=${library_directory}",
                    "-DprimaryJar=${primary_jar}",
                    ["rules": [["action": "allow", "os": ["name": "osx"]]], "value": "-Dfixture.macos=true"],
                    ["rules": [["action": "allow", "os": ["name": "windows"]]], "value": "-Dfixture.windows=true"]
                ] as [Any],
                "game": ["--launchTarget", launchTarget, "--fml.forgeVersion", loaderVersion]
            ]
        ], root: root)

        let plan = try MinecraftLauncher.makeLaunchPlan(configuration: LaunchConfiguration(
            gameDirectory: root, versionID: profileID, playerName: "OfflineUser", javaExecutable: java, memoryMB: 2048))
        let args = plan.arguments
        XCTAssertEqual(plan.executable, java)
        XCTAssertEqual(plan.workingDirectory, root)
        XCTAssertFalse(args.contains(where: { $0.contains("${") }), "Every metadata placeholder must resolve before Java starts")
        let mainIndex = try XCTUnwrap(args.firstIndex(of: mainClass))
        XCTAssertFalse(args.contains("net.minecraft.client.main.Main"))
        XCTAssertEqual(try value(after: "-p", in: args), moduleCoordinates.map { libraryPaths[$0]!.path }.joined(separator: ":"))
        XCTAssertEqual(try value(after: "--add-modules", in: args), "ALL-MODULE-PATH")
        XCTAssertEqual(try value(after: "--add-opens", in: args), opens)
        XCTAssertTrue(try XCTUnwrap(args.firstIndex(of: "--add-opens")) < mainIndex)
        XCTAssertTrue(args.contains("-Dfixture.macos=true"))
        XCTAssertFalse(args.contains("-Dfixture.windows=true"))
        XCTAssertTrue(args.contains("-DlibraryDirectory=\(root.appendingPathComponent("libraries").path)"))
        XCTAssertTrue(args.contains("-DprimaryJar=\(parentJar.path)"))
        let ignoreArgument = try XCTUnwrap(args.first(where: { $0.hasPrefix("-DignoreList=") }))
        let ignoredFiles = ignoreArgument.dropFirst("-DignoreList=".count).split(separator: ",").map(String.init)
        XCTAssertTrue(ignoredFiles.contains("\(profileID).jar"))
        XCTAssertTrue(ignoredFiles.contains(parentJar.lastPathComponent),
                      "Bootstrap must ignore the actual inherited client JAR as well as the named child profile")

        let classpath = try value(after: "-cp", in: args)
        XCTAssertTrue(args.contains("-DlegacyClassPath=\(classpath)"))
        let classpathFiles = classpath.split(separator: ":").map(String.init)
        XCTAssertTrue(classpathFiles.contains(parentJar.path), "The inherited vanilla client belongs on the launch classpath")
        XCTAssertTrue(classpathFiles.contains(try XCTUnwrap(libraryPaths[loaderCoordinate]).path))
        XCTAssertTrue(classpathFiles.contains(try XCTUnwrap(libraryPaths[selectedCommon]).path))
        XCTAssertFalse(classpathFiles.contains(where: { $0.contains("commons-lang3/3.12.0/") }))
        XCTAssertFalse(classpathFiles.contains(where: { $0.hasSuffix("/\(profileID).jar") }))
        XCTAssertEqual(try value(after: "--username", in: args), "OfflineUser")
        XCTAssertEqual(try value(after: "--version", in: args), profileID)
        XCTAssertEqual(try value(after: "--gameDir", in: args), root.path)
        XCTAssertEqual(try value(after: "--launchTarget", in: args), launchTarget)
        XCTAssertEqual(try value(after: "--fml.forgeVersion", in: args), loaderVersion)
        XCTAssertTrue(try XCTUnwrap(args.firstIndex(of: "--launchTarget")) > mainIndex)
    }

    private func writeProfile(_ id: String, metadata: [String: Any], root: URL) throws {
        let directory = root.appendingPathComponent("versions/\(id)")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try JSONSerialization.data(withJSONObject: metadata).write(to: directory.appendingPathComponent("\(id).json"))
    }

    private func value(after option: String, in arguments: [String], file: StaticString = #filePath, line: UInt = #line) throws -> String {
        let index = try XCTUnwrap(arguments.firstIndex(of: option), "Missing \(option)", file: file, line: line)
        return try XCTUnwrap(arguments.dropFirst(index + 1).first, "Missing value for \(option)", file: file, line: line)
    }
}
