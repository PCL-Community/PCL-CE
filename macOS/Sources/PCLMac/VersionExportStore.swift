import AppKit
import Combine
import Foundation
import PCLCore
import UniformTypeIdentifiers

@MainActor final class VersionExportState: ObservableObject {
    @Published var options = VersionPackExportOptions()
    @Published var isExporting = false
    @Published var progress: VersionPackExportProgress?
    @Published var result: VersionPackExportResult?
    @Published var error: String?
    @Published var resourcePacks: [VersionExportSelectableEntry] = []
    @Published var shaderPacks: [VersionExportSelectableEntry] = []
    @Published var saves: [VersionExportSelectableEntry] = []
    var versionID: String?
    var versionPath: String?
    var task: Task<Void, Never>?
    var operationID = UUID()
    let exporter = VersionPackExporter()
}

typealias VersionExportSelectableEntry = VersionPackExportItem

private final class VersionExportProgressRelay: @unchecked Sendable {
    private let lock = NSLock()
    private var lastUpdate = 0.0
    func accepts(_ value: VersionPackExportProgress) -> Bool {
        lock.lock(); defer { lock.unlock() }
        let now = ProcessInfo.processInfo.systemUptime
        guard value.fraction >= 1 || now - lastUpdate >= 0.1 else { return false }
        lastUpdate = now
        return true
    }
}

@MainActor extension LauncherStore {
    func refreshVersionExport() {
        let state = versionExportState
        guard !state.isExporting, let version = versionSettingsTarget else { return }
        if state.versionID != version.id || state.versionPath != version.directory.path {
            state.versionID = version.id
            state.versionPath = version.directory.path
            state.options = VersionPackExportOptions()
            state.options.name = version.id
            state.result = nil; state.progress = nil; state.error = nil
        }
        do {
            state.error = nil
            let profile = try VersionSettingsRepository.load(version: version)
            let runtime = profile.isolateGameDirectory ? version.directory : gameDirectory
            state.resourcePacks = try VersionPackExporter.selectableEntries(in: runtime, folders: ["resourcepacks", "texturepacks"])
            state.shaderPacks = try VersionPackExporter.selectableEntries(in: runtime, folders: ["shaderpacks"])
            state.saves = try VersionPackExporter.selectableEntries(in: runtime, folders: ["saves"], directoriesOnly: true)
        } catch { state.error = error.localizedDescription }
    }

    func startVersionPackExport() {
        guard !isBusy, !isGameRunning, let version = versionSettingsTarget else { return }
        let state = versionExportState
        saveVersionSettings()
        guard profileSaveError == nil, versionWindowInputError == nil else {
            state.error = profileSaveError ?? versionWindowInputError
            return
        }
        let options = state.options
        do { try VersionPackExporter.validate(options) } catch { state.error = error.localizedDescription; return }
        let panel = NSSavePanel()
        panel.title = "选择导出位置"
        panel.allowedContentTypes = [UTType(filenameExtension: options.format.fileExtension) ?? .data]
        panel.canCreateDirectories = true
        panel.isExtensionHidden = false
        let base = (options.name.isEmpty ? version.id : options.name)
            .replacingOccurrences(of: "/", with: "-").replacingOccurrences(of: ":", with: "-")
        panel.nameFieldStringValue = base + (options.version.isEmpty ? "" : " " + options.version) + "." + options.format.fileExtension
        guard panel.runModal() == .OK, let destination = panel.url else { return }
        let replace = FileManager.default.fileExists(atPath: destination.path)
        let directory = gameDirectory, operation = UUID()
        let progressRelay = VersionExportProgressRelay()
        state.operationID = operation; state.isExporting = true; state.progress = nil; state.result = nil; state.error = nil
        state.task = Task { [weak self] in
            defer {
                if state.operationID == operation { state.isExporting = false; state.task = nil }
            }
            do {
                let result = try await state.exporter.export(version: version, gameDirectory: directory, options: options,
                    to: destination, replaceExisting: replace) { value in
                        guard progressRelay.accepts(value) else { return }
                        Task { @MainActor in
                            guard state.operationID == operation, state.isExporting else { return }
                            if value.completedBytes >= (state.progress?.completedBytes ?? 0) { state.progress = value }
                        }
                    }
                guard state.operationID == operation else { return }
                state.result = result
                self?.statusMessage = "整合包已导出：\(result.url.lastPathComponent)"
                self?.appendLog("整合包已导出：\(result.url.path)，共 \(result.fileCount) 个文件。")
            } catch is CancellationError {
                if state.operationID == operation { state.error = "已取消导出，未完成的临时文件已清理。" }
            } catch {
                if state.operationID == operation { state.error = error.localizedDescription }
            }
        }
    }

    func cancelVersionExport() { versionExportState.task?.cancel() }

    func saveVersionExportConfiguration() {
        guard !versionExportState.isExporting else { return }
        let panel = NSSavePanel()
        panel.title = "保存导出配置"; panel.allowedContentTypes = [.json]
        panel.nameFieldStringValue = "整合包导出配置.json"
        guard panel.runModal() == .OK, let destination = panel.url else { return }
        do {
            let encoder = JSONEncoder(); encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
            try encoder.encode(versionExportState.options).write(to: destination, options: .atomic)
        } catch { versionExportState.error = error.localizedDescription }
    }

    func readVersionExportConfiguration() {
        guard !versionExportState.isExporting else { return }
        let panel = NSOpenPanel()
        panel.title = "读取导出配置"; panel.allowedContentTypes = [.json]
        panel.canChooseDirectories = false; panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            let data = try Data(contentsOf: url)
            guard data.count <= 64 * 1024 else { throw VersionPackExportError.invalidOptions("配置文件过大") }
            let options = try JSONDecoder().decode(VersionPackExportOptions.self, from: data)
            try VersionPackExporter.validate(options)
            versionExportState.options = options
            versionExportState.error = nil
        } catch { versionExportState.error = error.localizedDescription }
    }

    func resetVersionExportOptions() {
        guard !versionExportState.isExporting else { return }
        let name = versionExportState.options.name, version = versionExportState.options.version
        versionExportState.options = VersionPackExportOptions()
        versionExportState.options.name = name; versionExportState.options.version = version
    }

    func revealVersionPackExport() {
        guard let result = versionExportState.result else { return }
        NSWorkspace.shared.activateFileViewerSelecting([result.url])
    }
}
