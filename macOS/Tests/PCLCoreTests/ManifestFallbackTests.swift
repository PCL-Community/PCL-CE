import XCTest
@testable import PCLCore

final class ManifestFallbackTests: XCTestCase {
    private func session(primaryFailure: String = "http") -> (URLSession, String) {
        let run = UUID().uuidString
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [ManifestFallbackProtocol.self]
        configuration.httpAdditionalHeaders = ["X-Fixture-Run": run, "X-Primary-Failure": primaryFailure]
        let session = URLSession(configuration: configuration)
        addTeardownBlock { session.invalidateAndCancel() }
        return (session, run)
    }

    func testDefaultManifestFallsBackToOtherOfficialHost() async throws {
        let (session, run) = session()
        let manifest = try await MinecraftInstaller(session: session).fetchManifest()
        XCTAssertEqual(manifest.latest.release, "1.20.4")
        XCTAssertEqual(ManifestFallbackProtocol.count(host: "piston-meta.mojang.com", run: run), 1)
        XCTAssertEqual(ManifestFallbackProtocol.count(host: "launchermeta.mojang.com", run: run), 1)
    }

    func testCustomManifestFailureNeverUsesOfficialHosts() async throws {
        let (session, run) = session()
        let custom = URL(string: "https://manifest-fixture.invalid/custom.json")!
        do {
            _ = try await MinecraftInstaller(session: session, manifestURL: custom).fetchManifest()
            XCTFail("The explicit custom source should fail, not change source")
        } catch let error as MinecraftInstallError {
            guard case .http(503, _) = error else { return XCTFail("Unexpected error: \(error)") }
        }
        XCTAssertEqual(ManifestFallbackProtocol.count(host: "manifest-fixture.invalid", run: run), 1)
        XCTAssertEqual(ManifestFallbackProtocol.count(host: "piston-meta.mojang.com", run: run), 0)
        XCTAssertEqual(ManifestFallbackProtocol.count(host: "launchermeta.mojang.com", run: run), 0)
    }

    func testDefaultManifestRecoversFromRepeatedTLSFailuresUsingOfficialAlias() async throws {
        let (session, run) = session(primaryFailure: "tls")
        let manifest = try await MinecraftInstaller(session: session).fetchManifest()
        XCTAssertEqual(manifest.latest.release, "1.20.4")
        // Three bounded metadata attempts, each with one TLS 1.2 compatibility retry.
        XCTAssertEqual(ManifestFallbackProtocol.count(host: "piston-meta.mojang.com", run: run), 6)
        XCTAssertEqual(ManifestFallbackProtocol.count(host: "launchermeta.mojang.com", run: run), 1)
    }

    func testCertificateRejectionDoesNotRetryOrChangeManifestHost() async throws {
        let (session, run) = session(primaryFailure: "certificate")
        do {
            _ = try await MinecraftInstaller(session: session).fetchManifest()
            XCTFail("Certificate rejection must reach the caller")
        } catch let error as URLError { XCTAssertEqual(error.code, .serverCertificateUntrusted) }
        XCTAssertEqual(ManifestFallbackProtocol.count(host: "piston-meta.mojang.com", run: run), 1)
        XCTAssertEqual(ManifestFallbackProtocol.count(host: "launchermeta.mojang.com", run: run), 0)
    }
}

private final class ManifestFallbackProtocol: URLProtocol {
    private static let lock = NSLock()
    private static var requests: [String: Int] = [:]
    static func count(host: String, run: String) -> Int {
        lock.lock(); defer { lock.unlock() }
        return requests[run + ":" + host, default: 0]
    }
    override class func canInit(with request: URLRequest) -> Bool {
        ["piston-meta.mojang.com", "launchermeta.mojang.com", "manifest-fixture.invalid"].contains(request.url?.host ?? "")
    }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        guard let url = request.url else { return }
        let key = (request.value(forHTTPHeaderField: "X-Fixture-Run") ?? "") + ":" + (url.host ?? "")
        Self.lock.lock(); Self.requests[key, default: 0] += 1; Self.lock.unlock()
        let isAlias = url.host == "launchermeta.mojang.com"
        if !isAlias {
            let failure = request.value(forHTTPHeaderField: "X-Primary-Failure")
            if failure == "tls" || failure == "certificate" {
                client?.urlProtocol(self, didFailWithError: URLError(failure == "tls" ? .secureConnectionFailed : .serverCertificateUntrusted))
                return
            }
        }
        let body = isAlias ? Data(#"{"latest":{"release":"1.20.4","snapshot":"24w01a"},"versions":[]}"#.utf8) : Data()
        client?.urlProtocol(self, didReceive: HTTPURLResponse(url: url, statusCode: isAlias ? 200 : 503, httpVersion: "HTTP/1.1", headerFields: nil)!, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: body)
        client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() {}
}
