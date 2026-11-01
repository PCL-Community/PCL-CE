import Foundation
import XCTest
@testable import PCLCore

final class ForgeSourceFallbackTests: XCTestCase {
    private func installer(_ fixture: ForgeSourceFixture) -> ModLoaderInstaller {
        ForgeSourceProtocol.register(fixture)
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [ForgeSourceProtocol.self]
        config.httpAdditionalHeaders = ["X-PCL-Forge-Source-Fixture": fixture.id]
        let session = URLSession(configuration: config)
        addTeardownBlock { session.invalidateAndCancel(); ForgeSourceProtocol.unregister(fixture.id) }
        return ModLoaderInstaller(session: session)
    }

    func testFastOfficialCatalogueCancelsDelayedMirrorBeforeItRequestsAnything() async throws {
        let fixture = ForgeSourceFixture(officialDelay: 0.001)
        let values = try await installer(fixture).fetchVersions(kind: .forge, minecraftVersion: "1.20.1")
        XCTAssertEqual(values.map(\.version), ["47.2.20", "47.1.0"])
        XCTAssertTrue(try XCTUnwrap(values.first).recommended)
        XCTAssertEqual(fixture.requests("official"), 1)
        XCTAssertEqual(fixture.requests("promotions"), 1)
        XCTAssertEqual(fixture.requests("mirror"), 0)
    }

    func testReachableMirrorWinsWithoutWaitingForSlowOfficialAndCancelsItsRequest() async throws {
        let fixture = ForgeSourceFixture(officialDelay: 5)
        let start = Date()
        let values = try await installer(fixture).fetchVersions(kind: .forge, minecraftVersion: "1.20.1")
        XCTAssertEqual(values.map(\.version), ["47.2.0"])
        XCTAssertLessThan(Date().timeIntervalSince(start), 2, "The losing official request must be cancelled and drained promptly")
        XCTAssertEqual(fixture.requests("mirror"), 1)
        await fulfillment(of: [fixture.officialCancellation], timeout: 1)
        XCTAssertEqual(fixture.cancellations("official"), 1)
        XCTAssertEqual(fixture.requests("promotions"), 0)
    }

    func testEmptyMirrorDoesNotDiscardALaterValidOfficialCatalogue() async throws {
        let fixture = ForgeSourceFixture(officialDelay: 0.7, mirrorVersions: [])
        let values = try await installer(fixture).fetchVersions(kind: .forge, minecraftVersion: "1.20.1")
        XCTAssertEqual(values.map(\.version), ["47.2.20", "47.1.0"])
        XCTAssertEqual(fixture.requests("mirror"), 1)
    }

    func testUnsupportedMinecraftReturnsEmptyAfterBothSourcesAnswerEmpty() async throws {
        let fixture = ForgeSourceFixture(officialDelay: 0.001, officialVersions: [], mirrorVersions: [])
        let values = try await installer(fixture).fetchVersions(kind: .forge, minecraftVersion: "1.20.1")
        XCTAssertTrue(values.isEmpty)
        XCTAssertEqual(fixture.requests("official"), 1)
        XCTAssertEqual(fixture.requests("mirror"), 1)
        XCTAssertEqual(fixture.requests("promotions"), 0)
    }

    func testCallerCancellationStopsOfficialAndNeverStartsDelayedMirror() async throws {
        let fixture = ForgeSourceFixture(officialDelay: 5)
        let loader = installer(fixture)
        let task = Task { try await loader.fetchVersions(kind: .forge, minecraftVersion: "1.20.1") }
        for _ in 0..<30 where fixture.requests("official") == 0 { try await Task.sleep(nanoseconds: 5_000_000) }
        XCTAssertEqual(fixture.requests("official"), 1)
        task.cancel()
        do { _ = try await task.value; XCTFail("Cancelled lookup must not publish catalogue results") }
        catch { XCTAssertTrue(error is CancellationError, "\(error)") }
        XCTAssertEqual(fixture.requests("mirror"), 0)
        await fulfillment(of: [fixture.officialCancellation], timeout: 1)
        XCTAssertEqual(fixture.cancellations("official"), 1)
    }
}

