import AppKit
import Combine
import PCLCore
import UniformTypeIdentifiers

@MainActor final class VersionModState: ObservableObject {
    enum Filter: String, CaseIterable { case all = "全部", enabled = "启用", disabled = "禁用" }
    @Published var entries: [LocalModFile] = []
    @Published var search = ""
    @Published var filter = Filter.all
    @Published var selected = Set<String>()
    @Published var loading = false
    @Published var mutating = false
    @Published var error: String?
    var request = UUID()
    var folder: URL?
    var targetKey = ""
    var visible: [LocalModFile] {
        entries.filter { file in
            (filter == .all || file.enabled == (filter == .enabled)) &&
            (search.isEmpty || [file.name, file.filename, file.version, file.modID, file.summary].contains { $0.localizedCaseInsensitiveContains(search) })
        }
    }
    var selectedEntries: [LocalModFile] { entries.filter { selected.contains($0.id) } }
}

@MainActor extension LauncherStore {
    var versionModTargetKey: String {
        guard let version = versionSettingsTarget else { return "" }
        return gameDirectory.path + "/" + version.id + ":" + String(profileSettings(for: version).isolateGameDirectory)
    }

    func refreshVersionMods(clearSearch: Bool = false) {
        let state = versionModState
        guard !isBusy else { return }
        guard !state.loading || state.targetKey != versionModTargetKey || clearSearch else { return }
        guard let version = versionSettingsTarget else { state.entries = []; state.folder = nil; return }
        do {
            let folder = try LocalModManager.directory(gameDirectory: gameDirectory, version: version, isolate: profileSettings(for: version).isolateGameDirectory)
            let request = UUID(); state.request = request
            if state.targetKey != versionModTargetKey || clearSearch {
                state.search = ""; state.filter = .all; state.selected = []
            }
            state.targetKey = versionModTargetKey; state.folder = folder; state.loading = true; state.error = nil
            Task {
                let result = await Task.detached(priority: .userInitiated) { Result { try LocalModManager.scan(directory: folder) } }.value
                guard state.request == request, state.targetKey == versionModTargetKey else { return }
                state.loading = false
                switch result {
                case .success(let entries):
                    state.entries = entries; state.selected.formIntersection(Set(entries.map(\.id)))
                case .failure(let error): state.entries = []; state.error = error.localizedDescription
                }
            }
        } catch { state.loading = false; state.error = error.localizedDescription }
    }

    func toggleVersionModSelection(_ file: LocalModFile) {
        if versionModState.selected.contains(file.id) { versionModState.selected.remove(file.id) }
        else { versionModState.selected.insert(file.id) }
    }

    func selectAllVersionMods() {
        let ids = Set(versionModState.visible.map(\.id))
        if ids.isSubset(of: versionModState.selected) { versionModState.selected.subtract(ids) }
        else { versionModState.selected.formUnion(ids) }
    }

    func setVersionModsEnabled(_ enabled: Bool, files: [LocalModFile]? = nil) {
        let state = versionModState
        guard !isBusy, !isGameRunning, let folder = state.folder else { return }
        let entries = files ?? state.selectedEntries
        guard !entries.isEmpty else { return }
        let targetKey = versionModTargetKey
        state.mutating = true; state.error = nil
        Task {
            let result = await Task.detached(priority: .userInitiated) { Result { try LocalModManager.setEnabled(enabled, files: entries, directory: folder) } }.value
            state.mutating = false
            guard state.targetKey == targetKey, versionModTargetKey == targetKey else { return }
            state.selected = []; refreshVersionMods()
            if case .failure(let error) = result { state.error = error.localizedDescription }
            else { statusMessage = enabled ? "Mod 已启用" : "Mod 已禁用" }
        }
    }

