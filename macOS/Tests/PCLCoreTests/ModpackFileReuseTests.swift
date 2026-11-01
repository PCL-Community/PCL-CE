import Foundation
import CryptoKit
import XCTest
@testable import PCLCore

final class ModpackFileReuseTests: XCTestCase {
    private func root() throws -> URL {
        let value = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-Reuse-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: value, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: value) }
        return value
    }
    private func identity(_ data: Data, fingerprint: UInt32? = nil) -> ModpackFileIdentity {
        ModpackFileIdentity(size: Int64(data.count), sha1: hex(Insecure.SHA1.hash(data: data)),
                            sha512: hex(SHA512.hash(data: data)), md5: hex(Insecure.MD5.hash(data: data)), fingerprint: fingerprint)
    }
    private func hex<D: Sequence>(_ value: D) -> String where D.Element == UInt8 {
        value.map { String(format: "%02x", $0) }.joined()
    }

    func testMatchingBytesAreReusedAcrossNamesAndCopiesAreIndependent() async throws {
        let source = try root(), output = try root(), content = Data("exact distributable bytes".utf8)
        let file = source.appendingPathComponent("用户重命名的Mod.jar")
        try content.write(to: file)
        let reuse = ModpackFileReuse(roots: [source])
        let match = try await reuse.matchingFile(for: identity(content))
        XCTAssertEqual(match?.lastPathComponent, file.lastPathComponent)
        let destination = output.appendingPathComponent("nested/canonical-name.jar")
        let reused = try await reuse.reuse(identity(content), to: destination)
        XCTAssertTrue(reused)
        XCTAssertEqual(try Data(contentsOf: destination), content)
        try Data("changed target bytes".utf8).write(to: destination)
        XCTAssertEqual(try Data(contentsOf: file), content, "Reuse must never hard-link mutable profile files to their source")
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: destination.deletingLastPathComponent().path).contains { $0.hasSuffix(".partial") })
    }

    func testSameFilenameOrSameLengthCannotReplaceHashVerification() async throws {
        let source = try root(), content = Data("right version".utf8)
        let file = source.appendingPathComponent("mod-1.0.jar")
        try Data("wrong version".utf8).write(to: file)
        let reuse = ModpackFileReuse(roots: [source])
        let missing = try await reuse.matchingFile(for: identity(content))
        XCTAssertNil(missing)
        try content.write(to: file)
        let found = try await reuse.matchingFile(for: identity(content))
        XCTAssertNotNil(found, "Changed files must be rehashed even when their indexed size is unchanged")
        try Data("wrong version".utf8).write(to: file)
        let changed = try await reuse.matchingFile(for: identity(content))
        XCTAssertNil(changed, "A previously verified path must not remain trusted after its bytes change")
    }

    func testEveryDeclaredHashAndSizeMustMatch() async throws {
        let source = try root(), content = Data("payload".utf8)
        let file = source.appendingPathComponent("resource.zip")
        try content.write(to: file)
        let reuse = ModpackFileReuse(roots: [source])
        let badHash = ModpackFileIdentity(size: Int64(content.count), sha1: hex(Insecure.SHA1.hash(data: content)), md5: String(repeating: "0", count: 32))
        let rejected = try await reuse.matchingFile(for: badHash)
        XCTAssertNil(rejected)
        let badSize = ModpackFileIdentity(size: Int64(content.count + 1), sha1: hex(Insecure.SHA1.hash(data: content)))
        let rejectedSize = try await reuse.matchingFile(for: badSize)
        XCTAssertNil(rejectedSize)
        let uppercase = ModpackFileIdentity(sha512: hex(SHA512.hash(data: content)).uppercased())
        let accepted = try await reuse.matchingFile(for: uppercase)
        XCTAssertNotNil(accepted)
    }

    func testFingerprintAloneCannotSubstituteDifferentBytes() async throws {
        let source = try root(), content = Data("a b\nc\td\r".utf8)
        let file = source.appendingPathComponent("mod.jar")
        try content.write(to: file)
        let reuse = ModpackFileReuse(roots: [source])
        let fingerprintOnly = try await reuse.matchingFile(for: .init(size: Int64(content.count), fingerprint: 3_376_380_438))
        XCTAssertNil(fingerprintOnly)
        XCTAssertTrue(try ModpackFileReuse.matches(file, identity: identity(content, fingerprint: 3_376_380_438)))
        let spaced = Data("a b c d".utf8), otherWhitespace = Data("a\tb\nc\rd".utf8)
        XCTAssertEqual(spaced.count, otherWhitespace.count)
        try spaced.write(to: file)
        XCTAssertTrue(try ModpackFileReuse.matches(file, identity: identity(spaced, fingerprint: 3_376_380_438)))
        XCTAssertFalse(try ModpackFileReuse.matches(file, identity: identity(otherWhitespace, fingerprint: 3_376_380_438)),
                       "CurseForge strips whitespace before fingerprinting; complete-byte hashes must remain authoritative")
        for (text, fingerprint): (String, UInt32) in [("", 1_540_447_798), ("a", 626_045_324), ("ab", 1_692_487_918),
                                                     ("abc", 1_621_425_345), ("abcd", 3_376_380_438), ("hello", 2_788_266_382)] {
            let data = Data(text.utf8)
            try data.write(to: file)
            XCTAssertTrue(try ModpackFileReuse.matches(file, identity: identity(data, fingerprint: fingerprint)))
        }
    }

    func testOnlyImmediateRegularFilesInControlledRootsAreIndexed() async throws {
        let source = try root(), outside = try root(), content = Data("private".utf8)
        let externalFile = outside.appendingPathComponent("external.jar")
        try content.write(to: externalFile)
        try FileManager.default.createSymbolicLink(at: source.appendingPathComponent("linked.jar"), withDestinationURL: externalFile)
        let nested = source.appendingPathComponent("nested/inner.jar")
        try FileManager.default.createDirectory(at: nested.deletingLastPathComponent(), withIntermediateDirectories: true)
        try content.write(to: nested)
        try content.write(to: source.appendingPathComponent(".partial"))
        let linkedRoot = source.appendingPathComponent("linked-root")
        try FileManager.default.createSymbolicLink(at: linkedRoot, withDestinationURL: outside)
        let reuse = ModpackFileReuse(roots: [source, linkedRoot])
        let match = try await reuse.matchingFile(for: identity(content))
        XCTAssertNil(match, "Links, hidden partial files and nested dependencies must not become download replacements")
        XCTAssertFalse(try ModpackFileReuse.matches(source.appendingPathComponent("linked.jar"), identity: identity(content)))
    }

    func testExistingDestinationIsPreservedAndRefreshFindsNewCacheFiles() async throws {
        let source = try root(), output = try root(), content = Data("complete".utf8)
        let reuse = ModpackFileReuse(roots: [source])
        let initial = try await reuse.matchingFile(for: identity(content))
        XCTAssertNil(initial)
        try content.write(to: source.appendingPathComponent("newly-downloaded.bin"))
        try await reuse.refresh()
        let destination = output.appendingPathComponent("mod.jar")
        let override = Data("author override".utf8)
        try override.write(to: destination)
        let preserved = try await reuse.reuse(identity(content), to: destination)
        XCTAssertFalse(preserved)
        XCTAssertEqual(try Data(contentsOf: destination), override)
        try FileManager.default.removeItem(at: destination)
        let copied = try await reuse.reuse(identity(content), to: destination)
        XCTAssertTrue(copied)
        let existingMatch = try await reuse.reuse(identity(content), to: destination)
        XCTAssertTrue(existingMatch)
        let count = await reuse.reusedFiles
        XCTAssertEqual(count, 2)
    }

    func testLimitsAndMalformedExpectedHashesAreRejected() async throws {
        let source = try root(), content = Data("too big".utf8)
        try content.write(to: source.appendingPathComponent("mod.jar"))
        let capped = ModpackFileReuse(roots: [source], maximumFileBytes: 3)
        let found = try await capped.matchingFile(for: identity(content))
        XCTAssertNil(found)
        do {
            _ = try await capped.matchingFile(for: .init(sha1: "invalid"))
            XCTFail("Malformed expected digests must not be treated as identity proof")
        } catch ModpackImportError.invalidManifest { }
        do {
            _ = try await capped.matchingFile(for: .init(size: -1, md5: String(repeating: "0", count: 32)))
            XCTFail("Negative sizes must not be accepted")
        } catch ModpackImportError.invalidManifest { }
    }
}
