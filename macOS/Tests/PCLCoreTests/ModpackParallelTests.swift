import Foundation
import XCTest
import CryptoKit
import zlib
@testable import PCLCore

/// Gates are driven by actual URLProtocol requests and stage events. No assertion
/// depends on archive size, disk speed, network timing, or a sleeping downloader.
final class ModpackParallelTests: XCTestCase {
    private func directory() throws -> URL {
        let path = FileManager.default.temporaryDirectory.appendingPathComponent("PCLParallel-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: path, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: path) }
        return path
    }

    private func fixture(in root: URL, bundledMod: Data = ParallelFixture.bundledMod,
                         overridePath: String = "mods/原包命名.jar", modrinthRequiredPath: String? = nil,
                         extraMembers: [(String, Data)] = []) throws -> ParallelPack {
        let metadata: [String: Any]
        let manifestName: String
        if let path = modrinthRequiredPath {
            manifestName = "modrinth.index.json"
            metadata = ["formatVersion": 1, "game": "minecraft", "versionId": "parallel", "name": "Parallel fixture",
                "dependencies": ["minecraft": "1.20.4"], "files": [["path": path,
                    "hashes": ["sha1": MinecraftInstaller.hash(ParallelFixture.bundledMod),
                               "sha512": SHA512.hash(data: ParallelFixture.bundledMod).map { String(format: "%02x", $0) }.joined()],
                    "fileSize": ParallelFixture.bundledMod.count, "downloads": ["https://cdn.modrinth.com/pcl-parallel/canonical.jar"]]]]
        } else {
            manifestName = "manifest.json"
            metadata = ["manifestType": "minecraftModpack", "manifestVersion": 1, "name": "Parallel fixture",
                "minecraft": ["version": "1.20.4", "modLoaders": []],
                "files": [["projectID": 123, "fileID": 456, "required": true]], "overrides": "overrides"]
        }
        return try sourcePack(in: root, members: [
            (manifestName, JSONSerialization.data(withJSONObject: metadata)),
            ("overrides/config/copied.txt", Data("preserved override".utf8)),
            ("overrides/" + overridePath, bundledMod)
        ] + extraMembers)
    }

    private func sourcePack(in root: URL, members: [(String, Data)]) throws -> ParallelPack {
        let archive = root.appendingPathComponent("pack.zip")
        let data = try parallelZIP(members)
        try data.write(to: archive)
        let original = root.appendingPathComponent("versions/Original/mods/keep.jar")
        try FileManager.default.createDirectory(at: original.deletingLastPathComponent(), withIntermediateDirectories: true)
        let source = Data("an existing user's version must remain unchanged".utf8)
        try source.write(to: original)
        return ParallelPack(archive: archive, original: original, source: source, archiveBytes: data,
            originalModified: try original.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate,
            archiveModified: try archive.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate)
    }

    private func assertSourcesUnchanged(_ pack: ParallelPack, file: StaticString = #filePath, line: UInt = #line) throws {
        XCTAssertEqual(try Data(contentsOf: pack.original), pack.source, file: file, line: line)
        XCTAssertEqual(try Data(contentsOf: pack.archive), pack.archiveBytes, file: file, line: line)
        XCTAssertEqual(try pack.original.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate, pack.originalModified, file: file, line: line)
        XCTAssertEqual(try pack.archive.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate, pack.archiveModified, file: file, line: line)
    }

    private func importer(_ fixture: ParallelFixture) -> (ModpackImporter, URLSession) {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [ParallelFixtureProtocol.self]
        configuration.httpAdditionalHeaders = ["X-PCL-Parallel-Test-ID": fixture.id]
        ParallelFixtureProtocol.register(fixture)
        let session = URLSession(configuration: configuration)
        let installer = MinecraftInstaller(session: session, manifestURL: URL(string: "https://pcl-parallel.invalid/manifest")!)
        return (ModpackImporter(session: session, installer: installer, curseForgeAPIKey: ""), session)
    }

    func testIndependentBranchesOverlapAndBundledModNeedsNoDownload() async throws {
        let root = try directory(), pack = try fixture(in: root)
        let gate = ParallelFixture(root: root)
        let (importer, session) = importer(gate)
        defer { gate.releaseAll(); session.invalidateAndCancel(); ParallelFixtureProtocol.unregister(gate) }
        let task = Task {
            try await importer.importPack(archive: pack.archive, gameDirectory: root, profileID: "Parallel pack",
                javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { gate.observe($0) })
        }
        await fulfillment(of: [gate.extractionStarted, gate.vanillaRequested, gate.modMetadataRequested], timeout: 5)
        guard gate.sawAllStarts else {
            task.cancel(); gate.releaseAll(); _ = try? await task.value
            return
        }
        XCTAssertEqual(gate.state(of: "extract"), .running)
        XCTAssertTrue(gate.requestsPrecededExtractionCompletion, "Vanilla and Mod metadata requests must start before extraction finishes")

        // Metadata can resolve while both the bulk extractor and vanilla HTTP
        // request are held. An exact file already inside the archive is enough
        // to satisfy the Mod branch without waiting for the bulk copy to finish.
        gate.releaseModMetadata()
        await fulfillment(of: [gate.modFilesCompleted], timeout: 5)
        XCTAssertEqual(gate.state(of: "extract"), .running)
        XCTAssertEqual(gate.canonicalModDownloads, 0)
        XCTAssertFalse(gate.vanillaResponseWasReleased)

        gate.releaseExtraction()
        await fulfillment(of: [gate.extractionCompleted], timeout: 5)
        XCTAssertFalse(gate.vanillaResponseWasReleased, "Extraction must be able to finish while vanilla metadata is still pending")
        gate.releaseVanilla()
        try await task.value

        let profile = root.appendingPathComponent("versions/Parallel pack")
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("mods/原包命名.jar")), ParallelFixture.bundledMod)
        XCTAssertFalse(FileManager.default.fileExists(atPath: profile.appendingPathComponent("mods/canonical.jar").path), "Do not create a second Mod with a CDN filename")
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("config/copied.txt")), Data("preserved override".utf8))
        let report = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: profile.appendingPathComponent("PCL/import-report.json"))) as? [String: Any])
        XCTAssertEqual(report["resolvedFiles"] as? Int, 1)
        XCTAssertEqual(report["reusedFiles"] as? Int, 1)
        XCTAssertEqual(report["downloadedFiles"] as? Int, 0)
        try assertSourcesUnchanged(pack)
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: root.path).contains { $0.hasPrefix(".pcl-import-") })
    }

    func testSameSizeBundledFileWithDifferentFullHashDownloadsTheRequiredMod() async throws {
        let root = try directory()
        var wrongMod = ParallelFixture.bundledMod
        wrongMod[wrongMod.startIndex] ^= 1
        let pack = try fixture(in: root, bundledMod: wrongMod)
        let gate = ParallelFixture(root: root)
        let (importer, session) = importer(gate)
        defer { gate.releaseAll(); session.invalidateAndCancel(); ParallelFixtureProtocol.unregister(gate) }
        let task = Task {
            try await importer.importPack(archive: pack.archive, gameDirectory: root, profileID: "Hash checked pack",
                javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { gate.observe($0) })
        }
        await fulfillment(of: [gate.extractionStarted, gate.vanillaRequested, gate.modMetadataRequested], timeout: 5)
        guard gate.sawAllStarts else {
            task.cancel(); gate.releaseAll(); _ = try? await task.value
            return
        }
        gate.releaseModMetadata()
        await fulfillment(of: [gate.modFilesCompleted], timeout: 5)
        XCTAssertEqual(gate.state(of: "extract"), .running)
        XCTAssertEqual(gate.canonicalModDownloads, 1, "Equal size must never substitute for a complete file hash")
        gate.releaseExtraction(); gate.releaseVanilla()
        try await task.value

        let profile = root.appendingPathComponent("versions/Hash checked pack")
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("mods/canonical.jar")), ParallelFixture.bundledMod)
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("mods/原包命名.jar")), wrongMod)
        let report = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: profile.appendingPathComponent("PCL/import-report.json"))) as? [String: Any])
        XCTAssertEqual(report["reusedFiles"] as? Int, 0)
        XCTAssertEqual(report["downloadedFiles"] as? Int, 1)
        try assertSourcesUnchanged(pack)
    }

    func testBundledModLargerThanMetadataLimitReusesWithoutDownload() async throws {
        let root = try directory()
        let largeMod = Data(repeating: 0x53, count: 8 * 1024 * 1024 + 1)
        let pack = try fixture(in: root, bundledMod: largeMod)
        let gate = ParallelFixture(root: root, requiredMod: largeMod)
        let (importer, session) = importer(gate)
        defer { gate.releaseAll(); session.invalidateAndCancel(); ParallelFixtureProtocol.unregister(gate) }
        gate.releaseAll()
        try await importer.importPack(archive: pack.archive, gameDirectory: root, profileID: "Large bundled Mod",
            javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { gate.observe($0) })
        let profile = root.appendingPathComponent("versions/Large bundled Mod")
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("mods/原包命名.jar")), largeMod)
        XCTAssertFalse(FileManager.default.fileExists(atPath: profile.appendingPathComponent("mods/canonical.jar").path))
        XCTAssertEqual(gate.canonicalModDownloads, 0)
        try assertSourcesUnchanged(pack)
    }

    func testRenamedConfigDoesNotSatisfyARequiredConfigPath() async throws {
        try await assertRequiredPathDownloaded(requiredPath: "config/required.toml", overridePath: "config/unrelated.toml")
    }

    func testSameBytesWithDifferentExtensionDoNotSatisfyARequiredMod() async throws {
        try await assertRequiredPathDownloaded(requiredPath: "mods/canonical.jar", overridePath: "mods/not-a-mod.zip")
    }

    func testOnlyEffectiveClientOverrideCanSatisfyTheManifestHash() async throws {
        let root = try directory()
        var clientOverride = ParallelFixture.bundledMod
        clientOverride[clientOverride.startIndex] ^= 1
        let pack = try fixture(in: root, modrinthRequiredPath: "mods/canonical.jar",
            extraMembers: [("client-overrides/mods/原包命名.jar", clientOverride)])
        let gate = ParallelFixture(root: root)
        let (importer, session) = importer(gate)
        defer { gate.releaseAll(); session.invalidateAndCancel(); ParallelFixtureProtocol.unregister(gate) }
        gate.releaseAll()
        try await importer.importPack(archive: pack.archive, gameDirectory: root, profileID: "Effective override pack",
            javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { gate.observe($0) })
        let profile = root.appendingPathComponent("versions/Effective override pack")
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("mods/canonical.jar")), ParallelFixture.bundledMod)
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("mods/原包命名.jar")), clientOverride)
        XCTAssertEqual(gate.canonicalModDownloads, 1, "An overridden common file is no longer part of the effective runtime payload")
        try assertSourcesUnchanged(pack)
    }

    func testCompleteVersionPrivatePayloadWinsAndArchivedMetadataSurvivesBaseRepair() async throws {
        let root = try directory()
        var archivedMetadata = try XCTUnwrap(JSONSerialization.jsonObject(with: ParallelFixture.metadata) as? [String: Any])
        archivedMetadata["mainClass"] = "archived.custom.Main"
        let client = Data("archived custom client".utf8)
        let pack = try sourcePack(in: root, members: [
            ("versions/1.20.4/1.20.4.json", JSONSerialization.data(withJSONObject: archivedMetadata)),
            ("versions/1.20.4/1.20.4.jar", client),
            ("mods/example.jar", Data("global Mod".utf8)),
            ("versions/1.20.4/mods/example.jar", Data("private Mod wins".utf8)),
            ("config/example.txt", Data("global config".utf8)),
            ("versions/1.20.4/config/example.txt", Data("private config wins".utf8))
        ])
        let gate = ParallelFixture(root: root)
        let (importer, session) = importer(gate)
        defer { gate.releaseAll(); session.invalidateAndCancel(); ParallelFixtureProtocol.unregister(gate) }
        gate.releaseAll()
        try await importer.importPack(archive: pack.archive, gameDirectory: root, profileID: "Complete private pack",
            javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { gate.observe($0) })
        let profile = root.appendingPathComponent("versions/Complete private pack")
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("mods/example.jar")), Data("private Mod wins".utf8))
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("config/example.txt")), Data("private config wins".utf8))
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent("Complete private pack.jar")), client)
        let metadata = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: profile.appendingPathComponent("Complete private pack.json"))) as? [String: Any])
        XCTAssertEqual(metadata["mainClass"] as? String, "archived.custom.Main")
        XCTAssertNil(metadata["inheritsFrom"])
        try assertSourcesUnchanged(pack)
    }

    private func assertRequiredPathDownloaded(requiredPath: String, overridePath: String) async throws {
        let root = try directory()
        let pack = try fixture(in: root, overridePath: overridePath, modrinthRequiredPath: requiredPath)
        let gate = ParallelFixture(root: root)
        let (importer, session) = importer(gate)
        defer { gate.releaseAll(); session.invalidateAndCancel(); ParallelFixtureProtocol.unregister(gate) }
        gate.releaseAll()
        try await importer.importPack(archive: pack.archive, gameDirectory: root, profileID: "Required path pack",
            javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { gate.observe($0) })
        let profile = root.appendingPathComponent("versions/Required path pack")
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent(requiredPath)), ParallelFixture.bundledMod)
        XCTAssertEqual(try Data(contentsOf: profile.appendingPathComponent(overridePath)), ParallelFixture.bundledMod)
        XCTAssertEqual(gate.canonicalModDownloads, 1, "A same-byte file that cannot fill the requested runtime path must not suppress its download")
        try assertSourcesUnchanged(pack)
    }

    func testCancellationJoinsBlockedBranchesBeforeRemovingStaging() async throws {
        let root = try directory(), pack = try fixture(in: root)
        let gate = ParallelFixture(root: root)
        let (importer, session) = importer(gate)
        defer { gate.releaseAll(); session.invalidateAndCancel(); ParallelFixtureProtocol.unregister(gate) }
        let task = Task {
            try await importer.importPack(archive: pack.archive, gameDirectory: root, profileID: "Cancelled pack",
                javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { gate.observe($0) })
        }
        await fulfillment(of: [gate.extractionStarted, gate.vanillaRequested, gate.modMetadataRequested], timeout: 5)
        guard gate.sawAllStarts else {
            task.cancel(); gate.releaseAll(); _ = try? await task.value
            return
        }
        task.cancel()
        do { try await task.value; XCTFail("Cancelled branches cannot publish a profile") }
        catch { XCTAssertTrue(task.isCancelled) }
        XCTAssertTrue(gate.extractionCallbackExited, "Cancellation must finish the detached extraction branch before cleanup")
        XCTAssertTrue(gate.extractionObservedCancellation, "Cancellation must be forwarded to detached extraction")
        XCTAssertTrue(gate.stagingExistedWhenExtractionExited, "Do not remove staging while a cancelled branch is still running")
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/Cancelled pack").path), "Cancellation must not leave a partial profile directory")
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: root.path).contains { $0.hasPrefix(".pcl-import-") })
        try assertSourcesUnchanged(pack)
        XCTAssertEqual(gate.canonicalModDownloads, 0)
    }

    func testMetadataFailureCancelsSiblingWorkAndKeepsOriginalFiles() async throws {
        let root = try directory(), pack = try fixture(in: root)
        let gate = ParallelFixture(root: root, invalidMetadata: true)
        let (importer, session) = importer(gate)
        defer { gate.releaseAll(); session.invalidateAndCancel(); ParallelFixtureProtocol.unregister(gate) }
        let task = Task {
            try await importer.importPack(archive: pack.archive, gameDirectory: root, profileID: "Failed pack",
                javaExecutable: URL(fileURLWithPath: "/usr/bin/false"), progress: { gate.observe($0) })
        }
        await fulfillment(of: [gate.extractionStarted, gate.vanillaRequested, gate.modMetadataRequested], timeout: 5)
        guard gate.sawAllStarts else {
            task.cancel(); gate.releaseAll(); _ = try? await task.value
            return
        }
        gate.releaseModMetadata()
        do { try await task.value; XCTFail("A required file with invalid hash metadata must fail") }
        catch ModpackImportError.invalidManifest { }
        catch { XCTFail("Expected metadata validation failure, got \(error)") }
        XCTAssertTrue(gate.extractionCallbackExited, "A failed sibling must cancel and join extraction")
        XCTAssertTrue(gate.extractionObservedCancellation, "The failed metadata branch must cancel detached extraction")
        XCTAssertTrue(gate.stagingExistedWhenExtractionExited, "Do not delete staging before a failed operation has joined all branches")
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("versions/Failed pack").path), "A failed import must not leave a partial profile directory")
        XCTAssertFalse(try FileManager.default.contentsOfDirectory(atPath: root.path).contains { $0.hasPrefix(".pcl-import-") })
        try assertSourcesUnchanged(pack)
        XCTAssertEqual(gate.canonicalModDownloads, 0)
    }
}