    func importVersionMods() {
        guard !isBusy, !isGameRunning, let version = versionSettingsTarget else { return }
        do {
            let folder = try LocalModManager.directory(gameDirectory: gameDirectory, version: version, isolate: profileSettings(for: version).isolateGameDirectory)
            let targetKey = versionModTargetKey
            let panel = NSOpenPanel(); panel.canChooseFiles = true; panel.canChooseDirectories = false; panel.allowsMultipleSelection = true
            panel.allowedContentTypes = [UTType("com.sun.java-archive") ?? UTType(filenameExtension: "jar") ?? .data]; panel.message = "选择要安装的 Mod 文件"; panel.prompt = "安装"
            guard panel.runModal() == .OK, !isBusy, !isGameRunning, versionModTargetKey == targetKey else { return }
            let sources = panel.urls; let state = versionModState; state.mutating = true; state.error = nil
            Task {
                let result = await Task.detached(priority: .userInitiated) { Result { try LocalModManager.importFiles(sources, directory: folder) } }.value
                state.mutating = false
                guard state.targetKey == targetKey, versionModTargetKey == targetKey else { return }
                refreshVersionMods()
                switch result {
                case .success(let count): statusMessage = "已安装 \(count) 个 Mod"
                case .failure(let error): state.error = error.localizedDescription
                }
            }
        } catch { versionModState.error = error.localizedDescription }
    }

    func deleteVersionMods(_ files: [LocalModFile]? = nil) {
        let state = versionModState
        guard !isBusy, !isGameRunning, let folder = state.folder else { return }
        let entries = files ?? state.selectedEntries
        guard !entries.isEmpty else { return }
        let targetKey = versionModTargetKey
        do {
            _ = try LocalModManager.validatedURLs(entries, directory: folder)
            let alert = NSAlert(); alert.messageText = "Mod 删除确认"
            alert.informativeText = "你确定要删除这 \(entries.count) 个 Mod 吗？\n" + entries.map { "· " + $0.filename }.joined(separator: "\n")
            alert.addButton(withTitle: "删除"); alert.addButton(withTitle: "取消")
            guard alert.runModal() == .alertFirstButtonReturn, !isBusy, !isGameRunning, versionModTargetKey == targetKey else { return }
            let urls = try LocalModManager.validatedURLs(entries, directory: folder)
            state.mutating = true
            NSWorkspace.shared.recycle(urls) { _, error in
                Task { @MainActor in
                    state.mutating = false
                    guard state.targetKey == targetKey, self.versionModTargetKey == targetKey else { return }
                    if let error { self.report(error) }
                    else { self.statusMessage = "已删除 \(entries.count) 个 Mod" }
                    state.selected = []; self.refreshVersionMods()
                }
            }
        } catch { state.error = error.localizedDescription }
    }

    func revealVersionModFolder() {
        guard let version = versionSettingsTarget else { return }
        do {
            let folder = try LocalModManager.directory(gameDirectory: gameDirectory, version: version, isolate: profileSettings(for: version).isolateGameDirectory, create: true)
            NSWorkspace.shared.open(folder)
        } catch { versionModState.error = error.localizedDescription }
    }

    func showVersionModDetails(_ file: LocalModFile) {
        let alert = NSAlert(); alert.messageText = file.name
        alert.informativeText = [file.version.isEmpty ? nil : "版本：\(file.version)", file.modID.isEmpty ? nil : "Mod ID：\(file.modID)",
            file.summary.isEmpty ? nil : file.summary, "文件：\(file.filename)", file.enabled ? "状态：已启用" : "状态：已禁用", file.error].compactMap { $0 }.joined(separator: "\n\n")
        alert.addButton(withTitle: "确定"); alert.runModal()
    }

    func downloadVersionMods() {
        openCommunity(.mod)
        if let version = versionSettingsTarget {
            communityState.minecraftVersion = remoteVersionID(for: version)
            communityState.loader = version.modLoader?.kind.rawValue ?? ""
            communityState.targetVersionID = version.id
            searchCommunity(reset: true)
        }
    }
}
