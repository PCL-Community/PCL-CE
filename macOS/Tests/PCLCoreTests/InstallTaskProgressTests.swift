import XCTest
@testable import PCLCore

final class InstallTaskProgressTests: XCTestCase {
    private func monitor(_ clock: ProgressClock = ProgressClock(), stages: [InstallStageProgress]? = nil,
                         recorder: ProgressRecorder = ProgressRecorder()) -> InstallTaskMonitor {
        InstallTaskMonitor(title: "CurseForge 整合包安装：测试", stages: stages ?? [
            .init(id: "extract", title: "解压整合包文件", weight: 2),
            .init(id: "download", title: "下载 Mod", weight: 8),
            .init(id: "final", title: "最终整理", weight: 0.1, isVisible: false)
        ], now: { clock.value }, automaticUpdates: false, progress: { recorder.append($0) })
    }

    func testWeightedParallelStagesWaitForHiddenCommit() {
        let monitor = monitor()
        monitor.begin("extract"); monitor.begin("download")
        monitor.update("extract", fraction: 0.59)
        monitor.complete("download")
        let snapshot = monitor.snapshot
        XCTAssertEqual(snapshot.totalFraction, (0.59 * 2 + 8) / 10.1, accuracy: 0.000001)
        XCTAssertEqual(snapshot.stages.filter { $0.state == .running }.map(\.id), ["extract"])
        XCTAssertEqual(snapshot.stages.filter(\.isVisible).count, 2)
        monitor.complete("extract")
        XCTAssertLessThan(monitor.snapshot.totalFraction, 1, "A completed download cannot publish an incomplete profile as installed")
        monitor.complete("final")
        XCTAssertEqual(monitor.snapshot.totalFraction, 1)
    }

    func testStageFractionCannotRegressOrReopenACompletedStage() {
        let monitor = monitor()
        monitor.update("extract", fraction: 0.7)
        monitor.update("extract", fraction: 0.2)
        XCTAssertEqual(monitor.snapshot.stages[0].fraction, 0.7)
        monitor.complete("extract")
        monitor.update("extract", fraction: 0.1)
        XCTAssertEqual(monitor.snapshot.stages[0].state, .completed)
        XCTAssertEqual(monitor.snapshot.stages[0].fraction, 1)
        monitor.update("download", fraction: 0.4)
        monitor.fail("download", message: "校验失败")
        XCTAssertEqual(monitor.snapshot.stages[1].state, .failed)
        XCTAssertEqual(monitor.snapshot.stages[1].fraction, 0.4)
        monitor.cancel()
        XCTAssertEqual(monitor.snapshot.stages[1].state, .failed)
        XCTAssertEqual(monitor.snapshot.stages[2].state, .cancelled)
        XCTAssertLessThan(monitor.snapshot.totalFraction, 1)
    }

    func testRemainingFilesAreSummedAcrossIndependentBranchesAndReuseHasNoSpeed() {
        let monitor = monitor()
        monitor.setRemainingFiles(3807, for: "assets")
        monitor.setRemainingFiles(413, for: "mods")
        XCTAssertEqual(monitor.snapshot.remainingFiles, 4220)
        monitor.setRemainingFiles(0, for: "assets")
        monitor.setRemainingFiles(9, for: "mods")
        monitor.update("download", fraction: 0.9)
        XCTAssertEqual(monitor.snapshot.remainingFiles, 9)
        XCTAssertEqual(monitor.snapshot.downloadedBytes, 0)
        XCTAssertEqual(monitor.snapshot.bytesPerSecond, 0, "A cached copy is not network traffic")
    }

    func testRecentTransferRateDecaysAndBecomesZeroAtLastNetworkCompletion() {
        let clock = ProgressClock()
        let monitor = monitor(clock)
        monitor.networkTransferStarted()
        clock.advance(1)
        monitor.recordDownloadedBytes(4096)
        XCTAssertEqual(monitor.snapshot.bytesPerSecond, 4096, accuracy: 0.001)
        clock.advance(1)
        XCTAssertEqual(monitor.snapshot.bytesPerSecond, 2048, accuracy: 0.001)
        clock.advance(1.01)
        XCTAssertEqual(monitor.snapshot.bytesPerSecond, 0)
        monitor.recordDownloadedBytes(1024)
        XCTAssertEqual(monitor.snapshot.downloadedBytes, 5120)
        XCTAssertGreaterThan(monitor.snapshot.bytesPerSecond, 0)
        monitor.networkTransferFinished()
        XCTAssertEqual(monitor.snapshot.bytesPerSecond, 0)
    }

