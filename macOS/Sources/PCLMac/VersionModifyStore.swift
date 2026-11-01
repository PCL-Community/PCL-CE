import AppKit
import Combine
import PCLCore

@MainActor final class VersionModifyState: ObservableObject {
    @Published var targetKey = ""
    @Published var originalName = ""
    @Published var newName = ""
    @Published var originalMinecraft = ""
    @Published var minecraft = ""
    @Published var originalLoader: ModLoaderVersion?
    @Published var originalExtras: ExtraComponentSnapshot?
    @Published var originalOptiFine: OptiFineVersion?
    @Published var selectedOptiFine: OptiFineVersion?
    @Published var originalLiteLoader: LiteLoaderVersion?
    @Published var selectedLiteLoader: LiteLoaderVersion?
    @Published var originalOptiFabric: LocalModFile?
    @Published var selectedOptiFabric: CommunityVersion?
    @Published var keepOriginalOptiFabric = true
    @Published var extraCatalogue = ExtraComponentCatalogue()
    @Published var optiFabricAutoSelection = false
    var optiFabricExplicitNone = false
    @Published var rematchingExtras: Set<ExtraComponentKind> = []
    var pendingExtraVersions: [ExtraComponentKind: String] = [:]
    @Published var loaderKind: ModLoaderKind?
    @Published var loaderVersion = ""
    @Published var minecraftVersions: [RemoteVersion] = []
    @Published var loaderVersions: [ModLoaderKind: [ModLoaderVersion]] = [:]
    @Published var expandedLoader: ModLoaderKind?
    @Published var loading = false
    @Published var loadingLoader = false
    @Published var error: String?
    @Published var originalFabricAPI: FabricAPIState?
    @Published var selectedFabricAPI: CommunityVersion?
    @Published var keepOriginalFabricAPI = true
    @Published var fabricAPIVersions: [CommunityVersion] = []
    @Published var expandedFabricAPI = false
    @Published var loadingFabricAPI = false
    @Published var fabricAPIError: String?
    @Published var fabricAPIAutoSelection = false
    var fingerprint = ""
    var pendingLoaderVersion: String?
    var request = UUID()
    var loaderRequest = UUID()
    var task: Task<Void, Never>?
    var fabricAPIRequest = UUID()
    var fabricAPITask: Task<Void, Never>?

    var componentsChanged: Bool {
        minecraft != originalMinecraft || loaderKind != originalLoader?.kind || loaderVersion != (originalLoader?.version ?? "")
    }
    var fabricAPIChanged: Bool {
        guard let originalFabricAPI else { return false }
        if keepOriginalFabricAPI { return false }
        if let selectedFabricAPI { return !originalFabricAPI.matches(selectedFabricAPI) }
        return !originalFabricAPI.files.isEmpty
    }
    var fabricAPIDisplay: String {
        keepOriginalFabricAPI ? originalFabricAPI?.displayVersion ?? "无" : selectedFabricAPI?.versionNumber ?? "无"
    }
    var extraComponentsChanged: Bool {
        if selectedOptiFine != originalOptiFine || selectedLiteLoader != originalLiteLoader { return true }
        if keepOriginalOptiFabric { return false }
        if let selectedOptiFabric { return originalExtras?.matchesOptiFabric(selectedOptiFabric) != true }
        return originalOptiFabric != nil
    }
    var changed: Bool { newName != originalName || componentsChanged || fabricAPIChanged || extraComponentsChanged }
    func reset() {
        task?.cancel(); fabricAPITask?.cancel(); request = UUID(); loaderRequest = UUID(); fabricAPIRequest = UUID()
        extraCatalogue.reset()
        targetKey = ""; loading = false; loadingLoader = false; loadingFabricAPI = false; error = nil
    }
}

