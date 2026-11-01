import AppKit
import Foundation
import PCLCore

/// Snapshot delivery can race between extraction and network branches. Keep the
/// newest immutable revision synchronously, before hopping onto the main actor.
final class InstallProgressRelay: @unchecked Sendable {
    private let lock = NSLock()
    private var value: InstallProgress?
    func accept(_ incoming: InstallProgress) -> Bool {
        lock.lock(); defer { lock.unlock() }
        if let old = value?.snapshot, let next = incoming.snapshot, next.revision < old.revision { return false }
        value = incoming
        return true
    }
    var latest: InstallProgress? { lock.lock(); defer { lock.unlock() }; return value }
}

/// Keep loader completion below 100% while the selected add-on is being verified
/// and the temporary profile has not yet been published under its final name.
private final class InitialFabricAPIProgress: @unchecked Sendable {
    private let lock = NSLock()
    private let title: String
    private let progress: @Sendable (InstallProgress) -> Void
    private var phase = 0
    private var fraction = 0.0
    private var revision: Int64 = 0

    init(title: String, progress: @escaping @Sendable (InstallProgress) -> Void) {
        self.title = title
        self.progress = progress
    }

    func update(_ value: InstallProgress, phase incomingPhase: Int) {
        lock.lock()
        guard incomingPhase >= phase, phase < 2 else { lock.unlock(); return }
        phase = incomingPhase
        let sourceFraction = min(1, max(0, value.snapshot?.totalFraction ?? value.fraction))
        fraction = max(fraction, incomingPhase == 0 ? sourceFraction * 0.8 : 0.8 + sourceFraction * 0.15)
        revision += 1
        let snapshot = snapshot(state: .waiting, downloaded: value.snapshot)
        let next = InstallProgress(message: value.message, completed: Int(fraction * 10_000), total: 10_000, snapshot: snapshot)
        lock.unlock()
        progress(next)
    }

    func commit() { publish(finished: false) }
    func finish() { publish(finished: true) }

    private func publish(finished: Bool) {
        lock.lock()
        phase = finished ? 3 : 2
        fraction = finished ? 1 : 0.98
        revision += 1
        let next = InstallProgress(message: finished ? "Fabric 与 Fabric API 安装完成" : "安装游戏",
            completed: Int(fraction * 10_000), total: 10_000,
            snapshot: snapshot(state: finished ? .completed : .running, downloaded: nil))
        lock.unlock()
        progress(next)
    }

    private func snapshot(state: InstallStageState, downloaded: InstallTaskSnapshot?) -> InstallTaskSnapshot {
        InstallTaskSnapshot(title: title, stages: [
            .init(id: "loader", title: "安装 Fabric", state: phase == 0 ? .running : .completed,
                  fraction: phase == 0 ? fraction / 0.8 : 1),
            .init(id: "fabricAPI", title: "下载 Fabric API", state: phase == 0 ? .waiting : phase == 1 ? .running : .completed,
                  fraction: phase == 0 ? 0 : phase == 1 ? max(0, (fraction - 0.8) / 0.15) : 1),
            .init(id: "final", title: "安装游戏", state: state)
        ], totalFraction: fraction, bytesPerSecond: downloaded?.bytesPerSecond ?? 0,
           remainingFiles: downloaded?.remainingFiles ?? 0, downloadedBytes: downloaded?.downloadedBytes ?? 0, revision: revision)
    }
}

enum ExtraComponentKind: String, CaseIterable, Hashable {
    case optiFine, optiFabric, liteLoader
    var title: String {
        switch self { case .optiFine: return "OptiFine"; case .optiFabric: return "OptiFabric"; case .liteLoader: return "LiteLoader" }
    }
    var icon: String {
        switch self { case .optiFine: return "GrassPath"; case .optiFabric: return "OptiFabric"; case .liteLoader: return "Egg" }
    }
    static func optiFabricVersionLabel(_ raw: String) -> String {
        var value = raw.lowercased().replacingOccurrences(of: "optifabric-", with: "")
            .replacingOccurrences(of: ".jar", with: "").trimmingCharacters(in: .whitespacesAndNewlines)
        while value.first == "v" { value.removeFirst() }
        return value
    }
}

enum ExtraComponentVersion: Hashable, Identifiable {
    case optiFine(OptiFineVersion), optiFabric(CommunityVersion), liteLoader(LiteLoaderVersion)
    var kind: ExtraComponentKind {
        switch self { case .optiFine: return .optiFine; case .optiFabric: return .optiFabric; case .liteLoader: return .liteLoader }
    }
    var id: String {
        switch self { case .optiFine(let value): return "optiFine:" + value.id; case .optiFabric(let value): return "optiFabric:" + value.id; case .liteLoader(let value): return "liteLoader:" + value.id }
    }
    var title: String {
        switch self { case .optiFine(let value): return value.displayName; case .optiFabric(let value): return ExtraComponentKind.optiFabricVersionLabel(value.versionNumber); case .liteLoader(let value): return value.displayName }
    }
}

