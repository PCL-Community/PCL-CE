import Foundation

public enum InstallStageState: String, Sendable, Equatable {
    case waiting, running, completed, failed, cancelled
}

public struct InstallStageProgress: Identifiable, Sendable, Equatable {
    public let id: String
    public let title: String
    public let state: InstallStageState
    public let fraction: Double
    public let weight: Double
    public let isVisible: Bool

    public init(id: String, title: String, state: InstallStageState = .waiting,
                fraction: Double = 0, weight: Double = 1, isVisible: Bool = true) {
        self.id = id; self.title = title; self.state = state
        self.fraction = state == .completed ? 1 : Self.bounded(fraction)
        self.weight = weight.isFinite ? max(0, weight) : 0
        self.isVisible = isVisible
    }

    fileprivate static func bounded(_ fraction: Double) -> Double {
        fraction.isFinite ? max(0, min(1, fraction)) : 0
    }
}

public struct InstallTaskSnapshot: Sendable, Equatable {
    public let title: String
    public let stages: [InstallStageProgress]
    public let totalFraction: Double
    public let bytesPerSecond: Double
    public let remainingFiles: Int
    public let downloadedBytes: Int64
    public let revision: Int64

    public init(title: String, stages: [InstallStageProgress], totalFraction: Double,
                bytesPerSecond: Double = 0, remainingFiles: Int = 0,
                downloadedBytes: Int64 = 0, revision: Int64 = 0) {
        self.title = title; self.stages = stages
        self.totalFraction = InstallStageProgress.bounded(totalFraction)
        self.bytesPerSecond = bytesPerSecond.isFinite ? max(0, bytesPerSecond) : 0
        self.remainingFiles = max(0, remainingFiles)
        self.downloadedBytes = max(0, downloadedBytes); self.revision = revision
    }
}

/// Child task groups and actor calls inherit this context. Detached CPU work
/// explicitly re-enters it if it needs to report network/task statistics.
public enum InstallProgressContext {
    @TaskLocal public static var current: InstallTaskMonitor?
    @TaskLocal public static var stageID: String?
}

/// The operation owns a monitor; its callbacks contain immutable snapshots.
/// Counts are supplied by each branch, while byte samples only come from actual
/// URLSession transfer callbacks. Local copies never contribute to throughput.
public final class InstallTaskMonitor: @unchecked Sendable {
    private let lock = NSLock()
    private let title: String
    private var stages: [InstallStageProgress]
    private var pending: [String: Int] = [:]
    private var downloadedBytes: Int64 = 0
    private var activeTransfers = 0
    private var samples: [(time: TimeInterval, bytes: Int64)] = []
    private var revision: Int64 = 0
    private var lastPublication = -Double.infinity
    private var message = ""
    private var dirty = false
    private var finished = false
    private var timer: DispatchSourceTimer?
    private var transports: [ObjectIdentifier: InstallDownloadTelemetryDelegate] = [:]
    private let started: TimeInterval
    private let now: @Sendable () -> TimeInterval
    private let progress: @Sendable (InstallProgress) -> Void
    private let publicationInterval: TimeInterval = 0.12
    private let speedWindow: TimeInterval = 2

    public convenience init(title: String, stages: [InstallStageProgress],
                            progress: @escaping @Sendable (InstallProgress) -> Void) {
        self.init(title: title, stages: stages, now: { ProcessInfo.processInfo.systemUptime },
                  automaticUpdates: true, progress: progress)
    }

    /// Deterministic clock/timer injection is internal so production speed always
    /// uses a monotonic clock rather than wall-clock adjustments.
    init(title: String, stages: [InstallStageProgress], now: @escaping @Sendable () -> TimeInterval,
         automaticUpdates: Bool, progress: @escaping @Sendable (InstallProgress) -> Void) {
        precondition(Set(stages.map(\.id)).count == stages.count, "Installation stage IDs must be unique")
        self.title = title; self.stages = stages; self.now = now
        self.started = now(); self.progress = progress
        if automaticUpdates {
            let timer = DispatchSource.makeTimerSource(queue: DispatchQueue(label: "app.pcl.install-progress", qos: .utility))
            timer.schedule(deadline: .now() + 0.2, repeating: 0.2)
            timer.setEventHandler { [weak self] in self?.tick() }
            self.timer = timer
            timer.resume()
        }
    }

    deinit {
        timer?.cancel()
        transports.values.forEach { $0.finishTasksAndInvalidate() }
    }

    func downloadTransport(copying session: URLSession) -> InstallDownloadTelemetryDelegate {
        lock.lock(); defer { lock.unlock() }
        let key = ObjectIdentifier(session)
        if let existing = transports[key] { return existing }
        let transport = InstallDownloadTelemetryDelegate(monitor: self, copying: session)
        transports[key] = transport
        return transport
    }

    public var snapshot: InstallTaskSnapshot {
        lock.lock(); defer { lock.unlock() }
        return makeSnapshot(at: now())
    }

    public func begin(_ id: String, message: String? = nil) {
        mutateStage(id, state: .running, fraction: nil, message: message, force: true)
    }

    public func update(_ id: String, fraction: Double, message: String? = nil) {
        mutateStage(id, state: .running, fraction: fraction, message: message, force: false)
    }

