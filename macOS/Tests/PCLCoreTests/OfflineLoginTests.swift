import Foundation
import XCTest
@testable import PCLCore

final class OfflineLoginTests: XCTestCase {
    func testUsernameNormalizationMatchesOriginalLoginInput() throws {
        XCTAssertEqual(OfflineLogin.normalizedName("  Steve¨  \n"), "Steve")
        XCTAssertEqual(OfflineLogin.normalizedName("  测¨试 玩家  "), "测试 玩家")
        XCTAssertEqual(try OfflineLogin.validateName("  A¨  ", versionID: "1.20.4"), "A")
    }

    func testOriginalValidationRejectsOnlyEmptyAndQuotesBeforeVersionLimit() {
        for name in ["", " \n\t", " ¨ ¨ "] {
            XCTAssertThrowsError(try OfflineLogin.validateName(name)) { error in
                XCTAssertEqual(error as? OfflineLoginError, .emptyName)
            }
        }
        for name in ["Player\"Name", "\"", "  玩家\"  "] {
            XCTAssertThrowsError(try OfflineLogin.validateName(name, versionID: "1.20.4")) { error in
                XCTAssertEqual(error as? OfflineLoginError, .doubleQuote)
            }
        }
    }

    func testOneCharacterUnicodeAndPunctuationNamesRemainAllowed() throws {
        for name in ["A", "玩", "玩家_1", "Hello World", "a-b", "😀"] {
            XCTAssertEqual(try OfflineLogin.validateName(name, versionID: "1.20.4"), name)
        }
    }

    func testSixteenUTF16UnitLimitAppliesToModernNumericReleases() throws {
        let atLimit = String(repeating: "A", count: 16)
        let tooLong = atLimit + "B"
        for version in ["1.20.3", "1.20.4", "1.21", "1.21.10", "26.1", "26.3"] {
            XCTAssertEqual(try OfflineLogin.validateName(atLimit, versionID: version), atLimit)
            XCTAssertThrowsError(try OfflineLogin.validateName(tooLong, versionID: version)) { error in
                XCTAssertEqual(error as? OfflineLoginError, .nameTooLong)
            }
        }
        let eightEmoji = String(repeating: "😀", count: 8)
        XCTAssertEqual(try OfflineLogin.validateName(eightEmoji, versionID: "1.20.4"), eightEmoji)
        XCTAssertThrowsError(try OfflineLogin.validateName(eightEmoji + "😀", versionID: "1.20.4"))
        // Swift sees nine graphemes, but .NET's original String.Length counts eighteen units.
        let combinedCharacters = String(repeating: "a\u{0301}", count: 9)
        XCTAssertEqual(combinedCharacters.count, 9)
        XCTAssertThrowsError(try OfflineLogin.validateName(combinedCharacters, versionID: "1.20.4"))
    }

    func testNoVersionLengthLimitForOlderOrUnresolvedProfiles() throws {
        let name = String(repeating: "LongName", count: 4)
        for version: String? in [nil, "1.20.2", "1.20", "1.19.4", "1.8.9", "custom-profile", "24w14a", "1.20.3-rc1"] {
            XCTAssertEqual(try OfflineLogin.validateName(name, versionID: version), name)
        }
    }

    func testUUIDGoldenVectorsMatchPinnedOriginalPCLHelper() {
        let vectors = [
            "Steve": "0000000000003005998F503099DCF29B",
            "Alex": "0000000000003004998F501A96F4269A",
            "a": "0000000000003001998F501BC686B6EB",
            "A": "0000000000003001998F501BC686B6CB",
            "测试玩家": "0000000000003004998F501AA3E8FCEB",
            "😀": "0000000000003002998F501BC6CB03B7",
            "": "0000000000003000998F501BC684162A"
        ]
        for (name, expected) in vectors {
            XCTAssertEqual(OfflineLogin.uuid(for: name), expected)
            XCTAssertEqual(MinecraftArguments.offlineUUID(for: name), expected)
        }
    }