struct ExtraComponentCatalogue {
    var optiFine: [OptiFineVersion] = []
    var optiFabric: [CommunityVersion] = []
    var liteLoader: [LiteLoaderVersion] = []
    var expanded: ExtraComponentKind?
    var loading: Set<ExtraComponentKind> = []
    var loaded: Set<ExtraComponentKind> = []
    var errors: [ExtraComponentKind: String] = [:]
    var requests: [ExtraComponentKind: UUID] = [:]
    var tasks: [ExtraComponentKind: Task<Void, Never>] = [:]
    mutating func reset() {
        tasks.values.forEach { $0.cancel() }
        self = ExtraComponentCatalogue()
    }
    func versions(_ kind: ExtraComponentKind) -> [ExtraComponentVersion] {
        switch kind { case .optiFine: return optiFine.map(ExtraComponentVersion.optiFine); case .optiFabric: return optiFabric.map(ExtraComponentVersion.optiFabric); case .liteLoader: return liteLoader.map(ExtraComponentVersion.liteLoader) }
    }
}

struct ModdedInstallState {
    var selectedLoader: ModLoaderVersion?
    var selectedOptiFine: OptiFineVersion?
    var selectedLiteLoader: LiteLoaderVersion?
    var selectedOptiFabric: CommunityVersion?
    var extraCatalogue = ExtraComponentCatalogue()
    var optiFabricAutoSelection = true
    var expandedLoader: ModLoaderKind?
    var loaderVersions: [ModLoaderKind: [ModLoaderVersion]] = [:]
    var loaderErrors: [ModLoaderKind: String] = [:]
    var loadingLoaders: Set<ModLoaderKind> = []
    var catalogueRequest = UUID()
    var loaderTasks: [ModLoaderKind: Task<Void, Never>] = [:]
    var selectedFabricAPI: CommunityVersion?
    var fabricAPIVersions: [CommunityVersion] = []
    var expandedFabricAPI = false
    var loadingFabricAPI = false
    var fabricAPIError: String?
    var fabricAPICatalogueLoaded = false
    var fabricAPIAutoSelection = true
    var fabricAPIRequest = UUID()
    var fabricAPITask: Task<Void, Never>?
    var automaticInstallName = ""
    var showModpackSheet = false
    var inspectingModpack = false
    var modpackArchive: URL?
    var modpackDescriptor: ModpackDescriptor?
    var modpackNameDraft = ""
    var modpackError: String?
    var modpackRequest = UUID()
    var inspectionTask: Task<Void, Never>?
}

extension ModLoaderKind {
    var pclIcon: String {
        switch self {
        case .forge: return "Anvil"
        case .neoForge: return "NeoForge"
        case .fabric: return "Fabric"
        case .quilt: return "Fabric"
        }
    }
}

@MainActor extension LauncherStore {
    var suggestedInstallName: String {
        guard let base = currentRemoteSelection else { return installNameDraft }
        var name = base.id
        if let loader = moddedState.selectedLoader { name += "-\(loader.kind.displayName)-\(loader.version)" }
        if let optiFine = moddedState.selectedOptiFine { name += "-OptiFine-" + optiFine.version }
        if let liteLoader = moddedState.selectedLiteLoader { name += "-LiteLoader-" + liteLoader.version }
        return name
    }

    func resetModdedSelection(for version: RemoteVersion? = nil) {
        for task in moddedState.loaderTasks.values { task.cancel() }
        moddedState.loaderTasks = [:]
        moddedState.catalogueRequest = UUID()
        moddedState.loaderVersions = [:]
        moddedState.loaderErrors = [:]
        moddedState.loadingLoaders = []
        moddedState.selectedLoader = nil
        moddedState.selectedOptiFine = nil
        moddedState.selectedLiteLoader = nil
        moddedState.selectedOptiFabric = nil
        moddedState.extraCatalogue.reset()
        moddedState.optiFabricAutoSelection = true
        moddedState.expandedLoader = nil
        moddedState.fabricAPITask?.cancel()
        moddedState.fabricAPITask = nil
        moddedState.fabricAPIRequest = UUID()
        moddedState.selectedFabricAPI = nil
        moddedState.fabricAPIVersions = []
        moddedState.expandedFabricAPI = false
        moddedState.loadingFabricAPI = false
        moddedState.fabricAPIError = nil
        moddedState.fabricAPICatalogueLoaded = false
        moddedState.fabricAPIAutoSelection = true
        moddedState.automaticInstallName = version?.id ?? ""
    }

    func toggleLoaderCatalogue(_ kind: ModLoaderKind) {
        guard !isBusy, !isGameRunning, currentRemoteSelection != nil, initialLoaderConflict(kind) == nil else { return }
        moddedState.expandedFabricAPI = false
        moddedState.extraCatalogue.expanded = nil
        if moddedState.expandedLoader == kind { moddedState.expandedLoader = nil; return }
        moddedState.expandedLoader = kind
        if moddedState.loaderVersions[kind] == nil && !moddedState.loadingLoaders.contains(kind) {
            loadLoaderCatalogue(kind)
        }
    }

