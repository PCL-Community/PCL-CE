import Foundation
import XCTest
@testable import PCLCore

final class VersionJavaSelectionTests: XCTestCase {
    private func runtime(_ version: String, major: Int) -> JavaRuntime {
        JavaRuntime(executable: URL(fileURLWithPath: "/PCL-JavaSelection-Fixture/\(version)/bin/java"),
                    majorVersion: major, architecture: "arm64", displayName: "Java \(version) · arm64 · Test Vendor")
    }

    private func target(required: Int) -> InstalledVersion {
        InstalledVersion(id: "target-java-\(required)", type: "release", requiredJava: required,
                         directory: URL(fileURLWithPath: "/PCL-JavaSelection-Fixture/versions/target-java-\(required)"))
    }

    func testAutomaticSelectionUsesTargetRequirementAndLowestCompatibleMajor() throws {
        let java8 = runtime("1.8.0_442", major: 8), java17 = runtime("17.0.14", major: 17)
        let java21 = runtime("21.0.6", major: 21), java25 = runtime("25.0.1", major: 25)
        var profile = VersionProfileSettings()
        profile.javaMode = "auto"
        let available = [java25, java21, java8, java17]

        func selected(required: Int) throws -> JavaRuntime {
            try VersionJavaSelection(version: target(required: required), profile: profile,
                                     globalJavaPath: java25.executable.path, runtimes: available).resolve()
        }
        XCTAssertEqual(try selected(required: 8), java8)
        XCTAssertEqual(try selected(required: 17), java17)
        XCTAssertEqual(try selected(required: 20), java21)
        XCTAssertThrowsError(try selected(required: 26))
    }

    func testGlobalAndCustomPathsKeepTheirOwnPrecedence() throws {
        let global = runtime("21.0.6", major: 21), custom = runtime("17.0.14", major: 17)
        var profile = VersionProfileSettings()
        profile.javaPath = custom.executable.path
        profile.javaMode = "global"
        let version = target(required: 17)
        let available = [custom, global]
        XCTAssertEqual(try VersionJavaSelection(version: version, profile: profile,
                                               globalJavaPath: global.executable.path, runtimes: available).resolve(), global)
        profile.javaMode = "custom"
        XCTAssertEqual(try VersionJavaSelection(version: version, profile: profile,
                                               globalJavaPath: global.executable.path, runtimes: available).resolve(), custom)
        profile.javaMode = "auto"
        profile.javaPath = global.executable.path
        XCTAssertEqual(try VersionJavaSelection(version: version, profile: profile,
                                               globalJavaPath: global.executable.path, runtimes: available).resolve(), custom)
    }

    func testRangeHonorsPatchBoundsAndStillRequiresTheTargetMajor() throws {
        let before = runtime("17.0.9", major: 17), allowed = runtime("17.0.10", major: 17)
        let excluded = runtime("17.0.11", major: 17), java21 = runtime("21.0.6", major: 21)
        var profile = VersionProfileSettings()
        profile.javaMode = "range"
        profile.javaVersionRange = "[17.0.10, 17.0.11)"
        XCTAssertEqual(try VersionJavaSelection(version: target(required: 17), profile: profile,
                                               globalJavaPath: java21.executable.path,
                                               runtimes: [excluded, before, allowed, java21]).resolve(), allowed)
        XCTAssertThrowsError(try VersionJavaSelection(version: target(required: 21), profile: profile,
                                                     globalJavaPath: "", runtimes: [allowed]).resolve(fallback: java21))
        profile.javaVersionRange = "17.0.10"
        XCTAssertThrowsError(try VersionJavaSelection(version: target(required: 17), profile: profile,
                                                     globalJavaPath: "", runtimes: [allowed]).resolve()) { error in
            guard case VersionManagementError.invalidSettings = error else {
                return XCTFail("Expected explicit range validation, received \(error)")
            }
        }
    }

    func testMissingExplicitJavaNeverSilentlyUsesFallback() throws {
        let fallback = runtime("17.0.14", major: 17)
        var profile = VersionProfileSettings()
        profile.javaMode = "auto"
        let version = target(required: 17)
        XCTAssertEqual(try VersionJavaSelection(version: version, profile: profile,
                                               globalJavaPath: "", runtimes: []).resolve(fallback: fallback), fallback)
        profile.javaMode = "global"
        XCTAssertEqual(try VersionJavaSelection(version: version, profile: profile,
                                               globalJavaPath: "", runtimes: []).resolve(fallback: fallback), fallback)
        XCTAssertThrowsError(try VersionJavaSelection(version: version, profile: profile,
                                                     globalJavaPath: "/PCL-JavaSelection-Fixture/missing/bin/java",
                                                     runtimes: [fallback]).resolve(fallback: fallback))
        profile.javaMode = "custom"
        profile.javaPath = "/PCL-JavaSelection-Fixture/missing/bin/java"
        XCTAssertThrowsError(try VersionJavaSelection(version: version, profile: profile,
                                                     globalJavaPath: fallback.executable.path,
                                                     runtimes: [fallback]).resolve(fallback: fallback))
        profile.javaMode = "folder"
        XCTAssertThrowsError(try VersionJavaSelection(version: version, profile: profile,
                                                     globalJavaPath: fallback.executable.path,
                                                     runtimes: [fallback]).resolve(fallback: fallback))
    }

    func testLegacyProfileModesKeepGlobalAutomaticAndCustomBehavior() throws {
        let java17 = runtime("17.0.14", major: 17), java21 = runtime("21.0.6", major: 21)
        let version = target(required: 17)
        var profile = VersionProfileSettings()
        profile.javaPath = java17.executable.path
        XCTAssertNil(profile.javaMode)
        XCTAssertEqual(try VersionJavaSelection(version: version, profile: profile,
                                               globalJavaPath: java21.executable.path,
                                               runtimes: [java17, java21]).resolve(), java21)
        profile.useGlobalLaunchSettings = false
        XCTAssertEqual(try VersionJavaSelection(version: version, profile: profile,
                                               globalJavaPath: java21.executable.path,
                                               runtimes: [java17, java21]).resolve(), java17)
        profile.javaPath = ""
        XCTAssertEqual(try VersionJavaSelection(version: version, profile: profile,
                                               globalJavaPath: java21.executable.path,
                                               runtimes: [java21, java17]).resolve(), java17)
    }
}