private final class ForgeSourceFixture: @unchecked Sendable {
    let id = UUID().uuidString
    let officialDelay: TimeInterval
    let officialVersions: [String]
    let mirrorVersions: [String]
    let officialCancellation = XCTestExpectation(description: "The official URLProtocol request receives stopLoading")
    private let lock = NSLock()
    private var requestCounts: [String: Int] = [:]
    private var cancellationCounts: [String: Int] = [:]
    init(officialDelay: TimeInterval, officialVersions: [String] = ["47.2.20", "47.1.0"], mirrorVersions: [String] = ["47.2.0"]) {
        self.officialDelay = officialDelay; self.officialVersions = officialVersions; self.mirrorVersions = mirrorVersions
    }
    func began(_ source: String) { lock.lock(); requestCounts[source, default: 0] += 1; lock.unlock() }
    func cancelled(_ source: String) {
        lock.lock(); cancellationCounts[source, default: 0] += 1; lock.unlock()
        if source == "official" { officialCancellation.fulfill() }
    }
    func requests(_ source: String) -> Int { lock.lock(); defer { lock.unlock() }; return requestCounts[source, default: 0] }
    func cancellations(_ source: String) -> Int { lock.lock(); defer { lock.unlock() }; return cancellationCounts[source, default: 0] }
    func response(_ url: URL) -> (String, Data, TimeInterval, Int) {
        if url.host == "maven.minecraftforge.net" {
            let xml = "<metadata><versioning><versions>" + officialVersions.map { "<version>1.20.1-\($0)</version>" }.joined() + "</versions></versioning></metadata>"
            return ("official", Data(xml.utf8), officialDelay, 200)
        }
        if url.host == "files.minecraftforge.net" {
            return ("promotions", Data(#"{"promos":{"1.20.1-recommended":"47.2.20"}}"#.utf8), 0.001, 200)
        }
        if url.host == "bmclapi2.bangbang93.com", url.path.hasPrefix("/forge/minecraft/") {
            let entries: [[String: Any]] = mirrorVersions.map { ["version": $0, "files": [["category": "installer", "format": "jar", "hash": String(repeating: "a", count: 40)]]] }
            return ("mirror", try! JSONSerialization.data(withJSONObject: entries), 0.001, 200)
        }
        return ("unexpected", Data(), 0, 404)
    }
}

private final class ForgeSourceProtocol: URLProtocol, @unchecked Sendable {
    private static let registryLock = NSLock()
    private static var fixtures: [String: ForgeSourceFixture] = [:]
    static func register(_ fixture: ForgeSourceFixture) { registryLock.lock(); fixtures[fixture.id] = fixture; registryLock.unlock() }
    static func unregister(_ id: String) { registryLock.lock(); fixtures[id] = nil; registryLock.unlock() }
    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    private let lock = NSLock()
    private var fixture: ForgeSourceFixture?
    private var source = ""
    private var finished = false
    private var stopped = false
    private var work: DispatchWorkItem?
    override func startLoading() {
        Self.registryLock.lock()
        let current = request.value(forHTTPHeaderField: "X-PCL-Forge-Source-Fixture").flatMap { Self.fixtures[$0] }
        Self.registryLock.unlock()
        guard let current, let url = request.url else { client?.urlProtocol(self, didFailWithError: URLError(.badURL)); return }
        let (source, bytes, delay, status) = current.response(url)
        let item = DispatchWorkItem { [weak self] in
            guard let self else { return }
            self.lock.lock()
            guard !self.stopped else { self.lock.unlock(); return }
            self.finished = true
            self.lock.unlock()
            let response = HTTPURLResponse(url: url, statusCode: status, httpVersion: "HTTP/1.1", headerFields: ["Content-Length": String(bytes.count)])!
            self.client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            self.client?.urlProtocol(self, didLoad: bytes)
            self.client?.urlProtocolDidFinishLoading(self)
        }
        lock.lock(); self.fixture = current; self.source = source; self.work = item; lock.unlock()
        current.began(source)
        DispatchQueue.global(qos: .userInitiated).asyncAfter(deadline: .now() + delay, execute: item)
    }
    override func stopLoading() {
        lock.lock()
        guard !finished, !stopped else { lock.unlock(); return }
        stopped = true
        work?.cancel()
        let current = fixture, source = source
        lock.unlock()
        current?.cancelled(source)
    }
}
