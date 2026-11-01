import XCTest
@testable import PCLCore

final class NetworkTests: XCTestCase {
    private func session(delegate: URLSessionDelegate? = nil, queue: OperationQueue? = nil) -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [NetworkFixtureProtocol.self]
        let session = URLSession(configuration: configuration, delegate: delegate, delegateQueue: queue)
        addTeardownBlock { session.invalidateAndCancel() }
        return session
    }

    private func request(_ mode: String) -> URLRequest {
        URLRequest(url: URL(string: "https://network-fixture.invalid/\(mode)/\(UUID().uuidString)")!)
    }

    func testSuccessfulRequestUsesOriginalSessionWithoutRetry() async throws {
        let original = session()
        let request = request("success")
        let (data, response) = try await PCLHTTPS.data(for: request, session: original)
        XCTAssertEqual(data, NetworkFixtureProtocol.body)
        XCTAssertEqual((response as? HTTPURLResponse)?.statusCode, 200)
        XCTAssertEqual(NetworkFixtureProtocol.requests(for: request).count, 1)
        XCTAssertEqual(original.configuration.tlsMaximumSupportedProtocolVersion, .TLSv13)
    }

    func testTLSFailureRetriesSameRequestOnce() async throws {
        let original = session()
        var request = request("tls-once")
        request.setValue("fixture-key", forHTTPHeaderField: "x-api-key")
        let (data, _) = try await PCLHTTPS.data(for: request, session: original)
        XCTAssertEqual(data, NetworkFixtureProtocol.body)
        let attempts = NetworkFixtureProtocol.requests(for: request)
        XCTAssertEqual(attempts.count, 2)
        XCTAssertTrue(attempts.allSatisfy { $0.url == request.url })
        XCTAssertTrue(attempts.allSatisfy { $0.value(forHTTPHeaderField: "x-api-key") == "fixture-key" })
        XCTAssertEqual(original.configuration.tlsMaximumSupportedProtocolVersion, .TLSv13)
    }

    func testRepeatedTLSFailureStopsAfterSingleCompatibilityRetry() async throws {
        let request = request("tls-always")
        do {
            _ = try await PCLHTTPS.data(for: request, session: session())
            XCTFail("Both TLS handshakes should fail")
        } catch let error as URLError { XCTAssertEqual(error.code, .secureConnectionFailed) }
        XCTAssertEqual(NetworkFixtureProtocol.requests(for: request).count, 2)
    }

    func testOtherErrorsIncludingCertificateRejectionAreNotRetried() async throws {
        for code: URLError.Code in [.timedOut, .networkConnectionLost, .serverCertificateUntrusted, .cancelled] {
            let request = request("error-\(code.rawValue)")
            do {
                _ = try await PCLHTTPS.data(for: request, session: session())
                XCTFail("Expected \(code)")
            } catch let error as URLError { XCTAssertEqual(error.code, code) }
            XCTAssertEqual(NetworkFixtureProtocol.requests(for: request).count, 1)
        }
    }

    func testDownloadRetriesTLSFailureAndReturnsUsableTemporaryFile() async throws {
        let request = request("tls-once")
        let (file, response) = try await PCLHTTPS.download(for: request, session: session())
        defer { try? FileManager.default.removeItem(at: file) }
        XCTAssertEqual(try Data(contentsOf: file), NetworkFixtureProtocol.body)
        XCTAssertEqual((response as? HTTPURLResponse)?.statusCode, 200)
        XCTAssertEqual(NetworkFixtureProtocol.requests(for: request).count, 2)
    }

    func testMetadataRetriesTransientConnectionLoss() async throws {
        let request = request("transient-once")
        let data = try await MinecraftInstaller.fetchData(url: try XCTUnwrap(request.url), session: session())
        XCTAssertEqual(data, NetworkFixtureProtocol.body)
        XCTAssertEqual(NetworkFixtureProtocol.requests(for: request).count, 2)
    }

    func testCompatibilitySessionCopiesSettingsAndKeepsRedirectDelegate() throws {
        let policy = NetworkRedirectPolicy()
        let queue = OperationQueue()
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [NetworkFixtureProtocol.self]
        configuration.connectionProxyDictionary = ["HTTPSEnable": 1, "HTTPSProxy": "127.0.0.1", "HTTPSPort": 7890]
        configuration.httpAdditionalHeaders = ["X-Fixture": "preserved"]
        configuration.timeoutIntervalForRequest = 37
        configuration.timeoutIntervalForResource = 93
        configuration.requestCachePolicy = .reloadIgnoringLocalCacheData
        configuration.tlsMinimumSupportedProtocolVersion = .TLSv12
        configuration.tlsMaximumSupportedProtocolVersion = .TLSv13
        let original = URLSession(configuration: configuration, delegate: policy, delegateQueue: queue)
        defer { original.invalidateAndCancel() }
        let fallback = PCLHTTPS.tls12Session(copying: original)
        defer { fallback.invalidateAndCancel() }
        let copied = fallback.configuration
        XCTAssertTrue(fallback.delegate === policy, "Credential-bearing requests must keep the caller's redirect/trust delegate")
        XCTAssertTrue(fallback.delegateQueue === queue)
        XCTAssertEqual(copied.tlsMaximumSupportedProtocolVersion, .TLSv12)
        XCTAssertEqual(copied.tlsMinimumSupportedProtocolVersion, .TLSv12)
        XCTAssertEqual(original.configuration.tlsMaximumSupportedProtocolVersion, .TLSv13)
        XCTAssertEqual(copied.connectionProxyDictionary as NSDictionary?, original.configuration.connectionProxyDictionary as NSDictionary?)
        XCTAssertEqual(copied.httpAdditionalHeaders as NSDictionary?, original.configuration.httpAdditionalHeaders as NSDictionary?)
        XCTAssertEqual(copied.timeoutIntervalForRequest, 37)
        XCTAssertEqual(copied.timeoutIntervalForResource, 93)
        XCTAssertEqual(copied.requestCachePolicy, .reloadIgnoringLocalCacheData)
        XCTAssertTrue(copied.httpCookieStorage === original.configuration.httpCookieStorage)
        XCTAssertTrue(copied.urlCredentialStorage === original.configuration.urlCredentialStorage)
        XCTAssertTrue(copied.urlCache === original.configuration.urlCache)
        XCTAssertEqual(copied.protocolClasses?.count, 1)
        XCTAssertTrue(copied.protocolClasses?.first === NetworkFixtureProtocol.self)
    }

    func testPlainHTTPIsRejectedBeforeAnyRequest() async throws {
        let request = URLRequest(url: URL(string: "http://network-fixture.invalid/success/\(UUID().uuidString)")!)
        do {
            _ = try await PCLHTTPS.data(for: request, session: session())
            XCTFail("Only HTTPS may use this transport")
        } catch let error as URLError { XCTAssertEqual(error.code, .unsupportedURL) }
        XCTAssertTrue(NetworkFixtureProtocol.requests(for: request).isEmpty)
    }
}