@MainActor extension LauncherStore {
    func prepareVersionModification(force: Bool = false) {
        guard let version = versionSettingsTarget, !isBusy, !isGameRunning else { return }
        let state = versionModifyState, directory = gameDirectory
        let isolate = profileSettings(for: version).isolateGameDirectory
        let key = modificationTargetKey(version)
        guard force || state.targetKey != key else { return }
        state.task?.cancel()
        let request = UUID(); state.request = request; state.targetKey = key
        state.error = nil; state.loading = true; state.expandedLoader = nil; state.loaderVersions = [:]
        state.loaderRequest = UUID(); state.loadingLoader = false; state.pendingLoaderVersion = nil
        state.originalName = ""; state.newName = ""; state.originalMinecraft = ""; state.minecraft = ""
        state.originalLoader = nil; state.loaderKind = nil; state.loaderVersion = ""; state.fingerprint = ""
        state.extraCatalogue.reset(); state.originalExtras = nil
        state.originalOptiFine = nil; state.selectedOptiFine = nil; state.originalLiteLoader = nil; state.selectedLiteLoader = nil
        state.originalOptiFabric = nil; state.selectedOptiFabric = nil; state.keepOriginalOptiFabric = true
        state.optiFabricAutoSelection = false; state.optiFabricExplicitNone = false
        state.rematchingExtras = []; state.pendingExtraVersions = [:]
        state.fabricAPITask?.cancel(); state.fabricAPIRequest = UUID()
        state.originalFabricAPI = nil; state.selectedFabricAPI = nil; state.keepOriginalFabricAPI = true
        state.fabricAPIVersions = []; state.expandedFabricAPI = false; state.loadingFabricAPI = false; state.fabricAPIError = nil
        state.fabricAPIAutoSelection = false
        do {
            let metadata = try MinecraftVersionMetadata.resolve(versionID: version.id, gameDirectory: gameDirectory)
            state.fingerprint = try VersionComponentManager.fingerprint(version: version, gameDirectory: gameDirectory)
            state.originalName = version.id; state.newName = version.id
            state.originalMinecraft = metadata["pclOriginalVersion"] as? String ?? remoteVersionID(for: version)
            state.minecraft = state.originalMinecraft
            state.originalLoader = MinecraftVersionMetadata.modLoader(in: metadata)
            state.loaderKind = state.originalLoader?.kind; state.loaderVersion = state.originalLoader?.version ?? ""
        } catch { state.loading = false; state.error = error.localizedDescription; return }
        state.minecraftVersions = remoteVersions
        state.task = Task {
            defer { if state.request == request { state.loading = false } }
            do {
                let installedAPI = try await Task.detached(priority: .userInitiated) {
                    try FabricAPIComponent.installed(gameDirectory: directory, version: version, isolate: isolate)
                }.value
                try Task.checkCancellation()
                guard state.request == request else { return }
                state.originalFabricAPI = installedAPI
                let extras = try await extraComponentsInstaller.inspect(version: version, gameDirectory: directory, isolate: isolate)
                try Task.checkCancellation()
                guard state.request == request else { return }
                state.originalExtras = extras
                state.originalOptiFine = extras.optiFine; state.selectedOptiFine = extras.optiFine
                state.originalLiteLoader = extras.liteLoader; state.selectedLiteLoader = extras.liteLoader
                state.originalOptiFabric = extras.optiFabric
                if state.minecraft != state.originalMinecraft {
                    prepareModificationExtraRematching(minecraft: state.minecraft)
                    startModificationExtraRematching()
                } else if state.loaderKind != state.originalLoader?.kind {
                    reconcileModificationExtraComponents()
                }
                if state.loaderKind == .fabric { loadModificationFabricAPI(autoSelect: !state.keepOriginalFabricAPI) }
                if state.minecraftVersions.isEmpty {
                    let manifest = try await installer.fetchManifest()
                    try Task.checkCancellation()
                    guard state.request == request else { return }
                    state.minecraftVersions = manifest.versions
                }
            } catch is CancellationError {} catch {
                guard state.request == request else { return }; state.error = error.localizedDescription
            }
        }
    }

    var versionModificationError: String? {
        let state = versionModifyState
        guard let version = versionSettingsTarget, state.originalName == version.id, state.targetKey == modificationTargetKey(version),
              state.originalFabricAPI != nil, state.originalExtras != nil else { return "正在读取版本信息" }
        do {
            try VersionManager.validateName(state.newName)
            if state.newName.caseInsensitiveCompare(version.id) != .orderedSame {
                let destination = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(state.newName)", under: gameDirectory)
                if FileManager.default.fileExists(atPath: destination.path) { return "已有同名版本，请使用其他名称。" }
            }
            if state.loaderKind != nil && state.loadingLoader { return "正在确认 Mod 加载器版本，请稍候。" }
            if state.loaderKind != nil && state.loaderVersion.isEmpty { return "请选择 Mod 加载器版本。" }
            if state.fabricAPIAutoSelection {
                if state.loadingFabricAPI { return "正在获取 Fabric API 版本，请稍候。" }
                if state.fabricAPIError != nil { return "Fabric API 版本获取失败，请重试，或明确选择不安装 Fabric API。" }
            }
            if !state.keepOriginalFabricAPI, let api = state.selectedFabricAPI {
                guard state.loaderKind == .fabric else { return "Fabric API 需要 Fabric 加载器。" }
                guard api.gameVersions.contains(state.minecraft), api.loaders.contains("fabric") else { return "所选 Fabric API 不兼容当前 Minecraft 版本。" }
            }
            for kind in state.rematchingExtras {
                if state.extraCatalogue.loading.contains(kind) { return "正在重新匹配 \(kind.title) 版本，请稍候。" }
                if state.extraCatalogue.errors[kind] != nil { return "\(kind.title) 获取失败，请重试，或明确选择不安装。" }
            }
            if state.optiFabricAutoSelection {
                if state.extraCatalogue.loading.contains(.optiFabric) { return "正在获取 OptiFabric 版本，请稍候。" }
                if state.extraCatalogue.errors[.optiFabric] != nil { return "OptiFabric 获取失败，请重试，或清除 OptiFine。" }
            }
            if state.componentsChanged || state.extraComponentsChanged,
               let error = ExtraComponentsInstaller.compatibilityError(minecraftVersion: state.minecraft, loader: modificationSelectedLoader, selection: modificationExtraSelection) { return error }
            if (state.componentsChanged || state.extraComponentsChanged) && !state.minecraftVersions.contains(where: { $0.id == state.minecraft }) { return "未获取到所选 Minecraft 版本，请刷新后重试。" }
            return nil
        } catch { return error.localizedDescription }
    }