    func loadLoaderCatalogue(_ kind: ModLoaderKind) {
        guard let base = currentRemoteSelection, !moddedState.loadingLoaders.contains(kind) else { return }
        let request = moddedState.catalogueRequest
        moddedState.loadingLoaders.insert(kind)
        moddedState.loaderErrors[kind] = nil
        moddedState.loaderTasks[kind] = Task { [self] in
            defer {
                if moddedState.catalogueRequest == request {
                    moddedState.loadingLoaders.remove(kind)
                    moddedState.loaderTasks[kind] = nil
                }
            }
            do {
                let versions = try await modLoaderInstaller.fetchVersions(kind: kind, minecraftVersion: base.id)
                try Task.checkCancellation()
                guard moddedState.catalogueRequest == request, currentRemoteSelection?.id == base.id else { return }
                moddedState.loaderVersions[kind] = versions
            } catch is CancellationError {
            } catch {
                guard moddedState.catalogueRequest == request else { return }
                moddedState.loaderErrors[kind] = error.localizedDescription
            }
        }
    }

    func chooseLoader(_ version: ModLoaderVersion?) {
        guard !isBusy, !isGameRunning, currentRemoteSelection != nil else { return }
        let wasFabric = moddedState.selectedLoader?.kind == .fabric
        let followsSuggestedName = installNameDraft == moddedState.automaticInstallName
        moddedState.selectedLoader = version
        moddedState.expandedLoader = nil
        moddedState.automaticInstallName = suggestedInstallName
        if followsSuggestedName { installNameDraft = suggestedInstallName }
        if version?.kind == .fabric {
            if !wasFabric { moddedState.fabricAPIAutoSelection = true }
            if moddedState.fabricAPICatalogueLoaded {
                if moddedState.fabricAPIAutoSelection { moddedState.selectedFabricAPI = moddedState.fabricAPIVersions.first }
            } else if !moddedState.loadingFabricAPI { loadFabricAPICatalogue() }
        } else {
            moddedState.fabricAPITask?.cancel()
            moddedState.fabricAPITask = nil
            moddedState.fabricAPIRequest = UUID()
            moddedState.loadingFabricAPI = false
            moddedState.expandedFabricAPI = false
            moddedState.selectedFabricAPI = nil
        }
        reconcileInitialExtraComponents()
        moddedState.automaticInstallName = suggestedInstallName
        if followsSuggestedName { installNameDraft = suggestedInstallName }
    }

    var fabricAPISelectionError: String? {
        guard moddedState.selectedLoader?.kind == .fabric else { return nil }
        if moddedState.loadingFabricAPI && moddedState.fabricAPIAutoSelection { return "正在获取 Fabric API 版本列表，请稍候。" }
        if moddedState.fabricAPIAutoSelection, moddedState.fabricAPIError != nil {
            return "Fabric API 版本列表获取失败，请重试，或选择不安装 Fabric API。"
        }
        if let selected = moddedState.selectedFabricAPI,
           !selected.gameVersions.contains(currentRemoteSelection?.id ?? "") || !selected.loaders.contains("fabric") {
            return "所选 Fabric API 与 Minecraft 版本不兼容，请重新选择。"
        }
        return nil
    }

    func toggleFabricAPICatalogue() {
        guard !isBusy, !isGameRunning, moddedState.selectedLoader?.kind == .fabric else { return }
        moddedState.expandedFabricAPI.toggle()
        moddedState.expandedLoader = nil
        if moddedState.expandedFabricAPI && !moddedState.fabricAPICatalogueLoaded && !moddedState.loadingFabricAPI {
            loadFabricAPICatalogue()
        }
    }

    func loadFabricAPICatalogue() {
        guard !isBusy, !isGameRunning, let base = currentRemoteSelection,
              moddedState.selectedLoader?.kind == .fabric, !moddedState.loadingFabricAPI else { return }
        let request = UUID()
        moddedState.fabricAPIRequest = request
        moddedState.loadingFabricAPI = true
        moddedState.fabricAPIError = nil
        moddedState.fabricAPITask = Task { [self] in
            defer {
                if moddedState.fabricAPIRequest == request {
                    moddedState.loadingFabricAPI = false
                    moddedState.fabricAPITask = nil
                }
            }
            do {
                let versions = try await fabricAPIComponent.versions(minecraftVersion: base.id)
                try Task.checkCancellation()
                guard moddedState.fabricAPIRequest == request, currentRemoteSelection?.id == base.id,
                      moddedState.selectedLoader?.kind == .fabric else { return }
                moddedState.fabricAPIVersions = versions
                moddedState.fabricAPICatalogueLoaded = true
                if moddedState.fabricAPIAutoSelection { moddedState.selectedFabricAPI = versions.first }
            } catch is CancellationError {
            } catch {
                guard moddedState.fabricAPIRequest == request, currentRemoteSelection?.id == base.id else { return }
                moddedState.fabricAPIError = error.localizedDescription
            }
        }
    }