private struct ParallelPack {
    let archive: URL
    let original: URL
    let source: Data
    let archiveBytes: Data
    let originalModified: Date?
    let archiveModified: Date?
}

private final class ParallelFixture: @unchecked Sendable {
    enum RequestKind: Equatable { case vanilla, modMetadata, other }
    let id = UUID().uuidString
    let extractionStarted = XCTestExpectation(description: "extract branch running")
    let vanillaRequested = XCTestExpectation(description: "vanilla version JSON request started")
    let modMetadataRequested = XCTestExpectation(description: "Mod metadata request started")
    let extractionCompleted = XCTestExpectation(description: "extract branch completed")
    let modFilesCompleted = XCTestExpectation(description: "Mod files reused before extractor finishes")
    static let bundledMod = Data("exact whole bundled Mod JAR fixture".utf8)
    static let client = Data("vanilla client fixture".utf8)
    static let index = Data(#"{"objects":{}}"#.utf8)
    let invalidMetadata: Bool
    private let requiredMod: Data
    private let root: URL
    private let condition = NSCondition()
    private var stages: [String: InstallStageState] = [:]
    private var startedRequests = Set<String>()
    private var extractionClaimed = false
    private var extractionExited = false
    private var extractionWasCancelled = false
    private var stagingAtExtractionExit = false
    private var extractionReleased = false
    private var vanillaReleased = false
    private var modsReleased = false
    private var stageCompleted = Set<String>()
    private var requestOverlap = true
    private var modDownloads = 0
    private var lastRevision: Int64 = -1

    init(root: URL, invalidMetadata: Bool = false, requiredMod: Data = ParallelFixture.bundledMod) {
        self.root = root; self.invalidMetadata = invalidMetadata; self.requiredMod = requiredMod
    }
    var sawAllStarts: Bool { condition.lock(); defer { condition.unlock() }; return extractionClaimed && startedRequests.count == 2 }
    var requestsPrecededExtractionCompletion: Bool { condition.lock(); defer { condition.unlock() }; return requestOverlap }
    var vanillaResponseWasReleased: Bool { condition.lock(); defer { condition.unlock() }; return vanillaReleased }
    var canonicalModDownloads: Int { condition.lock(); defer { condition.unlock() }; return modDownloads }
    var extractionCallbackExited: Bool { condition.lock(); defer { condition.unlock() }; return extractionExited }
    var extractionObservedCancellation: Bool { condition.lock(); defer { condition.unlock() }; return extractionWasCancelled }
    var stagingExistedWhenExtractionExited: Bool { condition.lock(); defer { condition.unlock() }; return stagingAtExtractionExit }
    func state(of id: String) -> InstallStageState? { condition.lock(); defer { condition.unlock() }; return stages[id] }

    func observe(_ value: InstallProgress) {
        guard let snapshot = value.snapshot else { return }
        condition.lock()
        if snapshot.revision >= lastRevision {
            lastRevision = snapshot.revision
            for stage in snapshot.stages { stages[stage.id] = stage.state }
        }
        let shouldHold = InstallProgressContext.stageID == "extract" && stages["extract"] == .running && !extractionClaimed
        if shouldHold { extractionClaimed = true }
        let extractDone = stages["extract"] == .completed && stageCompleted.insert("extract").inserted
        let modsDone = stages["modFiles"] == .completed && stageCompleted.insert("modFiles").inserted
        condition.broadcast(); condition.unlock()
        if extractDone { extractionCompleted.fulfill() }
        if modsDone { modFilesCompleted.fulfill() }
        guard shouldHold else { return }
        extractionStarted.fulfill()
        condition.lock()
        let deadline = Date().addingTimeInterval(8)
        while !extractionReleased && !Task.isCancelled && Date() < deadline {
            _ = condition.wait(until: min(deadline, Date().addingTimeInterval(0.05)))
        }
        extractionWasCancelled = Task.isCancelled
        stagingAtExtractionExit = (try? FileManager.default.contentsOfDirectory(atPath: root.path))?.contains { $0.hasPrefix(".pcl-import-") } == true
        extractionExited = true
        condition.broadcast(); condition.unlock()
    }

    func began(_ request: URLRequest) -> RequestKind {
        let url = request.url!
        let kind: RequestKind = url.path == "/version.json" ? .vanilla : (url.host == "mod.mcimirror.top" && url.path.hasSuffix("/mods/files") ? .modMetadata : .other)
        condition.lock()
        let key = kind == .vanilla ? "vanilla" : "modMetadata"
        let first = kind != .other && startedRequests.insert(key).inserted
        if kind != .other { requestOverlap = requestOverlap && stages["extract"] != .completed }
        if url.lastPathComponent == "canonical.jar" { modDownloads += 1 }
        condition.broadcast(); condition.unlock()
        if first { (kind == .vanilla ? vanillaRequested : modMetadataRequested).fulfill() }
        return kind
    }

    func waitUntilReleased(_ kind: RequestKind, stopped: () -> Bool) -> Bool {
        condition.lock(); defer { condition.unlock() }
        let deadline = Date().addingTimeInterval(10)
        while !stopped() && Date() < deadline {
            if kind == .other || (kind == .vanilla && vanillaReleased) || (kind == .modMetadata && modsReleased) { return true }
            _ = condition.wait(until: min(deadline, Date().addingTimeInterval(0.05)))
        }
        return false
    }
    func wake() { condition.lock(); condition.broadcast(); condition.unlock() }
    func releaseVanilla() { condition.lock(); vanillaReleased = true; condition.broadcast(); condition.unlock() }
    func releaseModMetadata() { condition.lock(); modsReleased = true; condition.broadcast(); condition.unlock() }
    func releaseExtraction() { condition.lock(); extractionReleased = true; condition.broadcast(); condition.unlock() }
    func releaseAll() { condition.lock(); vanillaReleased = true; modsReleased = true; extractionReleased = true; condition.broadcast(); condition.unlock() }

    static var metadata: Data {
        try! JSONSerialization.data(withJSONObject: ["id": "1.20.4", "type": "release", "mainClass": "net.minecraft.client.main.Main", "libraries": [],
            "javaVersion": ["majorVersion": 17], "arguments": ["jvm": [], "game": []],
            "downloads": ["client": ["url": "https://pcl-parallel.invalid/client.jar", "size": client.count, "sha1": MinecraftInstaller.hash(client)]],
            "assetIndex": ["id": "parallel", "url": "https://pcl-parallel.invalid/index.json", "sha1": MinecraftInstaller.hash(index), "size": index.count]], options: [.sortedKeys])
    }
    func response(for url: URL) throws -> Data {
        switch url.lastPathComponent {
        case "manifest": return try JSONSerialization.data(withJSONObject: ["latest": ["release": "1.20.4", "snapshot": "1.20.4"], "versions": [["id": "1.20.4", "type": "release", "url": "https://pcl-parallel.invalid/version.json", "sha1": MinecraftInstaller.hash(Self.metadata)]]])
        case "version.json": return Self.metadata
        case "index.json": return Self.index
        case "client.jar": return Self.client
        case "canonical.jar": return requiredMod
        case "files": return try JSONSerialization.data(withJSONObject: ["data": [["id": 456, "modId": 123, "fileName": "canonical.jar", "fileLength": requiredMod.count,
            "downloadUrl": "https://mediafilez.forgecdn.net/files/0/456/canonical.jar",
            "hashes": [["algo": 1, "value": invalidMetadata ? "invalid" : MinecraftInstaller.hash(requiredMod)]], "modules": [["name": "fabric.mod.json"]]]]])
        default: throw URLError(.resourceUnavailable)
        }
    }
}

private final class ParallelFixtureProtocol: URLProtocol {
    private static let registryLock = NSLock()
    private static var fixtures: [String: ParallelFixture] = [:]
    private let stoppedLock = NSLock()
    private var stopped = false
    private var fixture: ParallelFixture?
    static func register(_ fixture: ParallelFixture) { registryLock.lock(); fixtures[fixture.id] = fixture; registryLock.unlock() }
    static func unregister(_ fixture: ParallelFixture) { registryLock.lock(); fixtures.removeValue(forKey: fixture.id); registryLock.unlock() }
    private static func fixture(for request: URLRequest) -> ParallelFixture? {
        registryLock.lock(); defer { registryLock.unlock() }
        if let id = request.value(forHTTPHeaderField: "X-PCL-Parallel-Test-ID") { return fixtures[id] }
        return fixtures.count == 1 ? fixtures.values.first : nil
    }
    private var isStopped: Bool { stoppedLock.lock(); defer { stoppedLock.unlock() }; return stopped }
    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        guard let fixture = Self.fixture(for: request), let url = request.url else {
            client?.urlProtocol(self, didFailWithError: URLError(.resourceUnavailable)); return
        }
        self.fixture = fixture
        let kind = fixture.began(request)
        DispatchQueue.global(qos: .userInitiated).async { [self] in
            guard fixture.waitUntilReleased(kind, stopped: { self.isStopped }), !isStopped else { return }
            do {
                let body = try fixture.response(for: url)
                guard !isStopped else { return }
                let response = HTTPURLResponse(url: url, statusCode: 200, httpVersion: "HTTP/1.1", headerFields: ["Content-Length": String(body.count)])!
                client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
                client?.urlProtocol(self, didLoad: body)
                client?.urlProtocolDidFinishLoading(self)
            } catch { if !isStopped { client?.urlProtocol(self, didFailWithError: error) } }
        }
    }
    override func stopLoading() { stoppedLock.lock(); stopped = true; stoppedLock.unlock(); fixture?.wake() }
}

