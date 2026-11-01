import Foundation
import XCTest
@testable import PCLCore

final class CurseForgeResolverTests: XCTestCase {
    private func resolver(_ mode: String, inheritedKey: String? = nil) -> CurseForgeResolver {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [CurseForgeResolverFixture.self]
        configuration.httpAdditionalHeaders = ["X-Resolver-Fixture": mode]
        if let inheritedKey { configuration.httpAdditionalHeaders?["x-api-key"] = inheritedKey }
        let session = URLSession(configuration: configuration)
        addTeardownBlock { session.invalidateAndCancel() }
        return CurseForgeResolver(session: session)
    }
    private let first = CurseForgeFileReference(projectID: 101, fileID: 7_668_919)
    private let second = CurseForgeFileReference(projectID: 102, fileID: 7_472_801)

    func testMirrorBatchWorksWithoutKeyPreservesRequestedOrderAndExactIdentity() async throws {
        let mode = "batch-\(UUID().uuidString)"
        let files = try await resolver(mode).resolve([first, second, first])
        XCTAssertEqual(files.map(\.fileID), [first.fileID, second.fileID])
        XCTAssertEqual(files.map(\.projectID), [101, 102])
        XCTAssertEqual(files.first?.path, "mods/fixture-7668919.jar")
        let requests = CurseForgeResolverFixture.requests(mode)
        XCTAssertEqual(requests.count, 1)
        XCTAssertEqual(requests.first?.httpMethod, "POST")
        XCTAssertEqual(requests.first?.url?.host, CurseForgeResolver.mirrorHost)
        XCTAssertNil(requests.first?.value(forHTTPHeaderField: "x-api-key"))
        XCTAssertEqual(requests.first?.value(forHTTPHeaderField: "User-Agent"), CurseForgeResolver.userAgent)
        let body = try XCTUnwrap(requests.first?.httpBody)
        XCTAssertEqual((try JSONSerialization.jsonObject(with: body) as? [String: [Int]])?["fileIds"], [first.fileID, second.fileID])
    }

    func testMissingBatchMemberUsesItsProjectScopedGetEndpoint() async throws {
        let mode = "partial-\(UUID().uuidString)"
        let files = try await resolver(mode).resolve([first, second])
        XCTAssertEqual(files.count, 2)
        let requests = CurseForgeResolverFixture.requests(mode)
        XCTAssertEqual(requests.count, 2)
        XCTAssertEqual(requests.last?.url?.path, "/curseforge/v1/mods/102/files/7472801")
    }

    func testUnavailableBatchEndpointFallsBackToExactIndividualFiles() async throws {
        let mode = "no-batch-\(UUID().uuidString)"
        let files = try await resolver(mode).resolve([first])
        XCTAssertEqual(files.first?.fileID, first.fileID)
        XCTAssertEqual(CurseForgeResolverFixture.requests(mode).count, 2)
    }

    func testMissingRequiredFileFailsRatherThanReturningPartialSuccess() async throws {
        let mode = "missing-\(UUID().uuidString)"
        do {
            _ = try await resolver(mode).resolve([first, second])
            XCTFail("A missing required file cannot be silently omitted")
        } catch ModpackImportError.unavailableFile(let message) {
            XCTAssertTrue(message.contains("102"))
            XCTAssertTrue(message.contains("7472801"))
        }
        XCTAssertFalse(CurseForgeResolverFixture.requests(mode).contains { $0.url?.host == CurseForgeResolver.officialHost })
    }