    func chooseFabricAPI(_ version: CommunityVersion?) {
        guard !isBusy, !isGameRunning, let base = currentRemoteSelection,
              moddedState.selectedLoader?.kind == .fabric else { return }
        if let version {
            guard version.gameVersions.contains(base.id), version.loaders.contains("fabric"),
                  moddedState.fabricAPIVersions.contains(where: { $0.id == version.id }) else { return }
        } else if moddedState.loadingFabricAPI {
            moddedState.fabricAPITask?.cancel()
            moddedState.fabricAPITask = nil
            moddedState.fabricAPIRequest = UUID()
            moddedState.loadingFabricAPI = false
        }
        moddedState.fabricAPIAutoSelection = false
        moddedState.selectedFabricAPI = version
        moddedState.expandedFabricAPI = false
    }

    var initialExtraSelection: ExtraComponentSelection {
        ExtraComponentSelection(optiFine: moddedState.selectedOptiFine, liteLoader: moddedState.selectedLiteLoader,
                                optiFabric: moddedState.selectedOptiFabric)
    }

    var extraComponentSelectionError: String? {
        guard let base = currentRemoteSelection else { return nil }
        if moddedState.optiFabricAutoSelection && moddedState.selectedLoader?.kind == .fabric && moddedState.selectedOptiFine != nil {
            if moddedState.extraCatalogue.loading.contains(.optiFabric) { return "正在获取 OptiFabric 版本，请稍候。" }
            if moddedState.extraCatalogue.errors[.optiFabric] != nil { return "OptiFabric 获取失败，请重试，或清除 OptiFine。" }
        }
        return ExtraComponentsInstaller.compatibilityError(minecraftVersion: base.id, loader: moddedState.selectedLoader, selection: initialExtraSelection)
    }

    func initialExtraSelection(_ kind: ExtraComponentKind) -> ExtraComponentVersion? {
        switch kind {
        case .optiFine: return moddedState.selectedOptiFine.map(ExtraComponentVersion.optiFine)
        case .optiFabric: return moddedState.selectedOptiFabric.map(ExtraComponentVersion.optiFabric)
        case .liteLoader: return moddedState.selectedLiteLoader.map(ExtraComponentVersion.liteLoader)
        }
    }

    func extraComponentUnavailableReason(_ kind: ExtraComponentKind, minecraft: String, loader: ModLoaderKind?, optiFine: OptiFineVersion?) -> String? {
        switch kind {
        case .optiFine:
            if loader == .neoForge || loader == .quilt { return "与 \(loader!.displayName) 不兼容" }
            if loader == .fabric && minecraft.compare("1.20.4", options: .numeric) == .orderedDescending { return "此版本的 Fabric 不支持 OptiFine" }
            if loader == .forge && minecraft.compare("1.13", options: .numeric) != .orderedAscending && minecraft.compare("1.14.3", options: .numeric) != .orderedDescending { return "此版本的 Forge 不支持 OptiFine" }
        case .optiFabric:
            if loader != .fabric || optiFine == nil { return "需要安装 Fabric 与 OptiFine" }
        case .liteLoader:
            if loader == .fabric || loader == .neoForge || loader == .quilt { return "与 \(loader!.displayName) 不兼容" }
            if minecraft.compare("1.12.2", options: .numeric) == .orderedDescending { return "此 Minecraft 版本没有 LiteLoader" }
        }
        return nil
    }

    func optiFineFitsForge(_ optiFine: OptiFineVersion, loader: ModLoaderVersion?) -> Bool {
        guard let loader else { return true }
        guard loader.kind == .forge else { return loader.kind == .fabric }
        guard loader.minecraftVersion == optiFine.minecraftVersion, let required = optiFine.requiredForgeVersion else { return false }
        if required.isEmpty { return true }
        return required.contains(".") ? loader.version == required : loader.version.split(separator: ".").last.map(String.init) == required
    }

    func initialLoaderConflict(_ kind: ModLoaderKind) -> String? {
        if moddedState.selectedLiteLoader != nil && kind != .forge { return "与 LiteLoader 不兼容" }
        if let optiFine = moddedState.selectedOptiFine,
           let reason = extraComponentUnavailableReason(.optiFine, minecraft: optiFine.minecraftVersion, loader: kind, optiFine: optiFine) { return reason }
        return nil
    }

    func initialLoaderVersions(_ kind: ModLoaderKind) -> [ModLoaderVersion] {
        (moddedState.loaderVersions[kind] ?? []).filter { value in
            guard let optiFine = moddedState.selectedOptiFine else { return true }
            return optiFineFitsForge(optiFine, loader: value)
        }
    }

