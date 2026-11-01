import AppKit
import Combine
import Foundation
import PCLCore

enum LauncherPage: String, CaseIterable, Identifiable {
    case launch = "开始游戏", library = "版本管理", downloads = "下载游戏", versionSettings = "版本设置", activity = "任务与日志", settings = "设置", about = "关于"
    var id: String { rawValue }
    var symbol: String {
        switch self {
        case .launch: return "play.square.stack"
        case .library: return "square.stack.3d.up"
        case .downloads: return "arrow.down.circle"
        case .versionSettings: return "wrench.and.screwdriver"
        case .activity: return "text.alignleft"
        case .settings: return "slider.horizontal.3"
        case .about: return "info.circle"
        }
    }
    var subtitle: String {
        switch self {
        case .launch: return "新的冒险，从这里开始。"
        case .library: return "管理这台 Mac 上的 Minecraft 版本。"
        case .downloads: return "从 Mojang 官方源获取原版游戏。"
        case .versionSettings: return "管理所选版本的显示与独立启动设置。"
        case .activity: return "下载进度与游戏输出，尽在此处。"
        case .settings: return "让启动器适合你的使用习惯。"
        case .about: return "一份面向 macOS 的原生重构。"
        }
    }
}

enum VersionSettingsTab: String, CaseIterable {
    case overview = "概览", settings = "设置", modify = "修改", mods = "Mod 管理", export = "导出"
}

struct LauncherPreferences: Codable {
    var gamePath: String = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        .appendingPathComponent("PCL-SwiftUI/minecraft").path
    var playerName = ""
    var javaPath = ""
    var memoryMB = 4096
    var selectedVersion = ""
    var showSnapshots = false
    var extraJVMArguments = ""
}

@MainActor final class LauncherStore: ObservableObject {
    @Published var page: LauncherPage = .launch
    @Published var preferences: LauncherPreferences { didSet { savePreferences() } }
    @Published var installedVersions: [InstalledVersion] = []
    @Published var remoteVersions: [RemoteVersion] = []
    @Published var javaRuntimes: [JavaRuntime] = []
    @Published var manifestLoading = false
    @Published var scanning = false
    @Published var installProgress: InstallProgress?
    @Published var installingID: String?
    @Published var isGameRunning = false
    @Published var isLaunching = false
    @Published var errorMessage: String?
    @Published var logs: [String] = []
    @Published var statusMessage = "准备就绪"
    @Published var search = ""
    @Published var showLaunchConsent = false
    @Published var currentRemoteSelection: RemoteVersion?
    @Published var installNameDraft = ""
    @Published var showHiddenVersions = false
    @Published var versionSettingsTargetID: String?
    @Published var versionSettingsTab: VersionSettingsTab = .overview
    let versionModState = VersionModState()
    let versionExportState = VersionExportState()
    let versionModifyState = VersionModifyState()
    private var versionStateSubscriptions: Set<AnyCancellable> = []
    @Published var versionSettingsDraft = VersionProfileSettings()
    @Published var profileSaveError: String?
    @Published var versionWindowInputError: String?
    @Published var preparingInstall = false
    @Published var completedInstallID: String?
    @Published var moddedState = ModdedInstallState()
    @Published var communityState = CommunityBrowserState()
    @Published var hasRenderedGame = false
    var versionProfileCache: [String: VersionProfileSettings] = [:]
    var originalVersionIDs: [String: String] = [:]
    var hydratingVersionSettings = false
    var versionSettingsReturnPage: LauncherPage = .launch
    @Published private(set) var offlineNameHistory: [String] = []
    @Published var offlineSkinModel: OfflineSkinModel = .automatic {
        didSet { defaults.set(offlineSkinModel.rawValue, forKey: Self.offlineSkinKey) }
    }