    func testExactHistoricalResourceAttachmentsResolveAfterConfirmedNotFound() async throws {
        let mode = "legacy-gone-\(UUID().uuidString)"
        let refs = [3_811_549, 3_811_554].map { CurseForgeFileReference(projectID: 338_808, fileID: $0) }
        let files = try await resolver(mode).resolve(refs)
        XCTAssertEqual(files.map(\.path), ["resourcepacks/XKSP 花团锦簇 lush flowers.zip", "resourcepacks/XKSP 披花戴叶 flower armor.zip"])
        XCTAssertEqual(files.map(\.size), [213_889, 131_951])
        XCTAssertEqual(files.map(\.sha1), ["4a3e8512007d224535adc94640ba0a43ab01a2b6", "c3e8c41abed1631f9efc486a5c48ba8fd2f8231a"])
        XCTAssertTrue(files.allSatisfy { $0.urls.first?.host == "mediafilez.forgecdn.net" && $0.fingerprint == nil })
        XCTAssertTrue(CurseForgeResolverFixture.requests(mode).allSatisfy { $0.url?.host == CurseForgeResolver.mirrorHost })
    }

    func testHistoricalPinsCannotSatisfyWrongProjectOtherFileOrPermissionFailure() async throws {
        for (reference, prefix) in [(CurseForgeFileReference(projectID: 999, fileID: 3_811_549), "legacy-gone"),
                                    (CurseForgeFileReference(projectID: 338_808, fileID: 3_811_550), "legacy-gone"),
                                    (CurseForgeFileReference(projectID: 338_808, fileID: 3_811_549), "legacy-denied")] {
            do {
                _ = try await resolver("\(prefix)-\(UUID().uuidString)").resolve([reference])
                XCTFail("Only exact public attachments with a confirmed missing API record may use pins")
            } catch ModpackImportError.unavailableFile { }
        }
    }

    func testApplicationKeyIsNeverSentToMirrorIncludingInheritedSessionHeaders() async throws {
        let mode = "official-fallback-\(UUID().uuidString)"
        let files = try await resolver(mode, inheritedKey: "inherited-secret").resolve([first], apiKey: "application-fixture-key")
        XCTAssertEqual(files.count, 1)
        let requests = CurseForgeResolverFixture.requests(mode)
        let mirror = requests.filter { $0.url?.host == CurseForgeResolver.mirrorHost }
        XCTAssertEqual(mirror.count, 2)
        XCTAssertTrue(mirror.allSatisfy { $0.value(forHTTPHeaderField: "x-api-key") == nil })
        let official = try XCTUnwrap(requests.first { $0.url?.host == CurseForgeResolver.officialHost })
        XCTAssertEqual(official.value(forHTTPHeaderField: "x-api-key"), "application-fixture-key")
    }

    func testRedirectPolicyRejectsCredentialExfiltrationAndDowngrade() {
        for url in ["http://api.curseforge.com/v1/mods", "https://evil.invalid/v1/mods", "https://api.curseforge.com.evil.invalid/x",
                    "https://user:password@api.curseforge.com/v1/mods", "https://api.curseforge.com:8443/v1/mods"] {
            XCTAssertFalse(CurseForgeMetadataRedirectPolicy.allows(URLRequest(url: URL(string: url)!), host: CurseForgeResolver.officialHost))
        }
        XCTAssertTrue(CurseForgeMetadataRedirectPolicy.allows(URLRequest(url: URL(string: "https://api.curseforge.com/v1/mods")!), host: CurseForgeResolver.officialHost))
        XCTAssertFalse(CurseForgeMetadataRedirectPolicy.allows(URLRequest(url: URL(string: "https://mod.mcimirror.top/curseforge/v1/mods")!), host: CurseForgeResolver.officialHost))
    }

    func testNullDownloadURLBuildsOriginalFileIDCandidatesAndEncodesNames() throws {
        let file = try CurseForgeResolver.parse(Self.file(first, name: "测试 + name.jar", nullURL: true), reference: first)
        XCTAssertEqual(file.urls.first?.host, "mediafilez.forgecdn.net")
        XCTAssertEqual(file.urls.first?.path.removingPercentEncoding, "/files/7668/919/测试 + name.jar")
        XCTAssertTrue(file.urls.contains { $0.host == CurseForgeResolver.mirrorHost })
        XCTAssertEqual(file.sha1, String(repeating: "a", count: 40))
        XCTAssertEqual(file.md5, String(repeating: "b", count: 32))
        XCTAssertEqual(file.fingerprint, 1_032_587_124)
        let future = try CurseForgeResolver.downloadURLs(rawURL: nil, fileID: 10_123_456, fileName: "future.jar")
        XCTAssertEqual(future.first?.path, "/files/10123/456/future.jar")
    }