    func testHighFrequencyUpdatesAreThrottledAndTerminalStateIsImmediate() {
        let clock = ProgressClock(), recorder = ProgressRecorder()
        let monitor = monitor(clock, recorder: recorder)
        monitor.begin("download")
        for count in 0..<5000 {
            monitor.update("download", fraction: Double(count) / 5000)
            monitor.setRemainingFiles(5000 - count, for: "mods")
        }
        XCTAssertLessThan(recorder.values.count, 5)
        clock.advance(0.13)
        monitor.update("download", fraction: 1)
        monitor.complete("download")
        XCTAssertEqual(recorder.values.last?.snapshot?.stages[1].state, .completed)
        XCTAssertLessThan(recorder.values.last!.fraction, 1)
    }

    func testCallbacksRunOutsideStateLockAndKeepTaskLocalStageContext() async {
        let entered = expectation(description: "extract callback entered")
        let otherBranch = expectation(description: "second branch progresses while extract callback waits")
        let release = DispatchSemaphore(value: 0)
        let contexts = ProgressContextRecorder()
        let monitor = InstallTaskMonitor(title: "parallel", stages: [
            .init(id: "extract", title: "extract"), .init(id: "base", title: "base")
        ], now: { 100 }, automaticUpdates: false) { value in
            if InstallProgressContext.stageID == "extract" {
                contexts.append("extract")
                entered.fulfill()
                _ = release.wait(timeout: .now() + 5)
            } else if InstallProgressContext.stageID == "base" {
                contexts.append("base")
                otherBranch.fulfill()
            }
        }
        DispatchQueue.global().async {
            InstallProgressContext.$stageID.withValue("extract") { monitor.begin("extract") }
        }
        await fulfillment(of: [entered], timeout: 2)
        DispatchQueue.global().async {
            InstallProgressContext.$stageID.withValue("base") { monitor.begin("base") }
        }
        await fulfillment(of: [otherBranch], timeout: 2)
        release.signal()
        XCTAssertEqual(Set(contexts.values), ["extract", "base"])
    }

    func testChildTasksInheritTheMonitorAndAggregateActualBytes() async {
        let monitor = monitor()
        monitor.networkTransferStarted()
        await InstallProgressContext.$current.withValue(monitor) {
            await withTaskGroup(of: Void.self) { group in
                for _ in 0..<100 {
                    group.addTask { InstallProgressContext.current?.recordDownloadedBytes(128) }
                }
            }
        }
        XCTAssertEqual(monitor.snapshot.downloadedBytes, 12_800)
        monitor.networkTransferFinished()
        XCTAssertEqual(monitor.snapshot.bytesPerSecond, 0)
    }

    func testByteDelegateForwardsOriginalRedirectAndAuthenticationDecisions() {
        let recorder = ProgressRecorder(), monitor = monitor(recorder: recorder)
        let policy = TelemetrySecurityPolicy()
        let session = URLSession(configuration: .ephemeral, delegate: policy, delegateQueue: nil)
        defer { session.invalidateAndCancel() }
        let delegate = InstallDownloadTelemetryDelegate(monitor: monitor, forwarding: policy)
        let task = session.downloadTask(with: URL(string: "https://fixture.invalid/original")!)
        let redirect = URLRequest(url: URL(string: "https://untrusted.invalid/file")!)
        let response = HTTPURLResponse(url: task.originalRequest!.url!, statusCode: 302, httpVersion: nil, headerFields: nil)!
        var redirected = true
        delegate.urlSession(session, task: task, willPerformHTTPRedirection: response, newRequest: redirect) { request in redirected = request != nil }
        XCTAssertFalse(redirected, "A telemetry delegate must not weaken the original same-host redirect policy")
        XCTAssertEqual(policy.redirectDecisions, 1)
        let space = URLProtectionSpace(host: "fixture.invalid", port: 443, protocol: "https", realm: nil, authenticationMethod: NSURLAuthenticationMethodServerTrust)
        let challenge = URLAuthenticationChallenge(protectionSpace: space, proposedCredential: nil, previousFailureCount: 0,
            failureResponse: nil, error: nil, sender: TelemetryChallengeSender())
        var disposition = URLSession.AuthChallengeDisposition.performDefaultHandling
        delegate.urlSession(session, task: task, didReceive: challenge) { choice, _ in disposition = choice }
        XCTAssertEqual(disposition, .cancelAuthenticationChallenge)
        XCTAssertEqual(policy.authDecisions, 1)
        monitor.networkTransferStarted()
        delegate.urlSession(session, downloadTask: task, didWriteData: 456, totalBytesWritten: 456, totalBytesExpectedToWrite: 1000)
        XCTAssertEqual(monitor.snapshot.downloadedBytes, 456)
        XCTAssertEqual(policy.forwardedBytes, 456)
        monitor.networkTransferFinished()
    }