    func chooseModificationMinecraft(_ id: String) {
        let state = versionModifyState
        guard state.minecraft != id else { return }
        prepareModificationExtraRematching(minecraft: id)
        state.minecraft = id; state.loaderVersions = [:]
        state.fabricAPITask?.cancel(); state.fabricAPIRequest = UUID(); state.loadingFabricAPI = false
        state.fabricAPIVersions = []; state.fabricAPIError = nil
        state.fabricAPIAutoSelection = false
        state.keepOriginalFabricAPI = (state.loaderKind != .fabric && state.originalLoader?.kind != .fabric)
            || (id == state.originalMinecraft && state.loaderKind == state.originalLoader?.kind)
        state.selectedFabricAPI = nil
        if state.loaderKind == .fabric { loadModificationFabricAPI(autoSelect: !state.keepOriginalFabricAPI) }
        state.loaderRequest = UUID(); state.loadingLoader = false
        if let kind = state.loaderKind {
            state.pendingLoaderVersion = state.loaderVersion
            state.loaderVersion = id == state.originalMinecraft && kind == state.originalLoader?.kind ? state.originalLoader?.version ?? "" : ""
            loadModificationLoader(kind)
        }
        startModificationExtraRematching()
    }

    func toggleModificationLoader(_ kind: ModLoaderKind) {
        let state = versionModifyState
        guard !isBusy, !isGameRunning, modificationLoaderConflict(kind) == nil else { return }
        state.extraCatalogue.expanded = nil
        if state.expandedLoader == kind { state.expandedLoader = nil }
        else { state.expandedLoader = kind; if state.loaderVersions[kind] == nil { loadModificationLoader(kind) } }
    }

    func loadModificationLoader(_ kind: ModLoaderKind) {
        let state = versionModifyState, minecraft = state.minecraft, request = UUID()
        state.loaderRequest = request; state.loadingLoader = true; state.error = nil
        Task {
            do {
                let entries = try await modLoaderInstaller.fetchVersions(kind: kind, minecraftVersion: minecraft)
                guard state.loaderRequest == request, state.minecraft == minecraft else { return }
                state.loaderVersions[kind] = entries; state.loadingLoader = false
                if state.loaderKind == kind {
                    if let pending = state.pendingLoaderVersion, entries.contains(where: { $0.version == pending }) { state.loaderVersion = pending }
                    state.pendingLoaderVersion = nil
                    if !entries.contains(where: { $0.version == state.loaderVersion }) && !(minecraft == state.originalMinecraft && kind == state.originalLoader?.kind) { state.loaderVersion = "" }
                }
                startModificationExtraRematching()
            } catch {
                guard state.loaderRequest == request else { return }
                state.loadingLoader = false; state.error = error.localizedDescription
            }
        }
    }

    func chooseModificationLoader(_ entry: ModLoaderVersion?) {
        let oldKind = versionModifyState.loaderKind
        versionModifyState.loaderKind = entry?.kind
        versionModifyState.loaderVersion = entry?.version ?? ""
        versionModifyState.expandedLoader = nil
        versionModifyState.pendingLoaderVersion = nil
        let state = versionModifyState
        if oldKind != state.loaderKind {
            state.fabricAPITask?.cancel(); state.fabricAPIRequest = UUID(); state.loadingFabricAPI = false
            state.fabricAPIAutoSelection = false
            state.fabricAPIError = nil
            state.selectedFabricAPI = nil
            state.keepOriginalFabricAPI = (state.loaderKind != .fabric && state.originalLoader?.kind != .fabric)
                || (state.minecraft == state.originalMinecraft && state.loaderKind == state.originalLoader?.kind)
            if state.loaderKind == .fabric { loadModificationFabricAPI(autoSelect: !state.keepOriginalFabricAPI) }
            else { state.expandedFabricAPI = false }
        }
        reconcileModificationExtraComponents()
        startModificationExtraRematching()
    }

    private func modificationTargetKey(_ version: InstalledVersion) -> String {
        gameDirectory.path + "/" + version.id + "|" + String(profileSettings(for: version).isolateGameDirectory)
    }

    func toggleModificationFabricAPI() {
        let state = versionModifyState
        guard !isBusy, !isGameRunning, state.loaderKind == .fabric, state.originalFabricAPI != nil else { return }
        state.expandedFabricAPI.toggle()
        if state.expandedFabricAPI && state.fabricAPIVersions.isEmpty && !state.loadingFabricAPI { loadModificationFabricAPI() }
    }

    func loadModificationFabricAPI(autoSelect: Bool = false) {
        let state = versionModifyState
        guard !isBusy, !isGameRunning, state.loaderKind == .fabric else { return }
        state.fabricAPITask?.cancel()
        if autoSelect { state.fabricAPIAutoSelection = true }
        let shouldAutoSelect = state.fabricAPIAutoSelection
        let request = UUID(), target = state.targetKey, minecraft = state.minecraft
        state.fabricAPIRequest = request; state.loadingFabricAPI = true; state.fabricAPIError = nil
        state.fabricAPITask = Task {
            do {
                let entries = try await fabricAPIComponent.versions(minecraftVersion: minecraft)
                try Task.checkCancellation()
                guard state.fabricAPIRequest == request, state.targetKey == target, state.minecraft == minecraft, state.loaderKind == .fabric else { return }
                state.fabricAPIVersions = entries; state.loadingFabricAPI = false
                if shouldAutoSelect && !state.keepOriginalFabricAPI && state.selectedFabricAPI == nil { state.selectedFabricAPI = entries.first }
                state.fabricAPIAutoSelection = false
            } catch is CancellationError {} catch {
                guard state.fabricAPIRequest == request, state.targetKey == target else { return }
                state.loadingFabricAPI = false; state.fabricAPIError = error.localizedDescription
            }
        }
    }