private final class NetworkRedirectPolicy: NSObject, URLSessionTaskDelegate, @unchecked Sendable {
    func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
                    newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) {
        completionHandler(request.url?.scheme == "https" && request.url?.host == "network-fixture.invalid" ? request : nil)
    }
}

private final class NetworkFixtureProtocol: URLProtocol {
    static let body = Data("network fixture payload".utf8)
    private static let lock = NSLock()
    private static var recorded: [URL: [URLRequest]] = [:]

    static func requests(for request: URLRequest) -> [URLRequest] {
        lock.lock(); defer { lock.unlock() }
        return request.url.flatMap { recorded[$0] } ?? []
    }

    override class func canInit(with request: URLRequest) -> Bool { request.url?.host == "network-fixture.invalid" }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        guard let url = request.url else { return }
        Self.lock.lock()
        Self.recorded[url, default: []].append(request)
        let attempt = Self.recorded[url]!.count
        Self.lock.unlock()
        let mode = url.pathComponents.dropFirst().first ?? ""
        if mode == "tls-always" || (mode == "tls-once" && attempt == 1) {
            client?.urlProtocol(self, didFailWithError: URLError(.secureConnectionFailed)); return
        }
        if mode == "transient-once" && attempt == 1 {
            client?.urlProtocol(self, didFailWithError: URLError(.networkConnectionLost)); return
        }
        if mode.hasPrefix("error-"), let code = Int(mode.dropFirst("error-".count)) {
            client?.urlProtocol(self, didFailWithError: URLError(URLError.Code(rawValue: code))); return
        }
        client?.urlProtocol(self, didReceive: HTTPURLResponse(url: url, statusCode: 200, httpVersion: "HTTP/1.1", headerFields: nil)!, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Self.body)
        client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() {}
}