    func testAsyncDownloadMeasuresActualTransferAndKeepsTheDownloadedFile() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [TelemetryDownloadFixtureProtocol.self]
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }
        let monitor = monitor()
        let request = URLRequest(url: URL(string: "https://telemetry-download.invalid/file")!)
        let (file, response) = try await InstallProgressContext.$current.withValue(monitor) {
            try await PCLHTTPS.download(for: request, session: session)
        }
        defer { try? FileManager.default.removeItem(at: file) }
        XCTAssertEqual((response as? HTTPURLResponse)?.statusCode, 200)
        XCTAssertEqual(try Data(contentsOf: file), TelemetryDownloadFixtureProtocol.body)
        XCTAssertEqual(monitor.snapshot.downloadedBytes, Int64(TelemetryDownloadFixtureProtocol.body.count))
        XCTAssertEqual(monitor.snapshot.bytesPerSecond, 0, "The connection is finished, even while the file awaits verification")
    }

    func testMonitoredDownloadCancellationStopsTheTaskAndClearsSpeed() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [TelemetryDownloadFixtureProtocol.self]
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }
        let monitor = monitor()
        let started = expectation(description: "download started")
        let path = "/cancel/\(UUID().uuidString)"
        TelemetryDownloadFixtureProtocol.state.observe(path) { started.fulfill() }
        let task = Task {
            try await InstallProgressContext.$current.withValue(monitor) {
                try await PCLHTTPS.download(for: URLRequest(url: URL(string: "https://telemetry-download.invalid\(path)")!), session: session)
            }
        }
        await fulfillment(of: [started], timeout: 2)
        task.cancel()
        do {
            let (file, _) = try await task.value
            try? FileManager.default.removeItem(at: file)
            XCTFail("Cancelled transfer should not succeed")
        } catch let error as URLError {
            XCTAssertEqual(error.code, .cancelled)
        } catch is CancellationError {
            // Cancellation may happen between the context entry and task setup.
        }
        XCTAssertEqual(monitor.snapshot.bytesPerSecond, 0)
        XCTAssertEqual(TelemetryDownloadFixtureProtocol.state.count(path), 1)
    }

    func testMonitoredDownloadTLSCompatibilityKeepsAccountingAndConfiguration() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [TelemetryDownloadFixtureProtocol.self]
        configuration.httpAdditionalHeaders = ["X-Original": "preserved"]
        let originalMaximum = configuration.tlsMaximumSupportedProtocolVersion
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }
        let monitor = monitor()
        let path = "/tls-once/\(UUID().uuidString)"
        let (file, _) = try await InstallProgressContext.$current.withValue(monitor) {
            try await PCLHTTPS.download(for: URLRequest(url: URL(string: "https://telemetry-download.invalid\(path)")!), session: session)
        }
        defer { try? FileManager.default.removeItem(at: file) }
        XCTAssertEqual(try Data(contentsOf: file), TelemetryDownloadFixtureProtocol.body)
        XCTAssertEqual(TelemetryDownloadFixtureProtocol.state.count(path), 2)
        XCTAssertEqual(monitor.snapshot.downloadedBytes, Int64(TelemetryDownloadFixtureProtocol.body.count))
        XCTAssertEqual(monitor.snapshot.bytesPerSecond, 0)
        XCTAssertEqual(session.configuration.tlsMaximumSupportedProtocolVersion, originalMaximum)
        XCTAssertEqual(session.configuration.httpAdditionalHeaders?["X-Original"] as? String, "preserved")
    }
}