    func chooseModificationFabricAPI(_ entry: CommunityVersion?) {
        let state = versionModifyState
        guard !isBusy, !isGameRunning else { return }
        guard state.loaderKind == .fabric || entry == nil else { return }
        guard entry == nil || entry!.gameVersions.contains(state.minecraft) else { return }
        state.fabricAPITask?.cancel(); state.fabricAPIRequest = UUID(); state.loadingFabricAPI = false
        state.fabricAPIAutoSelection = false
        state.keepOriginalFabricAPI = false; state.selectedFabricAPI = entry; state.expandedFabricAPI = false
    }

    func restoreModificationFabricAPI() {
        let state = versionModifyState
        guard !isBusy, !isGameRunning, state.minecraft == state.originalMinecraft, state.loaderKind == state.originalLoader?.kind else { return }
        state.fabricAPITask?.cancel(); state.fabricAPIRequest = UUID(); state.loadingFabricAPI = false; state.fabricAPIError = nil
        state.keepOriginalFabricAPI = true; state.selectedFabricAPI = nil; state.expandedFabricAPI = false
        state.fabricAPIAutoSelection = false
    }

    func restoreModificationLoader(_ kind: ModLoaderKind) {
        let state = versionModifyState
        if state.originalLoader?.kind == kind {
            guard state.minecraft == state.originalMinecraft, let original = state.originalLoader else { return }
            chooseModificationLoader(original)
        } else if state.loaderKind == kind { chooseModificationLoader(nil) }
    }

    func restoreModificationComponents() {
        let state = versionModifyState
        state.newName = state.originalName; state.minecraft = state.originalMinecraft
        state.loaderKind = state.originalLoader?.kind; state.loaderVersion = state.originalLoader?.version ?? ""
        state.loaderVersions = [:]; state.expandedLoader = nil
        state.loaderRequest = UUID(); state.loadingLoader = false; state.pendingLoaderVersion = nil
        state.fabricAPITask?.cancel(); state.fabricAPIRequest = UUID(); state.loadingFabricAPI = false
        state.keepOriginalFabricAPI = true; state.selectedFabricAPI = nil; state.fabricAPIVersions = []
        state.expandedFabricAPI = false; state.fabricAPIError = nil
        state.fabricAPIAutoSelection = false
        state.extraCatalogue.reset(); state.rematchingExtras = []; state.pendingExtraVersions = [:]
        state.selectedOptiFine = state.originalOptiFine; state.selectedLiteLoader = state.originalLiteLoader
        state.selectedOptiFabric = nil; state.keepOriginalOptiFabric = true; state.optiFabricAutoSelection = false
        state.optiFabricExplicitNone = state.originalOptiFabric == nil
        if state.loaderKind == .fabric { loadModificationFabricAPI() }
    }

    var modificationSelectedLoader: ModLoaderVersion? {
        let state = versionModifyState
        guard let kind = state.loaderKind, !state.loaderVersion.isEmpty else { return nil }
        return ModLoaderVersion(kind: kind, minecraftVersion: state.minecraft, version: state.loaderVersion)
    }

    var modificationExtraSelection: ExtraComponentSelection {
        let state = versionModifyState
        return ExtraComponentSelection(optiFine: state.selectedOptiFine, liteLoader: state.selectedLiteLoader,
            optiFabric: state.selectedOptiFabric, keepOriginalOptiFabric: state.keepOriginalOptiFabric && state.originalOptiFabric != nil)
    }

    func modificationExtraSelection(_ kind: ExtraComponentKind) -> ExtraComponentVersion? {
        let state = versionModifyState
        switch kind {
        case .optiFine: return state.selectedOptiFine.map(ExtraComponentVersion.optiFine)
        case .liteLoader: return state.selectedLiteLoader.map(ExtraComponentVersion.liteLoader)
        case .optiFabric: return state.selectedOptiFabric.map(ExtraComponentVersion.optiFabric)
        }
    }

    func modificationExtraDisplay(_ kind: ExtraComponentKind, original: Bool = false) -> String {
        let state = versionModifyState
        switch kind {
        case .optiFine: return (original ? state.originalOptiFine : state.selectedOptiFine)?.displayName ?? "无"
        case .liteLoader: return (original ? state.originalLiteLoader : state.selectedLiteLoader)?.displayName ?? "无"
        case .optiFabric:
            if original || state.keepOriginalOptiFabric { return state.originalOptiFabric.map { ExtraComponentKind.optiFabricVersionLabel($0.version) } ?? "无" }
            return state.selectedOptiFabric.map { ExtraComponentKind.optiFabricVersionLabel($0.versionNumber) } ?? "无"
        }
    }

    func modificationExtraChanged(_ kind: ExtraComponentKind) -> Bool {
        let state = versionModifyState
        switch kind {
        case .optiFine: return state.selectedOptiFine != state.originalOptiFine
        case .liteLoader: return state.selectedLiteLoader != state.originalLiteLoader
        case .optiFabric:
            if state.keepOriginalOptiFabric { return false }
            if let selected = state.selectedOptiFabric { return state.originalExtras?.matchesOptiFabric(selected) != true }
            return state.originalOptiFabric != nil
        }
    }

    func modificationExtraIsSelected(_ entry: ExtraComponentVersion) -> Bool {
        if case .optiFabric(let version) = entry, versionModifyState.keepOriginalOptiFabric {
            return versionModifyState.originalExtras?.matchesOptiFabric(version) == true
        }
        return modificationExtraSelection(entry.kind) == entry
    }

    func modificationExtraVersions(_ kind: ExtraComponentKind) -> [ExtraComponentVersion] {
        versionModifyState.extraCatalogue.versions(kind).filter { entry in
            if case .optiFine(let version) = entry { return optiFineFitsForge(version, loader: modificationSelectedLoader) }
            return true
        }
    }