private func parallelZIP(_ members: [(String, Data)]) throws -> Data {
    var bytes = Data(), central = Data()
    func checksum(_ value: Data) -> UInt32 {
        value.withUnsafeBytes { UInt32(truncatingIfNeeded: crc32(0, $0.bindMemory(to: Bytef.self).baseAddress, uInt(value.count))) }
    }
    for (path, body) in members {
        let name = Data(path.utf8), size = UInt32(body.count), offset = UInt32(bytes.count), crc = checksum(body)
        bytes.parallel32(0x04034b50); bytes.parallel16(20); bytes.parallel16(0x800); bytes.parallel16(0); bytes.parallel16(0); bytes.parallel16(0)
        bytes.parallel32(crc); bytes.parallel32(size); bytes.parallel32(size); bytes.parallel16(UInt16(name.count)); bytes.parallel16(0)
        bytes.append(name); bytes.append(body)
        central.parallel32(0x02014b50); central.parallel16(0x0314); central.parallel16(20); central.parallel16(0x800); central.parallel16(0); central.parallel16(0); central.parallel16(0)
        central.parallel32(crc); central.parallel32(size); central.parallel32(size); central.parallel16(UInt16(name.count)); central.parallel16(0); central.parallel16(0)
        central.parallel16(0); central.parallel16(0); central.parallel32(UInt32(0o100644) << 16); central.parallel32(offset); central.append(name)
    }
    let start = UInt32(bytes.count)
    bytes.append(central); bytes.parallel32(0x06054b50); bytes.parallel16(0); bytes.parallel16(0); bytes.parallel16(UInt16(members.count)); bytes.parallel16(UInt16(members.count))
    bytes.parallel32(UInt32(central.count)); bytes.parallel32(start); bytes.parallel16(0)
    return bytes
}
private extension Data {
    mutating func parallel16(_ value: UInt16) { append(UInt8(truncatingIfNeeded: value)); append(UInt8(truncatingIfNeeded: value >> 8)) }
    mutating func parallel32(_ value: UInt32) { parallel16(UInt16(truncatingIfNeeded: value)); parallel16(UInt16(truncatingIfNeeded: value >> 16)) }
}