    func initialExtraVersions(_ kind: ExtraComponentKind) -> [ExtraComponentVersion] {
        moddedState.extraCatalogue.versions(kind).filter { entry in
            if case .optiFine(let value) = entry { return optiFineFitsForge(value, loader: moddedState.selectedLoader) }
            return true
        }
    }

    func toggleExtraComponentCatalogue(_ kind: ExtraComponentKind) {
        guard !isBusy, !isGameRunning, let base = currentRemoteSelection,
              extraComponentUnavailableReason(kind, minecraft: base.id, loader: moddedState.selectedLoader?.kind, optiFine: moddedState.selectedOptiFine) == nil else { return }
        moddedState.expandedLoader = nil; moddedState.expandedFabricAPI = false
        moddedState.extraCatalogue.expanded = moddedState.extraCatalogue.expanded == kind ? nil : kind
        if moddedState.extraCatalogue.expanded == kind && !moddedState.extraCatalogue.loaded.contains(kind) && !moddedState.extraCatalogue.loading.contains(kind) { loadExtraComponentCatalogue(kind) }
    }

    func loadExtraComponentCatalogue(_ kind: ExtraComponentKind) {
        guard !isBusy, !isGameRunning, let base = currentRemoteSelection, !moddedState.extraCatalogue.loading.contains(kind) else { return }
        let request = UUID()
        moddedState.extraCatalogue.requests[kind] = request
        moddedState.extraCatalogue.loading.insert(kind); moddedState.extraCatalogue.errors[kind] = nil
        moddedState.extraCatalogue.tasks[kind] = Task {
            defer {
                if moddedState.extraCatalogue.requests[kind] == request {
                    moddedState.extraCatalogue.loading.remove(kind); moddedState.extraCatalogue.tasks[kind] = nil
                }
            }
            do {
                switch kind {
                case .optiFine:
                    let entries = try await extraComponentsInstaller.fetchOptiFineVersions(minecraftVersion: base.id)
                    try Task.checkCancellation()
                    guard moddedState.extraCatalogue.requests[kind] == request, currentRemoteSelection?.id == base.id else { return }
                    moddedState.extraCatalogue.optiFine = entries
                case .liteLoader:
                    let entries = try await extraComponentsInstaller.fetchLiteLoaderVersions(minecraftVersion: base.id)
                    try Task.checkCancellation()
                    guard moddedState.extraCatalogue.requests[kind] == request, currentRemoteSelection?.id == base.id else { return }
                    moddedState.extraCatalogue.liteLoader = entries
                case .optiFabric:
                    let entries = try await extraComponentsInstaller.fetchOptiFabricVersions(minecraftVersion: base.id)
                    try Task.checkCancellation()
                    guard moddedState.extraCatalogue.requests[kind] == request, currentRemoteSelection?.id == base.id,
                          moddedState.selectedLoader?.kind == .fabric, moddedState.selectedOptiFine != nil else { return }
                    moddedState.extraCatalogue.optiFabric = entries
                    if moddedState.optiFabricAutoSelection { moddedState.selectedOptiFabric = entries.first }
                    moddedState.optiFabricAutoSelection = false
                }
                moddedState.extraCatalogue.loaded.insert(kind)
            } catch is CancellationError {} catch {
                guard moddedState.extraCatalogue.requests[kind] == request, currentRemoteSelection?.id == base.id else { return }
                moddedState.extraCatalogue.errors[kind] = error.localizedDescription
            }
        }
    }

    func chooseExtraComponent(_ entry: ExtraComponentVersion?, kind: ExtraComponentKind) {
        guard !isBusy, !isGameRunning, currentRemoteSelection != nil else { return }
        if let entry { guard entry.kind == kind, initialExtraVersions(kind).contains(entry) else { return } }
        let followsSuggestedName = installNameDraft == moddedState.automaticInstallName
        moddedState.extraCatalogue.tasks[kind]?.cancel(); moddedState.extraCatalogue.requests[kind] = UUID()
        moddedState.extraCatalogue.loading.remove(kind); moddedState.extraCatalogue.errors[kind] = nil
        switch kind {
        case .optiFine: if case .optiFine(let value)? = entry { moddedState.selectedOptiFine = value } else { moddedState.selectedOptiFine = nil }
        case .liteLoader: if case .liteLoader(let value)? = entry { moddedState.selectedLiteLoader = value } else { moddedState.selectedLiteLoader = nil }
        case .optiFabric:
            if case .optiFabric(let value)? = entry { moddedState.selectedOptiFabric = value } else { moddedState.selectedOptiFabric = nil }
            moddedState.optiFabricAutoSelection = false
        }
        moddedState.extraCatalogue.expanded = nil
        reconcileInitialExtraComponents()
        moddedState.automaticInstallName = suggestedInstallName
        if followsSuggestedName { installNameDraft = suggestedInstallName }
    }