    func modificationExtraUnavailableReason(_ kind: ExtraComponentKind) -> String? {
        extraComponentUnavailableReason(kind, minecraft: versionModifyState.minecraft,
            loader: versionModifyState.loaderKind, optiFine: versionModifyState.selectedOptiFine)
    }

    func toggleModificationExtraCatalogue(_ kind: ExtraComponentKind) {
        let state = versionModifyState
        guard !isBusy, !isGameRunning, state.originalExtras != nil, modificationExtraUnavailableReason(kind) == nil else { return }
        state.expandedLoader = nil; state.expandedFabricAPI = false
        state.extraCatalogue.expanded = state.extraCatalogue.expanded == kind ? nil : kind
        if state.extraCatalogue.expanded == kind && !state.extraCatalogue.loaded.contains(kind) && !state.extraCatalogue.loading.contains(kind) { loadModificationExtraCatalogue(kind) }
    }

    func loadModificationExtraCatalogue(_ kind: ExtraComponentKind) {
        let state = versionModifyState
        guard !isBusy, !isGameRunning, !state.extraCatalogue.loading.contains(kind) else { return }
        let request = UUID(), minecraft = state.minecraft, target = state.targetKey
        state.extraCatalogue.requests[kind] = request
        state.extraCatalogue.loading.insert(kind); state.extraCatalogue.errors[kind] = nil
        state.extraCatalogue.tasks[kind] = Task {
            defer {
                if state.extraCatalogue.requests[kind] == request {
                    state.extraCatalogue.loading.remove(kind); state.extraCatalogue.tasks[kind] = nil
                }
            }
            do {
                let entries: [ExtraComponentVersion]
                switch kind {
                case .optiFine: entries = try await extraComponentsInstaller.fetchOptiFineVersions(minecraftVersion: minecraft).map(ExtraComponentVersion.optiFine)
                case .liteLoader: entries = try await extraComponentsInstaller.fetchLiteLoaderVersions(minecraftVersion: minecraft).map(ExtraComponentVersion.liteLoader)
                case .optiFabric: entries = try await extraComponentsInstaller.fetchOptiFabricVersions(minecraftVersion: minecraft).map(ExtraComponentVersion.optiFabric)
                }
                try Task.checkCancellation()
                guard state.extraCatalogue.requests[kind] == request, state.targetKey == target, state.minecraft == minecraft else { return }
                switch kind {
                case .optiFine: state.extraCatalogue.optiFine = entries.compactMap { if case .optiFine(let value) = $0 { return value }; return nil }
                case .liteLoader: state.extraCatalogue.liteLoader = entries.compactMap { if case .liteLoader(let value) = $0 { return value }; return nil }
                case .optiFabric: state.extraCatalogue.optiFabric = entries.compactMap { if case .optiFabric(let value) = $0 { return value }; return nil }
                }
                state.extraCatalogue.loaded.insert(kind)
                if state.rematchingExtras.contains(kind) {
                    let available = modificationExtraVersions(kind)
                    let pending = state.pendingExtraVersions[kind]
                    let selected = available.first { entry in
                        switch entry { case .optiFine(let value): return value.version == pending; case .liteLoader(let value): return value.version == pending; case .optiFabric(let value): return value.versionNumber == pending }
                    } ?? available.first
                    setModificationExtra(selected, kind: kind)
                    state.rematchingExtras.remove(kind); state.pendingExtraVersions[kind] = nil
                }
                if kind == .optiFabric, state.optiFabricAutoSelection,
                   state.loaderKind == .fabric, state.selectedOptiFine != nil {
                    state.keepOriginalOptiFabric = false; state.selectedOptiFabric = state.extraCatalogue.optiFabric.first
                    state.optiFabricAutoSelection = false
                }
                if kind != .optiFabric { reconcileModificationExtraComponents() }
            } catch is CancellationError {} catch {
                guard state.extraCatalogue.requests[kind] == request, state.targetKey == target, state.minecraft == minecraft else { return }
                state.extraCatalogue.errors[kind] = error.localizedDescription
            }
        }
    }

    func chooseModificationExtra(_ entry: ExtraComponentVersion?, kind: ExtraComponentKind) {
        let state = versionModifyState
        guard !isBusy, !isGameRunning, state.originalExtras != nil else { return }
        if let entry { guard entry.kind == kind, modificationExtraVersions(kind).contains(entry) else { return } }
        state.extraCatalogue.tasks[kind]?.cancel(); state.extraCatalogue.requests[kind] = UUID()
        state.extraCatalogue.loading.remove(kind); state.extraCatalogue.errors[kind] = nil
        state.rematchingExtras.remove(kind); state.pendingExtraVersions[kind] = nil
        setModificationExtra(entry, kind: kind)
        if kind == .optiFabric { state.optiFabricAutoSelection = false; state.optiFabricExplicitNone = entry == nil }
        state.extraCatalogue.expanded = nil
        reconcileModificationExtraComponents()
    }

    private func setModificationExtra(_ entry: ExtraComponentVersion?, kind: ExtraComponentKind) {
        let state = versionModifyState
        switch kind {
        case .optiFine: if case .optiFine(let value)? = entry { state.selectedOptiFine = value } else { state.selectedOptiFine = nil }
        case .liteLoader: if case .liteLoader(let value)? = entry { state.selectedLiteLoader = value } else { state.selectedLiteLoader = nil }
        case .optiFabric:
            if case .optiFabric(let value)? = entry { state.selectedOptiFabric = value } else { state.selectedOptiFabric = nil }
            state.keepOriginalOptiFabric = false
        }
    }