    func testForcedSkinModelsMatchOriginalParityAndSuffixWrap() {
        let steveDefault = "0000000000003005998F503099DCF29B"
        XCTAssertEqual(OfflineLogin.uuid(for: "Steve", skinModel: .automatic), steveDefault)
        XCTAssertEqual(OfflineLogin.uuid(for: "Steve", skinModel: .steve), steveDefault)
        XCTAssertEqual(OfflineLogin.uuid(for: "Steve", skinModel: .alex), "0000000000003005998F503099DCF29C")
        XCTAssertEqual(OfflineLogin.uuid(for: "a", skinModel: .steve), "0000000000003001998F501BC686B6EC")
        for name in ["Steve", "Alex", "a", "玩家", "😀"] {
            XCTAssertEqual(OfflineLogin.skinName(forUUID: OfflineLogin.uuid(for: name, skinModel: .steve)), "Steve")
            XCTAssertEqual(OfflineLogin.skinName(forUUID: OfflineLogin.uuid(for: name, skinModel: .alex)), "Alex")
        }
        XCTAssertEqual(OfflineLogin.skinName(forUUID: "invalid"), "Steve")
        // This fixture's original hash ends FFFFF. PCL wraps then increments to 00001;
        // that is still Alex, so forced Steve must advance again to 00002, not stop at 00000.
        let wrapName = "vaa\u{B743}"
        XCTAssertEqual(OfflineLogin.uuid(for: wrapName), "0000000000003004998F501A96EFFFFF")
        XCTAssertEqual(OfflineLogin.uuid(for: wrapName, skinModel: .steve), "0000000000003004998F501A96E00002")
    }

    func testLaunchIdentityUsesChosenSkinUUIDConsistently() throws {
        let root = URL(fileURLWithPath: "/tmp/PCL forced offline skin")
        let config = LaunchConfiguration(gameDirectory: root, versionID: "1.20.4", playerName: "Steve",
                                         javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), memoryMB: 1024,
                                         offlineSkinModel: .alex)
        let metadata: [String: Any] = ["mainClass": "net.minecraft.client.main.Main", "minecraftArguments":
            "--username ${auth_player_name} --uuid ${auth_uuid} --accessToken ${auth_access_token} --session ${auth_session} --alias ${access_token}"]
        let args = try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root)
        let forcedUUID = "0000000000003005998F503099DCF29C"
        for key in ["--uuid", "--accessToken", "--session", "--alias"] {
            let index = try XCTUnwrap(args.firstIndex(of: key))
            XCTAssertEqual(args[index + 1], forcedUUID)
        }
        let usernameIndex = try XCTUnwrap(args.firstIndex(of: "--username"))
        XCTAssertEqual(args[usernameIndex + 1], "Steve")
        XCTAssertEqual(OfflineLogin.skinName(forUUID: forcedUUID), "Alex")
    }

    func testLaunchArgumentsUseOriginalOfflineIdentityAndNormalizedName() throws {
        let root = URL(fileURLWithPath: "/tmp/PCL offline parity")
        let config = LaunchConfiguration(gameDirectory: root, versionID: "1.20.4", playerName: "  玩¨家  ",
                                         javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), memoryMB: 1024)
        let metadata: [String: Any] = ["mainClass": "net.minecraft.client.main.Main", "arguments": [
            "jvm": [], "game": ["--username", "${auth_player_name}", "--uuid", "${auth_uuid}",
                                "--accessToken", "${auth_access_token}", "--session", "${auth_session}",
                                "--legacyToken", "${access_token}", "--userType", "${user_type}"]
        ]]
        let args = try MinecraftArguments.build(metadata: metadata, configuration: config, classpath: [], nativesDirectory: root)
        let expectedUUID = OfflineLogin.uuid(for: "玩家")
        func value(after key: String) throws -> String {
            let index = try XCTUnwrap(args.firstIndex(of: key))
            return args[index + 1]
        }
        XCTAssertEqual(try value(after: "--username"), "玩家")
        for key in ["--uuid", "--accessToken", "--session", "--legacyToken"] {
            XCTAssertEqual(try value(after: key), expectedUUID)
        }
        XCTAssertEqual(try value(after: "--userType"), "msa")
    }

    func testInvalidNameIsRejectedBeforeLaunchPlanReadsFilesOrJava() {
        let config = LaunchConfiguration(gameDirectory: URL(fileURLWithPath: "/tmp/not-a-game"), versionID: "1.20.4",
                                         playerName: "  ¨  ", javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), memoryMB: 1024)
        XCTAssertThrowsError(try MinecraftLauncher.makeLaunchPlan(configuration: config)) { error in
            XCTAssertEqual(error as? OfflineLoginError, .emptyName)
        }
    }
}