    let installer = MinecraftInstaller()
    lazy var modLoaderInstaller = ModLoaderInstaller(vanillaInstaller: installer)
    lazy var modpackImporter = ModpackImporter(installer: installer, loaderInstaller: modLoaderInstaller)
    let communityClient = CommunityResourceClient()
    let fabricAPIComponent = FabricAPIComponent()
    let extraComponentsInstaller = ExtraComponentsInstaller()
    lazy var communityInstaller = CommunityResourceInstaller(client: communityClient)
    private let launcher = MinecraftLauncher()
    var installTask: Task<Void, Never>?
    var installOperation: UUID?
    private let defaults = UserDefaults.standard
    private static let preferencesKey = "PCLSwiftUI.preferences.v1"
    private static let offlineHistoryKey = "PCLSwiftUI.offlineNameHistory.v1"
    private static let offlineSkinKey = "PCLSwiftUI.offlineSkinModel.v1"
    private var didRestoreOfflineLogin = false
    private var requestedGameStop = false

    init() {
        if let data = UserDefaults.standard.data(forKey: Self.preferencesKey),
           let saved = try? JSONDecoder().decode(LauncherPreferences.self, from: data) {
            preferences = saved
        } else { preferences = LauncherPreferences() }
        offlineNameHistory = defaults.stringArray(forKey: Self.offlineHistoryKey) ?? []
        offlineSkinModel = OfflineSkinModel(rawValue: defaults.string(forKey: Self.offlineSkinKey) ?? "") ?? .automatic
        if let mostRecent = offlineNameHistory.first { preferences.playerName = mostRecent }
        for publisher in [versionModState.objectWillChange, versionExportState.objectWillChange, versionModifyState.objectWillChange] {
            publisher.sink { [weak self] _ in self?.objectWillChange.send() }.store(in: &versionStateSubscriptions)
        }
    }