    func restoreModificationExtra(_ kind: ExtraComponentKind) {
        let state = versionModifyState
        guard !isBusy, !isGameRunning, state.minecraft == state.originalMinecraft,
              (modificationExtraUnavailableReason(kind) == nil || modificationExtraDisplay(kind, original: true) == "无") else { return }
        state.extraCatalogue.tasks[kind]?.cancel(); state.extraCatalogue.requests[kind] = UUID()
        state.extraCatalogue.loading.remove(kind); state.extraCatalogue.errors[kind] = nil
        state.rematchingExtras.remove(kind); state.pendingExtraVersions[kind] = nil
        switch kind {
        case .optiFine: state.selectedOptiFine = state.originalOptiFine
        case .liteLoader: state.selectedLiteLoader = state.originalLiteLoader
        case .optiFabric:
            state.keepOriginalOptiFabric = true; state.selectedOptiFabric = nil; state.optiFabricAutoSelection = false
            state.optiFabricExplicitNone = state.originalOptiFabric == nil
        }
        state.extraCatalogue.expanded = nil
        reconcileModificationExtraComponents()
    }

    private func prepareModificationExtraRematching(minecraft: String) {
        let state = versionModifyState
        let oldOptiFine = state.selectedOptiFine?.version ?? state.pendingExtraVersions[.optiFine]
        let oldLiteLoader = state.selectedLiteLoader?.version ?? state.pendingExtraVersions[.liteLoader]
        state.extraCatalogue.reset(); state.rematchingExtras = []; state.pendingExtraVersions = [:]
        state.optiFabricExplicitNone = false
        if minecraft == state.originalMinecraft {
            state.selectedOptiFine = state.originalOptiFine; state.selectedLiteLoader = state.originalLiteLoader
            state.keepOriginalOptiFabric = true; state.selectedOptiFabric = nil; state.optiFabricAutoSelection = false
        } else {
            state.selectedOptiFine = nil; state.selectedLiteLoader = nil; state.selectedOptiFabric = nil
            state.keepOriginalOptiFabric = state.loaderKind != .fabric && state.originalLoader?.kind != .fabric
            state.optiFabricAutoSelection = state.loaderKind == .fabric && oldOptiFine != nil
            if let oldOptiFine { state.pendingExtraVersions[.optiFine] = oldOptiFine; state.rematchingExtras.insert(.optiFine) }
            if let oldLiteLoader { state.pendingExtraVersions[.liteLoader] = oldLiteLoader; state.rematchingExtras.insert(.liteLoader) }
        }
    }

    private func startModificationExtraRematching() {
        let state = versionModifyState
        for kind in state.rematchingExtras where kind != .optiFabric {
            if kind == .optiFine && state.loaderKind != nil && (state.loadingLoader || state.loaderVersion.isEmpty) { continue }
            loadModificationExtraCatalogue(kind)
        }
        if state.rematchingExtras.isEmpty { reconcileModificationExtraComponents() }
    }

    private func reconcileModificationExtraComponents() {
        let state = versionModifyState
        if let optiFine = state.selectedOptiFine,
           modificationExtraUnavailableReason(.optiFine) != nil || !optiFineFitsForge(optiFine, loader: modificationSelectedLoader) { state.selectedOptiFine = nil }
        if modificationExtraUnavailableReason(.liteLoader) != nil { state.selectedLiteLoader = nil }
        for kind in [ExtraComponentKind.optiFine, .liteLoader] where modificationExtraUnavailableReason(kind) != nil {
            state.extraCatalogue.tasks[kind]?.cancel(); state.extraCatalogue.requests[kind] = UUID()
            state.extraCatalogue.loading.remove(kind); state.extraCatalogue.errors[kind] = nil
            state.rematchingExtras.remove(kind); state.pendingExtraVersions[kind] = nil
        }
        if state.loaderKind == .fabric && state.selectedOptiFine != nil {
            if state.keepOriginalOptiFabric && state.originalOptiFabric != nil && state.minecraft == state.originalMinecraft { return }
            if !state.optiFabricExplicitNone && state.selectedOptiFabric == nil && !state.extraCatalogue.loaded.contains(.optiFabric) { state.optiFabricAutoSelection = true }
            if state.optiFabricAutoSelection {
                if state.extraCatalogue.loaded.contains(.optiFabric) {
                    state.keepOriginalOptiFabric = false; state.selectedOptiFabric = state.extraCatalogue.optiFabric.first
                    state.optiFabricAutoSelection = false
                } else if !state.extraCatalogue.loading.contains(.optiFabric) { loadModificationExtraCatalogue(.optiFabric) }
            }
        } else if !state.rematchingExtras.contains(.optiFine) {
            state.extraCatalogue.tasks[.optiFabric]?.cancel(); state.extraCatalogue.requests[.optiFabric] = UUID()
            state.extraCatalogue.loading.remove(.optiFabric); state.extraCatalogue.errors[.optiFabric] = nil
            state.selectedOptiFabric = nil; state.optiFabricAutoSelection = false; state.optiFabricExplicitNone = false
            if state.originalLoader?.kind == .fabric || state.loaderKind == .fabric { state.keepOriginalOptiFabric = false }
        }
    }