    private func reconcileInitialExtraComponents() {
        if let selected = moddedState.selectedOptiFine,
           extraComponentUnavailableReason(.optiFine, minecraft: selected.minecraftVersion, loader: moddedState.selectedLoader?.kind, optiFine: selected) != nil || !optiFineFitsForge(selected, loader: moddedState.selectedLoader) {
            moddedState.selectedOptiFine = nil
        }
        if let base = currentRemoteSelection, extraComponentUnavailableReason(.liteLoader, minecraft: base.id, loader: moddedState.selectedLoader?.kind, optiFine: moddedState.selectedOptiFine) != nil { moddedState.selectedLiteLoader = nil }
        if moddedState.selectedLoader?.kind == .fabric && moddedState.selectedOptiFine != nil {
            if moddedState.extraCatalogue.loaded.contains(.optiFabric) {
                if moddedState.optiFabricAutoSelection { moddedState.selectedOptiFabric = moddedState.extraCatalogue.optiFabric.first; moddedState.optiFabricAutoSelection = false }
            } else if !moddedState.extraCatalogue.loading.contains(.optiFabric) { loadExtraComponentCatalogue(.optiFabric) }
        } else {
            moddedState.extraCatalogue.tasks[.optiFabric]?.cancel(); moddedState.extraCatalogue.requests[.optiFabric] = UUID()
            moddedState.extraCatalogue.loading.remove(.optiFabric); moddedState.extraCatalogue.errors[.optiFabric] = nil
            moddedState.selectedOptiFabric = nil; moddedState.optiFabricAutoSelection = true
        }
    }

    func installSelectedModdedVersion() {
        if moddedState.selectedOptiFine != nil || moddedState.selectedLiteLoader != nil || moddedState.selectedOptiFabric != nil {
            installSelectedExtraComponents()
            return
        }
        guard let base = currentRemoteSelection, let loader = moddedState.selectedLoader, !isBusy, !isGameRunning else { return }
        if let error = installNameError { errorMessage = error; return }
        if let error = fabricAPISelectionError { errorMessage = error; return }
        let fabricAPI = loader.kind == .fabric ? moddedState.selectedFabricAPI : nil
        let targetID = installNameDraft
        let directory = gameDirectory
        let reuseProfiles = installedVersions.map { ($0, profileSettings(for: $0).isolateGameDirectory) }
        currentRemoteSelection = nil
        resetModdedSelection()
        runModdedInstallation(profileID: targetID, directory: directory,
                              summary: "安装 \(loader.kind.displayName) \(loader.version)",
                              checkCancellationAfterOperation: fabricAPI == nil) { [self] progress in
            let java = try await installationJava(minecraftVersion: base.id, metadataURL: base.url)
            guard let fabricAPI else {
                try await modLoaderInstaller.install(version: loader, minecraftVersion: base, gameDirectory: directory,
                    profileID: targetID, javaExecutable: java.executable, progress: progress)
                return
            }
            let publication = InitialFabricAPIProgress(title: "安装 Fabric 与 Fabric API： " + targetID, progress: progress)
            let temporaryID = "PCL-Install-" + UUID().uuidString
            let temporary = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(temporaryID)", under: directory)
            guard !FileManager.default.fileExists(atPath: temporary.path) else { throw VersionManagementError.destinationExists }
            var preserveTemporary = false
            defer { if !preserveTemporary { try? FileManager.default.removeItem(at: temporary) } }
            try await modLoaderInstaller.install(version: loader, minecraftVersion: base, gameDirectory: directory,
                profileID: temporaryID, javaExecutable: java.executable, progress: { publication.update($0, phase: 0) })
            let reuseFiles = await Task.detached(priority: .utility) {
                reuseProfiles.flatMap { version, isolate in
                    (try? FabricAPIComponent.installed(gameDirectory: directory, version: version, isolate: isolate).files.map(\.url)) ?? []
                }
            }.value
            try Task.checkCancellation()
            let file = try await fabricAPIComponent.prepare(version: fabricAPI, gameDirectory: directory,
                reuseFiles: reuseFiles, progress: { publication.update($0, phase: 1) })
            let metadata = try MinecraftVersionMetadata.resolve(versionID: temporaryID, gameDirectory: directory)
            let local = InstalledVersion(id: temporaryID, type: base.type, requiredJava: MinecraftVersionMetadata.requiredJava(metadata),
                directory: temporary, inheritsFrom: base.id, modLoader: loader)
            let previous = try FabricAPIComponent.installed(gameDirectory: directory, version: local, isolate: true)
            let transaction = try FabricAPIComponent.apply(preparedFile: file, gameDirectory: directory,
                version: local, isolate: true, expectedState: previous)
            do {
                try Task.checkCancellation()
                publication.commit()
                _ = try VersionManager.rename(version: local, to: targetID, gameDirectory: directory)
                transaction.finish()
                publication.finish()
            } catch {
                let failure = error
                do { try transaction.rollback() }
                catch { preserveTemporary = true; throw error }
                throw failure
            }
        }
    }