    var gameDirectory: URL { URL(fileURLWithPath: preferences.gamePath, isDirectory: true) }
    var selectedVersion: InstalledVersion? { installedVersions.first { $0.id == preferences.selectedVersion } }
    var selectedJava: JavaRuntime? {
        let javaPath = selectedVersion.map { effectiveJavaPath(for: $0) } ?? preferences.javaPath
        if !javaPath.isEmpty {
            return javaRuntimes.first { $0.executable.path == javaPath }
        }
        let requirement = selectedVersion?.requiredJava ?? 17
        return javaRuntimes.first { $0.majorVersion == requirement }
            ?? javaRuntimes.filter { $0.majorVersion >= requirement }.min { $0.majorVersion < $1.majorVersion }
    }
    var visibleRemoteVersions: [RemoteVersion] {
        remoteVersions.filter {
            (preferences.showSnapshots || $0.type == "release") &&
            (search.isEmpty || $0.id.localizedCaseInsensitiveContains(search))
        }
    }
    var visibleInstalledVersions: [InstalledVersion] {
        installedVersions.filter {
            let hidden = profileSettings(for: $0).category == "hidden"
            return (showHiddenVersions ? hidden : !hidden)
                && (search.isEmpty || $0.id.localizedCaseInsensitiveContains(search)
                    || profileSettings(for: $0).displayDescription.localizedCaseInsensitiveContains(search))
        }
    }
    var isBusy: Bool { installingID != nil || isLaunching || preparingInstall || versionModState.mutating || versionExportState.isExporting }
    var memoryLimit: Int { max(1024, Int(ProcessInfo.processInfo.physicalMemory / 1024 / 1024) - 2048) }
    var memoryLabel: String { String(format: "%.1f GB", Double(preferences.memoryMB) / 1024) }
    var offlineNameWarning: String? {
        let name = preferences.playerName
        return name.range(of: #"^[0-9A-Za-z_]*$"#, options: .regularExpression) == nil
            ? "玩家名如果包含英文、数字、下划线以外的内容，可能会无法进入 Minecraft 1.18 以后的世界！" : nil
    }

    func start() async {
        await refreshLocal()
        if !didRestoreOfflineLogin {
            didRestoreOfflineLogin = true
            completeOfflineLogin()
        }
    }

    /// The original offline loader validates locally and remembers a successful name; no
    /// password, network request, or separate login button is involved.
    @discardableResult func completeOfflineLogin(reportErrors: Bool = false) -> Bool {
        do {
            let name = try OfflineLogin.validateName(preferences.playerName, versionID: selectedVersion.map { remoteVersionID(for: $0) })
            offlineNameHistory.removeAll { $0 == name }
            offlineNameHistory.insert(name, at: 0)
            preferences.playerName = name
            persistOfflineHistory()
            return true
        } catch {
            if reportErrors { errorMessage = error.localizedDescription }
            return false
        }
    }

    func selectOfflineName(_ name: String) { preferences.playerName = name }

    func deleteOfflineName(_ name: String) {
        offlineNameHistory.removeAll { $0 == name }
        // Matches the original delete action, including deleting an unselected history row.
        preferences.playerName = offlineNameHistory.first ?? ""
        persistOfflineHistory()
    }

    private func persistOfflineHistory() {
        defaults.set(offlineNameHistory, forKey: Self.offlineHistoryKey)
        savePreferences()
    }

    func refreshLocal() async {
        guard !scanning else { return }
        scanning = true
        let directory = gameDirectory
        let customJava = preferences.javaPath
        let result = await Task.detached(priority: .userInitiated) { () -> ([InstalledVersion], [JavaRuntime], [String: VersionProfileSettings], [String: String], [String]) in
            var runtimes = JavaDiscovery.discover()
            if !customJava.isEmpty, !runtimes.contains(where: { $0.executable.path == customJava }),
               let runtime = JavaDiscovery.inspect(executable: URL(fileURLWithPath: customJava)) { runtimes.insert(runtime, at: 0) }
            do {
                let versions = try LocalVersionScanner.scan(gameDirectory: directory)
                var profiles: [String: VersionProfileSettings] = [:]
                var originals: [String: String] = [:]
                var warnings: [String] = []
                for version in versions {
                    do { profiles[version.id] = try VersionSettingsRepository.load(version: version) }
                    catch { profiles[version.id] = VersionProfileSettings(); warnings.append("\(version.id)：\(error.localizedDescription)") }
                    let json = try? MinecraftVersionMetadata.resolve(versionID: version.id, gameDirectory: directory)
                    originals[version.id] = json?["pclOriginalVersion"] as? String ?? version.id
                    if let path = profiles[version.id]?.javaPath, !path.isEmpty,
                       !runtimes.contains(where: { $0.executable.path == path }),
                       let java = JavaDiscovery.inspect(executable: URL(fileURLWithPath: path)) { runtimes.append(java) }
                }
                return (versions, runtimes, profiles, originals, warnings)
            } catch { return ([], runtimes, [:], [:], [error.localizedDescription]) }
        }.value
        // A folder choice may change while Java detection is in progress.
        guard directory == gameDirectory else { scanning = false; await refreshLocal(); return }
        // A recycle operation may finish while the detached scan is returning.
        installedVersions = result.0.filter {
            FileManager.default.fileExists(atPath: $0.directory.appendingPathComponent("\($0.id).json").path)
        }
        javaRuntimes = result.1
        let availableIDs = Set(installedVersions.map(\.id))
        versionProfileCache = result.2.filter { availableIDs.contains($0.key) }
        originalVersionIDs = result.3.filter { availableIDs.contains($0.key) }
        if selectedVersion == nil { preferences.selectedVersion = installedVersions.first(where: { profileSettings(for: $0).category != "hidden" })?.id ?? "" }
        for warning in result.4 { appendLog(warning) }
        scanning = false
    }

    func fetchManifest() async {
        guard !manifestLoading else { return }
        manifestLoading = true
        defer { manifestLoading = false }
        do {
            let manifest = try await installer.fetchManifest()
            remoteVersions = manifest.versions
            appendLog("已获取官方版本列表，共 \(manifest.versions.count) 个版本。")
        } catch { report(error) }
    }

    func install(_ version: RemoteVersion, profileID: String? = nil) {
        guard !isBusy, !isGameRunning else { return }
        let targetID = profileID ?? version.id
        installingID = targetID
        completedInstallID = nil
        let operation = UUID()
        installOperation = operation
        installProgress = nil
        statusMessage = "正在安装 \(targetID)"
        appendLog("开始从 Mojang 官方源安装 \(version.id)，版本名称：\(targetID)。")
        let directory = gameDirectory
        installTask = Task { [self] in
            do {
                try await installer.install(version: version, gameDirectory: directory, profileID: targetID) { [self] progress in
                    Task { @MainActor in
                        if installOperation == operation { installProgress = progress }
                    }
                }
                preferences.selectedVersion = targetID
                completedInstallID = targetID
                statusMessage = "\(targetID) 安装完成"
                appendLog(statusMessage)
            } catch is CancellationError {
                statusMessage = "下载已取消，可再次安装以继续"
                appendLog(statusMessage)
            } catch { report(error) }
            installingID = nil
            installOperation = nil
            installProgress = nil
            installTask = nil
            await refreshLocal()
            if completedInstallID == targetID && page == .activity { page = .library }
        }
    }

    func cancelInstall() {
        if let installTask { installTask.cancel() }
        else { installProgress = nil }
    }

    func repair(_ version: InstalledVersion) {
        guard !isBusy, !isGameRunning else { return }
        let directory = gameDirectory
        let sourceID = remoteVersionID(for: version)
        preparingInstall = true
        Task {
            defer { preparingInstall = false }
            if remoteVersions.isEmpty { await fetchManifest() }
            guard directory == gameDirectory,
                  FileManager.default.fileExists(atPath: version.directory.appendingPathComponent("\(version.id).json").path) else {
                appendLog("补全任务已取消：版本或游戏文件夹已发生变化。")
                return
            }
            guard let remote = remoteVersions.first(where: { $0.id == sourceID }) else {
                if errorMessage == nil { errorMessage = "官方版本列表中没有此版本的基础游戏，无法补全文件。" }
                return
            }
            let raw = (try? MinecraftVersionMetadata.load(versionID: version.id, gameDirectory: directory)) ?? [:]
            let metadata = (try? MinecraftVersionMetadata.resolve(versionID: version.id, gameDirectory: directory)) ?? raw
            preparingInstall = false
            if let loader = MinecraftVersionMetadata.modLoader(in: metadata) {
                repairModdedVersion(version, remote: remote, loader: loader)
            } else {
                install(remote, profileID: version.id)
            }
            page = .activity
        }
    }

    func requestLaunch() {
        guard !isBusy, !isGameRunning else { return }
        guard let version = selectedVersion else { page = .downloads; return }
        do { preferences.playerName = try OfflineLogin.validateName(preferences.playerName, versionID: remoteVersionID(for: version)) }
        catch { errorMessage = error.localizedDescription; return }
        do { _ = try launchConfiguration(for: version) }
        catch { report(error); return }
        showLaunchConsent = true
    }

    func launchGame() {
        showLaunchConsent = false
        guard let version = selectedVersion, !isBusy, !isGameRunning else { return }
        guard completeOfflineLogin(reportErrors: true) else { return }
        let configuration: LaunchConfiguration
        do { configuration = try launchConfiguration(for: version) }
        catch { report(error); return }
        isLaunching = true
        requestedGameStop = false
        hasRenderedGame = false
        statusMessage = "正在准备游戏"
        appendLog("准备启动 \(version.id)，Java \(configuration.javaMajorVersion ?? version.requiredJava)，内存 \(configuration.memoryMB) MB，游戏目录：\(configuration.runtimeGameDirectory?.path ?? gameDirectory.path)。")
        let state = self
        Task {
            do {
                try await Task.detached(priority: .userInitiated) { [launcher, state] in
                    try launcher.launch(configuration: configuration, onOutput: { output in
                        Task { @MainActor in
                            state.appendLog(output)
                            if output.contains("Created:"), output.contains("textures/atlas/") {
                                state.hasRenderedGame = true
                                state.statusMessage = "游戏已启动"
                            }
                        }
                    }, onExit: { code in
                        Task { @MainActor in
                            state.isGameRunning = false
                            let stopped = state.requestedGameStop
                            state.statusMessage = stopped ? "游戏已停止" : (code == 0 ? "游戏已退出" : "游戏异常退出（\(code)）")
                            state.appendLog(state.statusMessage)
                            if code != 0 && !stopped { state.errorMessage = "游戏已退出，退出码 \(code)。请在任务与日志中查看详细输出。" }
                        }
                    })
                }.value
                isGameRunning = launcher.isRunning
                statusMessage = isGameRunning ? "游戏运行中" : "游戏已退出"
            } catch { report(error) }
            isLaunching = false
        }
    }

    func stopGame() { requestedGameStop = true; launcher.stop(); appendLog("已发送停止游戏请求。") }

    func prepareToQuit() { installTask?.cancel(); cancelVersionExport(); launcher.stop() }

    func chooseGameDirectory() {
        guard !isBusy, !isGameRunning else { return }
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true; panel.canChooseFiles = false; panel.canCreateDirectories = true
        panel.message = "选择 Minecraft 游戏目录（包含 versions、libraries 和 assets）"
        panel.directoryURL = gameDirectory
        if panel.runModal() == .OK, let url = panel.url {
            versionSettingsTargetID = nil
            preferences.gamePath = url.path
            Task { await refreshLocal() }
        }
    }

    func chooseJava() {
        let panel = NSOpenPanel()
        panel.canChooseFiles = true; panel.canChooseDirectories = false
        panel.showsHiddenFiles = true; panel.message = "选择 JDK 中 Contents/Home/bin/java 可执行文件"
        panel.directoryURL = URL(fileURLWithPath: "/Library/Java/JavaVirtualMachines")
        if panel.runModal() == .OK, let url = panel.url {
            guard let runtime = JavaDiscovery.inspect(executable: url) else {
                errorMessage = "此文件无法作为 Java 运行，请选择有效的 bin/java。"; return
            }
            if !javaRuntimes.contains(where: { $0.id == runtime.id }) { javaRuntimes.append(runtime) }
            preferences.javaPath = url.path
        }
    }

    func revealGameDirectory() {
        do { try FileManager.default.createDirectory(at: gameDirectory, withIntermediateDirectories: true)
            NSWorkspace.shared.open(gameDirectory)
        } catch { report(error) }
    }

    func reveal(_ version: InstalledVersion) { NSWorkspace.shared.open(version.directory) }

    func exportLogs() {
        let panel = NSSavePanel()
        panel.nameFieldStringValue = "PCL-SwiftUI.log"
        if panel.runModal() == .OK, let url = panel.url {
            do { try logs.joined(separator: "\n").write(to: url, atomically: true, encoding: .utf8) }
            catch { report(error) }
        }
    }

    func appendLog(_ message: String) {
        let time = Date.now.formatted(date: .omitted, time: .standard)
        logs.append(contentsOf: message.split(whereSeparator: \.isNewline).map { "[\(time)] \($0)" })
        if logs.count > 2000 { logs.removeFirst(logs.count - 2000) }
    }

    func report(_ error: Error) {
        errorMessage = error.localizedDescription
        statusMessage = "操作失败"
        appendLog("错误：\(error.localizedDescription)")
    }
    private func savePreferences() {
        var saved = preferences
        // A half-entered name is not a successful offline login. Restore the most recent
        // completed name on the next run, as LoginLegacyName does in the original project.
        saved.playerName = offlineNameHistory.first ?? ""
        if let data = try? JSONEncoder().encode(saved) { defaults.set(data, forKey: Self.preferencesKey) }
    }
}