    func testOriginalURLCandidatesKeepEscapingAndUseTrustedCDNHosts() throws {
        let url = "https://edge.forgecdn.net/files/7472/801/fabric-api-0.92.7%2b1.20.1.jar"
        let values = try CurseForgeResolver.downloadURLs(rawURL: url, fileID: 7_472_801, fileName: "fabric-api-0.92.7+1.20.1.jar")
        XCTAssertEqual(values.first?.host, "mediafilez.forgecdn.net")
        XCTAssertTrue(values.first?.absoluteString.contains("%2b") == true)
        for raw in ["http://edge.forgecdn.net/file.jar", "https://evil.invalid/file.jar", "https://edge.forgecdn.net:8443/file.jar", "https://user:secret@edge.forgecdn.net/file.jar"] {
            XCTAssertThrowsError(try CurseForgeResolver.downloadURLs(rawURL: raw, fileID: first.fileID, fileName: "file.jar"))
        }
    }

    func testResourceClassificationMatchesOriginalPackFlow() throws {
        let cases: [(String, [String]?, String)] = [
            ("mod.jar", ["pack.mcmeta"], "mods"), ("legacy.zip", ["META-INF"], "mods"),
            ("resource.zip", ["pack.mcmeta", "assets"], "resourcepacks"),
            ("shader.zip", ["shaders"], "shaderpacks"), ("world.zip", ["level.dat", "region"], "saves"),
            ("unknown.jar", nil, "mods"), ("unknown.zip", nil, "mods")
        ]
        for (name, modules, folder) in cases {
            var metadata = Self.file(first, name: name)
            if let modules { metadata["modules"] = modules.map { ["name": $0] } }
            else { metadata["modules"] = NSNull() }
            XCTAssertEqual(try CurseForgeResolver.parse(metadata, reference: first).path, folder + "/" + name)
        }
    }

    func testMalformedIdentityPathSizeHashAndUnsafeURLAreRejected() throws {
        let changes: [[String: Any]] = [
            ["id": first.fileID + 1], ["modId": first.projectID + 1], ["fileName": "../outside.jar"],
            ["fileName": "bad\\outside.jar"], ["fileName": "bad:name.jar"], ["fileLength": -1], ["fileLength": true],
            ["hashes": [["algo": 1, "value": "malformed"]]], ["hashes": []], ["downloadUrl": "https://evil.invalid/payload.jar"]
        ]
        for change in changes {
            var file = Self.file(first)
            file.merge(change) { _, new in new }
            XCTAssertThrowsError(try CurseForgeResolver.parse(file, reference: first), String(describing: change.keys))
        }
        var onlyMD5 = Self.file(first)
        onlyMD5["hashes"] = [["algo": 2, "value": String(repeating: "b", count: 32)]]
        XCTAssertNil(try CurseForgeResolver.parse(onlyMD5, reference: first).sha1)
    }

    func testBatchRejectsMismatchedReturnedProject() async throws {
        do {
            _ = try await resolver("wrong-project-\(UUID().uuidString)").resolve([first])
            XCTFail("The response must match the manifest's project as well as its file ID")
        } catch ModpackImportError.invalidManifest { }
    }

    func testConflictingReferencesAndCancelledTaskDoNotRequestAnything() async throws {
        let mode = "cancel-\(UUID().uuidString)"
        let resolver = resolver(mode)
        do {
            _ = try await resolver.resolve([first, .init(projectID: 999, fileID: first.fileID)])
            XCTFail("Conflicting project IDs must be rejected")
        } catch ModpackImportError.invalidManifest { }
        let task = Task { try await resolver.resolve([first]) }
        task.cancel()
        do { _ = try await task.value; XCTFail("Cancellation must stop metadata lookup") }
        catch is CancellationError { }
        XCTAssertTrue(CurseForgeResolverFixture.requests(mode).isEmpty)
    }