    public func complete(_ id: String, message: String? = nil) {
        mutateStage(id, state: .completed, fraction: 1, message: message, force: true)
    }

    public func fail(_ id: String, message: String? = nil) {
        mutateStage(id, state: .failed, fraction: nil, message: message, force: true)
    }

    public func cancel() {
        change(force: true) {
            guard !finished else { return false }
            stages = stages.map { stage in
                guard stage.state == .waiting || stage.state == .running else { return stage }
                return InstallStageProgress(id: stage.id, title: stage.title, state: .cancelled,
                    fraction: stage.fraction, weight: stage.weight, isVisible: stage.isVisible)
            }
            pending.removeAll(); activeTransfers = 0; finished = true
            timer?.cancel(); timer = nil
            return true
        }
    }

    /// A branch replaces its own pending count. Different branches use distinct
    /// group IDs; they cannot reset one another's outstanding-file totals.
    public func setRemainingFiles(_ count: Int, for groupID: String) {
        change(force: false) {
            guard !finished else { return false }
            pending[groupID] = max(0, count)
            return true
        }
    }

    public func networkTransferStarted() {
        change(force: false) {
            guard !finished else { return false }
            activeTransfers += 1
            return true
        }
    }

    public func networkTransferFinished() {
        // Force this event: the last transfer ending must immediately show zero
        // rather than a stale network speed during validation/local copying.
        change(force: false, forceWhenNetworkIdle: true) {
            guard !finished else { return false }
            activeTransfers = max(0, activeTransfers - 1)
            return true
        }
    }

    public func recordDownloadedBytes(_ count: Int64) {
        guard count > 0 else { return }
        change(force: false) {
            guard !finished else { return false }
            let current = now()
            downloadedBytes = count > Int64.max - downloadedBytes ? Int64.max : downloadedBytes + count
            samples.append((current, count))
            discardOldSamples(at: current)
            return true
        }
    }

    private func mutateStage(_ id: String, state: InstallStageState, fraction: Double?, message: String?, force: Bool) {
        change(force: force) {
            guard !finished, let index = stages.firstIndex(where: { $0.id == id }) else { return false }
            let previous = stages[index]
            // Completion/cancellation is terminal. A delayed download callback
            // cannot turn a blue checkmark back into a running percentage.
            guard previous.state != .completed && previous.state != .failed && previous.state != .cancelled else { return false }
            stages[index] = InstallStageProgress(id: previous.id, title: previous.title, state: state,
                fraction: max(previous.fraction, fraction.map(InstallStageProgress.bounded) ?? previous.fraction),
                weight: previous.weight, isVisible: previous.isVisible)
            if let message { self.message = message }
            if stages.allSatisfy({ $0.state == .completed || $0.state == .failed || $0.state == .cancelled }) {
                finished = true; pending.removeAll(); activeTransfers = 0
                timer?.cancel(); timer = nil
            }
            return true
        }
    }

    /// The callback is deliberately synchronous but outside the state lock. Its
    /// TaskLocal stage context is preserved, and a blocked branch cannot prevent
    /// a second branch from advancing. Consumers discard older revisions if
    /// concurrent callbacks arrive in the opposite order.
    private func change(force: Bool, forceWhenNetworkIdle: Bool = false, _ body: () -> Bool) {
        let publication: InstallProgress?
        lock.lock()
        if body() { publication = makePublication(force: force || (forceWhenNetworkIdle && activeTransfers == 0)) }
        else { publication = nil }
        lock.unlock()
        if let publication { progress(publication) }
    }

    private func discardOldSamples(at current: TimeInterval) {
        samples.removeAll { $0.time <= current - speedWindow }
    }

    private func makeSnapshot(at current: TimeInterval) -> InstallTaskSnapshot {
        let weights = stages.reduce(0) { $0 + $1.weight }
        let fraction = weights > 0 ? stages.reduce(0) { $0 + $1.fraction * $1.weight } / weights : 0
        let bytes = samples.filter { $0.time > current - speedWindow }.reduce(Int64(0)) { value, sample in
            sample.bytes > Int64.max - value ? Int64.max : value + sample.bytes
        }
        let duration = max(0.2, min(speedWindow, current - started))
        let speed = activeTransfers > 0 && !finished ? Double(bytes) / duration : 0
        let remaining = pending.values.reduce(0) { value, count in count > Int.max - value ? Int.max : value + count }
        return InstallTaskSnapshot(title: title, stages: stages, totalFraction: fraction,
            bytesPerSecond: speed, remainingFiles: remaining, downloadedBytes: downloadedBytes, revision: revision)
    }

    private func makePublication(force: Bool) -> InstallProgress? {
        let current = now()
        dirty = true
        guard force || current - lastPublication >= publicationInterval else { return nil }
        lastPublication = current; dirty = false; revision += 1
        let snapshot = makeSnapshot(at: current)
        return InstallProgress(message: message, completed: Int(snapshot.totalFraction * 1_000_000),
            total: 1_000_000, snapshot: snapshot)
    }

    private func tick() {
        change(force: true) {
            guard !finished else { return false }
            discardOldSamples(at: now())
            // Publish while a transfer is active so a stalled connection's speed
            // can decay to zero; otherwise flush only throttled stage/count data.
            return dirty || activeTransfers > 0
        }
    }
}