    func modificationLoaderConflict(_ kind: ModLoaderKind) -> String? {
        let state = versionModifyState
        if state.selectedLiteLoader != nil && kind != .forge { return "与 LiteLoader 不兼容" }
        if let optiFine = state.selectedOptiFine { return extraComponentUnavailableReason(.optiFine, minecraft: state.minecraft, loader: kind, optiFine: optiFine) }
        return nil
    }

    func modificationLoaderVersions(_ kind: ModLoaderKind) -> [ModLoaderVersion] {
        (versionModifyState.loaderVersions[kind] ?? []).filter { value in
            guard let optiFine = versionModifyState.selectedOptiFine else { return true }
            return optiFineFitsForge(optiFine, loader: value)
        }
    }

    func startVersionModification() {
        guard !isBusy, !isGameRunning, let version = versionSettingsTarget else { return }
        let state = versionModifyState
        guard state.changed else { return }
        if let error = versionModificationError { state.error = error; return }
        let directory = gameDirectory, finalName = state.newName, minecraft = state.minecraft
        let kind = state.loaderKind, loaderVersion = state.loaderVersion, fingerprint = state.fingerprint
        let changedComponents = state.componentsChanged
        let changedFabricAPI = state.fabricAPIChanged, selectedAPI = state.selectedFabricAPI
        guard let originalAPI = state.originalFabricAPI, let originalExtras = state.originalExtras else { return }
        let extraSelection = modificationExtraSelection
        let changedExtras = state.extraComponentsChanged
        let rebuildExtraLaunch = state.selectedOptiFine != state.originalOptiFine || state.selectedLiteLoader != state.originalLiteLoader
        let useExtraWorkflow = changedExtras || (changedComponents && (originalExtras.optiFine != nil || originalExtras.liteLoader != nil || (state.originalLoader?.kind == .fabric && originalExtras.optiFabric != nil) || extraSelection.optiFine != nil || extraSelection.liteLoader != nil || extraSelection.optiFabric != nil))
        let selectedMainLoader = modificationSelectedLoader
        let keepOriginalAPI = state.keepOriginalFabricAPI
        let isolate = profileSettings(for: version).isolateGameDirectory
        let reuseProfiles = installedVersions.map { ($0, profileSettings(for: $0).isolateGameDirectory) }
        let wasSelected = preferences.selectedVersion == version.id
        if changedComponents || changedFabricAPI || changedExtras {
            var warnings = changedComponents || rebuildExtraLaunch
                ? ["如果该版本经过第三方修改，这些改动可能会丢失。", "现有的 Mod、资源包等在修改后可能会不兼容！"]
                : ["将替换或移除选中的组件文件，其他 Mod 和存档保留。"]
            if !isolate && (kind != nil || changedFabricAPI || changedExtras) {
                warnings.insert("该版本尚未开启版本隔离，可能与别的 MC 版本共用同一个 Mod 和存档文件夹。", at: 0)
            }
            let alert = NSAlert(); alert.messageText = "修改确认"
            alert.informativeText = "请注意：\n• " + warnings.joined(separator: "\n• ")
            alert.addButton(withTitle: "开始修改"); alert.addButton(withTitle: "取消")
            guard alert.runModal() == .alertFirstButtonReturn else { return }
        }
        guard directory == gameDirectory, versionSettingsTargetID == version.id else { return }
        state.task?.cancel(); state.request = UUID(); state.loading = false
        let remote = state.minecraftVersions.first { $0.id == minecraft }
        if useExtraWorkflow {
            runModdedInstallation(profileID: finalName, directory: directory, summary: "修改版本组件", selectVersionOnSuccess: wasSelected,
                successPage: .versionSettings, successMessage: "版本修改完成", checkCancellationAfterOperation: false) { [self] progress in
                guard let remote else { throw MinecraftInstallError.invalidMetadata("未找到 Minecraft 元数据") }
                let java = try await installationJava(minecraftVersion: minecraft, metadataURL: remote.url)
                try await extraComponentsInstaller.modify(version: version, minecraft: remote, minecraftID: minecraft,
                    loader: selectedMainLoader, selection: extraSelection, fabricAPI: selectedAPI, keepOriginalFabricAPI: keepOriginalAPI,
                    originalFabricAPI: originalAPI, originalExtras: originalExtras, newName: finalName, isolate: isolate,
                    expectedFingerprint: fingerprint, gameDirectory: directory, javaExecutable: java.executable, progress: progress)
                if directory == gameDirectory { versionSettingsTargetID = finalName; versionModifyState.targetKey = "" }
            }
            return
        }
        let temporaryID = "PCL-Modify-" + UUID().uuidString
        runModdedInstallation(profileID: finalName, directory: directory, summary: "修改版本组件", selectVersionOnSuccess: wasSelected,
                              successPage: .versionSettings, successMessage: "版本修改完成", checkCancellationAfterOperation: false) { [self] progress in
            let publication = VersionModificationProgress(title: "修改版本组件： " + finalName,
                includesFabricAPI: changedFabricAPI && selectedAPI != nil, progress: progress)
            let temporary = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(temporaryID)", under: directory)
            var ownsTemporary = false
            defer { if ownsTemporary { try? FileManager.default.removeItem(at: temporary) } }
            if changedComponents {
                guard let remote else { throw MinecraftInstallError.invalidMetadata("未找到 Minecraft 元数据") }
                guard !FileManager.default.fileExists(atPath: temporary.path) else { throw VersionManagementError.destinationExists }
                ownsTemporary = true
                if let kind {
                    let java = try await installationJava(minecraftVersion: minecraft, metadataURL: remote.url)
                    try await modLoaderInstaller.install(kind: kind, version: ModLoaderVersion(kind: kind, minecraftVersion: minecraft, version: loaderVersion),
                        minecraftVersion: remote, gameDirectory: directory, profileID: temporaryID, javaExecutable: java.executable, progress: { publication.preparation($0) })
                } else { try await installer.install(version: remote, gameDirectory: directory, profileID: temporaryID, progress: { publication.preparation($0) }) }
            }
            let preparedAPI: URL?
            if changedFabricAPI, let selectedAPI {
                publication.beginFabricAPI(afterComponents: changedComponents)
                let reuseFiles = await Task.detached(priority: .utility) {
                    reuseProfiles.flatMap { candidate, isolated in
                        (try? FabricAPIComponent.installed(gameDirectory: directory, version: candidate, isolate: isolated).files.map(\.url)) ?? []
                    }
                }.value
                preparedAPI = try await fabricAPIComponent.prepare(version: selectedAPI, gameDirectory: directory,
                    reuseFiles: originalAPI.files.map(\.url) + reuseFiles, progress: { publication.preparation($0, phase: 1) })
            } else { preparedAPI = nil }
            try Task.checkCancellation()
            guard try VersionComponentManager.fingerprint(version: version, gameDirectory: directory) == fingerprint else {
                throw MinecraftInstallError.invalidMetadata("原版本配置已改变，请刷新后重试")
            }
            guard try VersionSettingsRepository.load(version: version).isolateGameDirectory == isolate else {
                throw MinecraftInstallError.invalidMetadata("版本隔离设置已改变，请刷新后重试")
            }
            var modTransaction: FabricAPITransaction?
            do {
                if changedFabricAPI {
                    modTransaction = try FabricAPIComponent.apply(preparedFile: preparedAPI, gameDirectory: directory,
                        version: version, isolate: isolate, expectedState: originalAPI)
                }
                try Task.checkCancellation()
                publication.commit()
                if changedComponents {
                    _ = try VersionComponentManager.applyPreparedProfile(version: version, preparedID: temporaryID, gameDirectory: directory,
                        newName: finalName, expectedFingerprint: fingerprint)
                } else if finalName != version.id {
                    _ = try VersionManager.rename(version: version, to: finalName, gameDirectory: directory)
                }
                modTransaction?.finish()
            } catch {
                try modTransaction?.rollback()
                throw error
            }
            if directory == gameDirectory { versionSettingsTargetID = finalName; versionModifyState.targetKey = "" }
            publication.finish()
        }
    }
}