    fileprivate static func file(_ reference: CurseForgeFileReference, name: String? = nil, nullURL: Bool = false) -> [String: Any] {
        let name = name ?? "fixture-\(reference.fileID).jar"
        return ["id": reference.fileID, "modId": reference.projectID, "fileName": name, "fileLength": 123,
                "downloadUrl": nullURL ? NSNull() : "https://edge.forgecdn.net/files/\(reference.fileID / 1000)/\(reference.fileID % 1000)/\(name)",
                "hashes": [["algo": 1, "value": String(repeating: "a", count: 40)], ["algo": 2, "value": String(repeating: "b", count: 32)]],
                "fileFingerprint": 1_032_587_124, "modules": [["name": "fabric.mod.json"]]]
    }
}

private final class CurseForgeResolverFixture: URLProtocol {
    private static let lock = NSLock()
    private static var recorded: [String: [URLRequest]] = [:]
    static func requests(_ mode: String) -> [URLRequest] { lock.lock(); defer { lock.unlock() }; return recorded[mode] ?? [] }
    override class func canInit(with request: URLRequest) -> Bool {
        [CurseForgeResolver.mirrorHost, CurseForgeResolver.officialHost].contains(request.url?.host ?? "")
    }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        guard let url = request.url else { return }
        let mode = request.value(forHTTPHeaderField: "X-Resolver-Fixture") ?? ""
        var captured = request
        captured.httpBody = Self.bodyData(request)
        Self.lock.lock(); Self.recorded[mode, default: []].append(captured); Self.lock.unlock()
        let official = url.host == CurseForgeResolver.officialHost
        let batch = request.httpMethod == "POST"
        var status = 200
        let payload: [String: Any]
        if batch {
            let ids = (captured.httpBody.flatMap { try? JSONSerialization.jsonObject(with: $0) as? [String: [Int]] })?["fileIds"] ?? []
            var files = ids.reversed().map { id in
                CurseForgeResolverTests.file(.init(projectID: id == 7_668_919 ? 101 : 102, fileID: id))
            }
            if mode.hasPrefix("wrong-project") { files = files.map { item in var item = item; item["modId"] = 999; return item } }
            if mode.hasPrefix("partial") || mode.hasPrefix("missing") { files.removeAll { $0["id"] as? Int == 7_472_801 } }
            if mode.hasPrefix("legacy-") { files = [] }
            if mode.hasPrefix("no-batch") || mode.hasPrefix("official-fallback") { status = 404 }
            payload = ["data": files]
        } else {
            let parts = url.pathComponents
            let id = Int(parts.last ?? "") ?? 0
            let project = Int(parts.dropLast(2).last ?? "") ?? 0
            payload = ["data": CurseForgeResolverTests.file(.init(projectID: project, fileID: id))]
            if mode.hasPrefix("missing") || (mode.hasPrefix("official-fallback") && !official) { status = 404 }
            if mode.hasPrefix("legacy-gone") { status = 404 }
            if mode.hasPrefix("legacy-denied") { status = 403 }
        }
        let data = try! JSONSerialization.data(withJSONObject: payload)
        client?.urlProtocol(self, didReceive: HTTPURLResponse(url: url, statusCode: status, httpVersion: nil, headerFields: ["Content-Type": "application/json"])!, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() { }
    private static func bodyData(_ request: URLRequest) -> Data? {
        if let data = request.httpBody { return data }
        guard let stream = request.httpBodyStream else { return nil }
        stream.open(); defer { stream.close() }
        var data = Data(), buffer = [UInt8](repeating: 0, count: 4096)
        while stream.hasBytesAvailable { let count = stream.read(&buffer, maxLength: buffer.count); if count <= 0 { break }; data.append(buffer, count: count) }
        return data
    }
}
