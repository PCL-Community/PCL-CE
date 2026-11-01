import Foundation

/// Retry a failed TLS handshake with TLS 1.2 for proxies that cannot negotiate
/// TLS 1.3. Trust evaluation, redirect policy and all other session settings stay
/// with the caller, including the delegate transport used for progress reporting.
public enum PCLHTTPS {
    public static func data(for request: URLRequest, session: URLSession = .shared) async throws -> (Data, URLResponse) {
        try validateHTTPS(request)
        do { return try await session.data(for: request) }
        catch let error as URLError where error.code == .secureConnectionFailed {
            try Task.checkCancellation()
            let fallback = tls12Session(copying: session)
            defer { fallback.finishTasksAndInvalidate() }
            return try await fallback.data(for: request)
        }
    }

    public static func download(for request: URLRequest, session: URLSession = .shared) async throws -> (URL, URLResponse) {
        try validateHTTPS(request)
        do { return try await downloadAttempt(request, session: session) }
        catch let error as URLError where error.code == .secureConnectionFailed {
            try Task.checkCancellation()
            let fallback = tls12Session(copying: session)
            defer { fallback.finishTasksAndInvalidate() }
            return try await downloadAttempt(request, session: fallback)
        }
    }

    private static func downloadAttempt(_ request: URLRequest, session: URLSession) async throws -> (URL, URLResponse) {
        guard let monitor = InstallProgressContext.current else { return try await session.download(for: request) }
        let transport = monitor.downloadTransport(copying: session)
        monitor.networkTransferStarted()
        defer { monitor.networkTransferFinished() }
        let cancellation = InstallDownloadCancellation()
        return try await withTaskCancellationHandler {
            try Task.checkCancellation()
            return try await withCheckedThrowingContinuation { continuation in
                transport.start(request, cancellation: cancellation, continuation: continuation)
            }
        } onCancel: { cancellation.cancel() }
    }

    static func tls12Session(copying original: URLSession) -> URLSession {
        let configuration = original.configuration
        configuration.tlsMaximumSupportedProtocolVersion = .TLSv12
        return URLSession(configuration: configuration, delegate: original.delegate, delegateQueue: original.delegateQueue)
    }

    private static func validateHTTPS(_ request: URLRequest) throws {
        guard request.url?.scheme?.lowercased() == "https" else { throw URLError(.unsupportedURL) }
    }
}