    private func installSelectedExtraComponents() {
        guard let base = currentRemoteSelection, !isBusy, !isGameRunning else { return }
        if let error = installNameError ?? fabricAPISelectionError ?? extraComponentSelectionError { errorMessage = error; return }
        let directory = gameDirectory, targetID = installNameDraft
        let loader = moddedState.selectedLoader, selection = initialExtraSelection, api = moddedState.selectedFabricAPI
        currentRemoteSelection = nil
        resetModdedSelection()
        runModdedInstallation(profileID: targetID, directory: directory, summary: "安装游戏组件", checkCancellationAfterOperation: false) { [self] progress in
            let java = try await installationJava(minecraftVersion: base.id, metadataURL: base.url)
            try await extraComponentsInstaller.install(minecraft: base, loader: loader, selection: selection, fabricAPI: api,
                gameDirectory: directory, profileID: targetID, javaExecutable: java.executable, progress: progress)
        }
    }

    func repairModdedVersion(_ version: InstalledVersion, remote: RemoteVersion, loader: ModLoaderVersion) {
        guard !isBusy, !isGameRunning else { return }
        let directory = gameDirectory
        runModdedInstallation(profileID: version.id, directory: directory,
                              summary: "补全 \(loader.kind.displayName) \(loader.version)") { [self] progress in
            let java = try await installationJava(minecraftVersion: remote.id, metadataURL: remote.url)
            try await modLoaderInstaller.install(version: loader, minecraftVersion: remote, gameDirectory: directory,
                profileID: version.id, javaExecutable: java.executable, progress: progress)
        }
    }

    func chooseModpackArchive() {
        guard !isBusy, !isGameRunning else { return }
        let panel = NSOpenPanel()
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        panel.allowsMultipleSelection = false
        // Use literal extensions for .mrpack, which may have no registered UTI.
        // The importer separately validates the ZIP bytes and pack metadata.
        panel.allowedFileTypes = ["zip", "mrpack"]
        panel.message = "选择 Modrinth、CurseForge、MultiMC / Prism 或完整游戏版本整合包"
        panel.prompt = "选择整合包"
        let completion: (NSApplication.ModalResponse) -> Void = { [weak self] response in
            guard response == .OK, let archive = panel.url else { return }
            self?.inspectModpackArchive(archive)
        }
        if let window = NSApp.keyWindow ?? NSApp.mainWindow {
            panel.beginSheetModal(for: window, completionHandler: completion)
        } else {
            panel.begin(completionHandler: completion)
        }
    }

    func inspectModpackArchive(_ archive: URL) {
        guard !isBusy, !isGameRunning else { return }
        dismissModpackPreview()
        let request = UUID()
        moddedState.modpackRequest = request
        moddedState.modpackArchive = archive
        moddedState.modpackNameDraft = suggestedPackName(archive.deletingPathExtension().lastPathComponent)
        moddedState.inspectingModpack = true
        moddedState.showModpackSheet = true
        preparingInstall = true
        moddedState.inspectionTask = Task { [self] in
            defer {
                if moddedState.modpackRequest == request {
                    moddedState.inspectingModpack = false
                    moddedState.inspectionTask = nil
                    preparingInstall = false
                }
            }
            do {
                let descriptor = try await modpackImporter.inspect(archive: archive)
                try Task.checkCancellation()
                guard moddedState.modpackRequest == request else { return }
                moddedState.modpackDescriptor = descriptor
                moddedState.modpackNameDraft = suggestedPackName(descriptor.name)
            } catch is CancellationError {
            } catch {
                guard moddedState.modpackRequest == request else { return }
                moddedState.modpackError = error.localizedDescription
            }
        }
    }

    var modpackNameError: String? {
        guard moddedState.modpackDescriptor != nil else { return nil }
        do {
            try MinecraftInstaller.validateProfileID(moddedState.modpackNameDraft)
            let target = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(moddedState.modpackNameDraft)", under: gameDirectory)
            if FileManager.default.fileExists(atPath: target.path) { return "该版本名称已存在，请修改版本名！" }
            return nil
        } catch { return error.localizedDescription }
    }

    func dismissModpackPreview() {
        moddedState.inspectionTask?.cancel()
        moddedState.inspectionTask = nil
        moddedState.modpackRequest = UUID()
        if moddedState.inspectingModpack { preparingInstall = false }
        moddedState.inspectingModpack = false
        moddedState.showModpackSheet = false
        moddedState.modpackArchive = nil
        moddedState.modpackDescriptor = nil
        moddedState.modpackError = nil
    }