/// Keep download callbacks below 90% until the atomic component commit succeeds.
private final class VersionModificationProgress: @unchecked Sendable {
    private let lock = NSLock()
    private var phase = 0
    private var revision: Int64 = 0
    private var sourceRevision: Int64 = -1
    private var fraction = 0.0
    private var preparationStart = 0.0
    private var preparationWeight: Double
    private var preparationPhase = 0
    private let title: String
    private let progress: @Sendable (InstallProgress) -> Void
    init(title: String, includesFabricAPI: Bool, progress: @escaping @Sendable (InstallProgress) -> Void) {
        self.title = title; self.progress = progress
        preparationWeight = includesFabricAPI ? 0.65 : 0.9
    }

    func beginFabricAPI(afterComponents: Bool) {
        lock.lock(); defer { lock.unlock() }
        sourceRevision = -1
        preparationPhase = 1
        preparationStart = afterComponents ? 0.65 : 0
        preparationWeight = 0.9 - preparationStart
    }

    func preparation(_ value: InstallProgress, phase: Int = 0) {
        lock.lock()
        guard self.phase == 0, phase == preparationPhase else { lock.unlock(); return }
        if let incoming = value.snapshot {
            guard incoming.revision >= sourceRevision else { lock.unlock(); return }
            sourceRevision = incoming.revision
        }
        fraction = max(fraction, min(0.9, preparationStart + (value.snapshot?.totalFraction ?? value.fraction) * preparationWeight)); revision += 1
        let stages = (value.snapshot?.stages ?? [.init(id: "prepare", title: "准备版本组件", state: .running, fraction: fraction / 0.9)]) + [.init(id: "modifyCommit", title: "应用版本组件")]
        let snapshot = InstallTaskSnapshot(title: title, stages: stages, totalFraction: fraction,
            bytesPerSecond: value.snapshot?.bytesPerSecond ?? 0, remainingFiles: value.snapshot?.remainingFiles ?? 0,
            downloadedBytes: value.snapshot?.downloadedBytes ?? 0, revision: revision)
        let next = InstallProgress(message: value.message, completed: Int(fraction * 10000), total: 10000, snapshot: snapshot)
        lock.unlock(); progress(next)
    }
    func commit() { publish(phase: 1, fraction: 0.95, message: "应用版本组件") }
    func finish() { publish(phase: 2, fraction: 1, message: "版本修改完成") }
    private func publish(phase nextPhase: Int, fraction nextFraction: Double, message: String) {
        lock.lock(); phase = nextPhase; fraction = nextFraction; revision += 1
        let snapshot = InstallTaskSnapshot(title: title, stages: [
            .init(id: "prepare", title: "准备版本组件", state: .completed),
            .init(id: "modifyCommit", title: "应用版本组件", state: nextPhase == 2 ? .completed : .running, fraction: nextPhase == 2 ? 1 : 0.5)
        ], totalFraction: fraction, revision: revision)
        let next = InstallProgress(message: message, completed: Int(fraction * 10000), total: 10000, snapshot: snapshot)
        lock.unlock(); progress(next)
    }
}