/// One delegate-based transport is reused for each original session within an
/// installation. Foundation's async download API does not deliver incremental
/// download callbacks. Copying its configuration preserves connection pooling,
/// and forwarding redirect/authentication keeps the caller's security policy.
final class InstallDownloadTelemetryDelegate: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    private weak var monitor: InstallTaskMonitor?
    private let original: URLSessionDelegate?
    private let sourceSession: URLSession?
    private var transportSession: URLSession?
    private let stateLock = NSLock()
    private var tasks: [Int: DownloadState] = [:]

    init(monitor: InstallTaskMonitor, forwarding original: URLSessionDelegate?) {
        self.monitor = monitor; self.original = original; self.sourceSession = nil
    }

    init(monitor: InstallTaskMonitor, copying originalSession: URLSession) {
        self.monitor = monitor; self.original = originalSession.delegate
        self.sourceSession = originalSession
        super.init()
        self.transportSession = URLSession(configuration: originalSession.configuration,
            delegate: self, delegateQueue: originalSession.delegateQueue)
    }

    func finishTasksAndInvalidate() { transportSession?.finishTasksAndInvalidate() }

    func start(_ request: URLRequest, cancellation: InstallDownloadCancellation,
               continuation: CheckedContinuation<(URL, URLResponse), Error>) {
        guard let session = transportSession else {
            continuation.resume(throwing: URLError(.unknown)); return
        }
        let task = session.downloadTask(with: request)
        stateLock.lock()
        tasks[task.taskIdentifier] = DownloadState(continuation: continuation)
        stateLock.unlock()
        cancellation.attach(task)
        task.resume()
    }

    override func responds(to selector: Selector!) -> Bool {
        super.responds(to: selector) || original?.responds(to: selector) == true
    }

    override func forwardingTarget(for selector: Selector!) -> Any? {
        if original?.responds(to: selector) == true { return original }
        return super.forwardingTarget(for: selector)
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask,
                    didWriteData bytesWritten: Int64, totalBytesWritten: Int64, totalBytesExpectedToWrite: Int64) {
        monitor?.recordDownloadedBytes(bytesWritten)
        (original as? URLSessionDownloadDelegate)?.urlSession?(session, downloadTask: downloadTask,
            didWriteData: bytesWritten, totalBytesWritten: totalBytesWritten, totalBytesExpectedToWrite: totalBytesExpectedToWrite)
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didFinishDownloadingTo location: URL) {
        // URLSession removes its temporary download immediately after this
        // delegate callback. Preserve bytes before resuming the async caller.
        let saved = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-transfer-\(UUID().uuidString).tmp")
        do {
            try FileManager.default.copyItem(at: location, to: saved)
            stateLock.lock()
            if var state = tasks[downloadTask.taskIdentifier] {
                state.file = saved; tasks[downloadTask.taskIdentifier] = state
                stateLock.unlock()
            } else {
                stateLock.unlock()
                try? FileManager.default.removeItem(at: saved)
            }
        } catch {
            stateLock.lock()
            if var state = tasks[downloadTask.taskIdentifier] {
                state.error = error; tasks[downloadTask.taskIdentifier] = state
            }
            stateLock.unlock()
        }
        (original as? URLSessionDownloadDelegate)?.urlSession(session, downloadTask: downloadTask, didFinishDownloadingTo: location)
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        stateLock.lock()
        let state = tasks.removeValue(forKey: task.taskIdentifier)
        stateLock.unlock()
        if let state {
            if let failure = error ?? state.error {
                if let file = state.file { try? FileManager.default.removeItem(at: file) }
                state.continuation.resume(throwing: failure)
            } else if let file = state.file, let response = task.response {
                state.continuation.resume(returning: (file, response))
            } else {
                if let file = state.file { try? FileManager.default.removeItem(at: file) }
                state.continuation.resume(throwing: URLError(.badServerResponse))
            }
        }
        (original as? URLSessionTaskDelegate)?.urlSession?(session, task: task, didCompleteWithError: error)
    }

    private struct DownloadState {
        let continuation: CheckedContinuation<(URL, URLResponse), Error>
        var file: URL?
        var error: Error?
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
                    newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) {
        let selector = #selector(URLSessionTaskDelegate.urlSession(_:task:willPerformHTTPRedirection:newRequest:completionHandler:))
        if let delegate = original as? URLSessionTaskDelegate, delegate.responds(to: selector) {
            delegate.urlSession?(session, task: task, willPerformHTTPRedirection: response, newRequest: request, completionHandler: completionHandler)
        } else { completionHandler(request) }
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didReceive challenge: URLAuthenticationChallenge,
                    completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void) {
        let taskSelector = #selector(URLSessionTaskDelegate.urlSession(_:task:didReceive:completionHandler:))
        let sessionSelector = #selector(URLSessionDelegate.urlSession(_:didReceive:completionHandler:))
        if let delegate = original as? URLSessionTaskDelegate, delegate.responds(to: taskSelector) {
            delegate.urlSession?(session, task: task, didReceive: challenge, completionHandler: completionHandler)
        } else if let original, original.responds(to: sessionSelector) {
            original.urlSession?(session, didReceive: challenge, completionHandler: completionHandler)
        } else { completionHandler(.performDefaultHandling, nil) }
    }
}

final class InstallDownloadCancellation: @unchecked Sendable {
    private let lock = NSLock()
    private var task: URLSessionTask?
    private var cancelled = false

    func attach(_ task: URLSessionTask) {
        lock.lock()
        self.task = task
        let shouldCancel = cancelled
        lock.unlock()
        if shouldCancel { task.cancel() }
    }

    func cancel() {
        lock.lock()
        cancelled = true
        let task = task
        lock.unlock()
        task?.cancel()
    }
}