    func importSelectedModpack() {
        guard let archive = moddedState.modpackArchive, let descriptor = moddedState.modpackDescriptor,
              !moddedState.inspectingModpack, !isBusy, !isGameRunning else { return }
        if let error = modpackNameError { moddedState.modpackError = error; return }
        let targetID = moddedState.modpackNameDraft
        let directory = gameDirectory
        dismissModpackPreview()
        runModdedInstallation(profileID: targetID, directory: directory, summary: "导入整合包 \(descriptor.name)") { [self] progress in
            if remoteVersions.isEmpty { await fetchManifest() }
            let metadataURL = remoteVersions.first { $0.id == descriptor.minecraftVersion }?.url
            let java = try await installationJava(minecraftVersion: descriptor.minecraftVersion, metadataURL: metadataURL)
            try await modpackImporter.importPack(archive: archive, gameDirectory: directory, profileID: targetID,
                javaExecutable: java.executable, progress: progress)
        }
    }

    func runModdedInstallation(profileID: String, directory: URL, summary: String,
        selectVersionOnSuccess: Bool = true, successPage: LauncherPage = .library, successMessage: String? = nil,
        checkCancellationAfterOperation: Bool = true,
        operation: @escaping (@escaping @Sendable (InstallProgress) -> Void) async throws -> Void) {
        guard !isBusy, !isGameRunning else { return }
        let token = UUID()
        installingID = profileID
        completedInstallID = nil
        installOperation = token
        installProgress = InstallProgress(message: "准备安装环境", completed: 0, total: 1)
        statusMessage = "\(summary) · \(profileID)"
        appendLog(statusMessage)
        page = .activity
        let relay = InstallProgressRelay()
        installTask = Task { [self] in
            var failed = false
            do {
                try await operation { [self] value in
                    guard relay.accept(value) else { return }
                    Task { @MainActor in
                        guard installOperation == token else { return }
                        if let old = installProgress?.snapshot, let next = value.snapshot, next.revision < old.revision { return }
                        installProgress = value
                    }
                }
                if checkCancellationAfterOperation { try Task.checkCancellation() }
                if selectVersionOnSuccess && directory == gameDirectory { preferences.selectedVersion = profileID }
                completedInstallID = profileID
                statusMessage = successMessage ?? "\(profileID) 安装完成"
                appendLog(statusMessage)
            } catch is CancellationError {
                statusMessage = "安装已取消，已下载的公共文件会保留"
                appendLog(statusMessage)
            } catch {
                failed = true
                statusMessage = error.localizedDescription
                appendLog(statusMessage)
                let latest = relay.latest
                let snapshot: InstallTaskSnapshot
                if let existing = latest?.snapshot, existing.stages.contains(where: { $0.state == .failed }) {
                    snapshot = existing
                } else {
                    snapshot = InstallTaskSnapshot(title: summary + "： " + profileID,
                        stages: [.init(id: "failed", title: summary, state: .failed)],
                        totalFraction: latest?.fraction ?? 0)
                }
                installProgress = InstallProgress(message: statusMessage, completed: latest?.completed ?? 0,
                    total: latest?.total ?? 1, snapshot: snapshot)
                page = .activity
            }
            installingID = nil
            installOperation = nil
            if !failed { installProgress = nil }
            installTask = nil
            await refreshLocal()
            if completedInstallID == profileID && page == .activity { page = successPage }
        }
    }

    func installationJava(minecraftVersion: String, metadataURL: URL? = nil) async throws -> JavaRuntime {
        var required = MinecraftVersionMetadata.requiredJava(["id": minecraftVersion])
        if let metadataURL {
            let data = try await MinecraftInstaller.fetchData(url: metadataURL)
            guard let metadata = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
                throw MinecraftInstallError.invalidMetadata("版本元数据格式错误")
            }
            required = MinecraftVersionMetadata.requiredJava(metadata)
        }
        try Task.checkCancellation()
        if !preferences.javaPath.isEmpty {
            guard let runtime = JavaDiscovery.inspect(executable: URL(fileURLWithPath: preferences.javaPath)), runtime.majorVersion >= required else {
                throw LauncherError.invalidJava("该整合包或组件需要 Java \(required) 或更新版本，请在设置中更换 Java。")
            }
            return runtime
        }
        if let runtime = javaRuntimes.first(where: { $0.majorVersion == required })
            ?? javaRuntimes.filter({ $0.majorVersion >= required }).min(by: { $0.majorVersion < $1.majorVersion }) { return runtime }
        throw LauncherError.invalidJava("未找到 Java \(required) 或更新版本，请在设置中手动导入对应的 Java。")
    }

    private func suggestedPackName(_ value: String) -> String {
        let forbidden = CharacterSet(charactersIn: "/\\:*?\"<>|!;").union(.controlCharacters)
        var name = value.unicodeScalars.map { forbidden.contains($0) ? "_" : String($0) }.joined()
            .trimmingCharacters(in: .whitespacesAndNewlines)
        while name.hasPrefix(".") { name.removeFirst() }
        while name.hasSuffix(".") || name.hasSuffix(" ") { name.removeLast() }
        while name.utf8.count > 200 { name.removeLast() }
        return name.isEmpty ? "整合包" : name
    }
}