private final class ProgressClock: @unchecked Sendable {
    private let lock = NSLock()
    private var time: TimeInterval = 100
    var value: TimeInterval { lock.lock(); defer { lock.unlock() }; return time }
    func advance(_ delta: TimeInterval) { lock.lock(); time += delta; lock.unlock() }
}

private final class ProgressRecorder: @unchecked Sendable {
    private let lock = NSLock()
    private var recorded: [InstallProgress] = []
    var values: [InstallProgress] { lock.lock(); defer { lock.unlock() }; return recorded }
    func append(_ value: InstallProgress) { lock.lock(); recorded.append(value); lock.unlock() }
}

private final class ProgressContextRecorder: @unchecked Sendable {
    private let lock = NSLock()
    private var recorded: [String] = []
    var values: [String] { lock.lock(); defer { lock.unlock() }; return recorded }
    func append(_ value: String) { lock.lock(); recorded.append(value); lock.unlock() }
}

private final class TelemetrySecurityPolicy: NSObject, URLSessionDownloadDelegate {
    var redirectDecisions = 0, authDecisions = 0
    var forwardedBytes: Int64 = 0
    func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
                    newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) {
        redirectDecisions += 1; completionHandler(nil)
    }
    func urlSession(_ session: URLSession, didReceive challenge: URLAuthenticationChallenge,
                    completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void) {
        authDecisions += 1; completionHandler(.cancelAuthenticationChallenge, nil)
    }
    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask,
                    didWriteData bytesWritten: Int64, totalBytesWritten: Int64, totalBytesExpectedToWrite: Int64) { forwardedBytes += bytesWritten }
    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didFinishDownloadingTo location: URL) {}
}

private final class TelemetryChallengeSender: NSObject, URLAuthenticationChallengeSender {
    func use(_ credential: URLCredential, for challenge: URLAuthenticationChallenge) {}
    func continueWithoutCredential(for challenge: URLAuthenticationChallenge) {}
    func cancel(_ challenge: URLAuthenticationChallenge) {}
}

private final class TelemetryDownloadFixtureProtocol: URLProtocol {
    static let body = Data(repeating: 0x73, count: 512 * 1024)
    static let state = TelemetryFixtureState()
    override class func canInit(with request: URLRequest) -> Bool { request.url?.host == "telemetry-download.invalid" }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }
    override func startLoading() {
        let path = request.url!.path
        let attempt = Self.state.started(path)
        if path.hasPrefix("/tls-once/"), attempt == 1 {
            client?.urlProtocol(self, didFailWithError: URLError(.secureConnectionFailed))
            return
        }
        let response = HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: "HTTP/1.1",
            headerFields: ["Content-Length": String(Self.body.count)])!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        if path.hasPrefix("/cancel/") { return }
        for offset in stride(from: 0, to: Self.body.count, by: 64 * 1024) {
            client?.urlProtocol(self, didLoad: Self.body.subdata(in: offset..<min(offset + 64 * 1024, Self.body.count)))
        }
        client?.urlProtocolDidFinishLoading(self)
    }
    override func stopLoading() {}
}

private final class TelemetryFixtureState: @unchecked Sendable {
    private let lock = NSLock()
    private var counts: [String: Int] = [:]
    private var observers: [String: @Sendable () -> Void] = [:]
    func observe(_ path: String, callback: @escaping @Sendable () -> Void) {
        lock.lock(); observers[path] = callback; lock.unlock()
    }
    func started(_ path: String) -> Int {
        lock.lock()
        counts[path, default: 0] += 1
        let count = counts[path]!
        let callback = observers.removeValue(forKey: path)
        lock.unlock()
        callback?()
        return count
    }
    func count(_ path: String) -> Int { lock.lock(); defer { lock.unlock() }; return counts[path] ?? 0 }
}
