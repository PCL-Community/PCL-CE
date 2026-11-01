import AppKit
import Foundation
import PCLCore
import UniformTypeIdentifiers

struct CommunityBrowserState {
    var kind: CommunityResourceKind?
    var queryText = ""
    var minecraftVersion = ""
    var loader = ""
    var category = ""
    var source: CommunitySource = .all
    var sort: CommunitySort = .relevance
    var projects: [CommunityProject] = []
    var total = 0
    var hasMore = false
    var searching = false
    var loadingMore = false
    var error: String?
    var warnings: [String] = []
    var selectedProject: CommunityProject?
    var detail: CommunityProjectDetail?
    var versions: [CommunityVersion] = []
    var loadingDetail = false
    var loadingVersions = false
    var detailError: String?
    var versionFilter = ""
    var loaderFilter = ""
    var showDownloadSheet = false
    var selectedVersion: CommunityVersion?
    var targetVersionID = ""
    var targetWorldPath = ""
    var includeDependencies = true
    var downloadName = ""
    var downloadError: String?
    var nextOffset = 0
    var searchRequest = UUID()
    var detailRequest = UUID()
    var versionsRequest = UUID()
    var searchTask: Task<Void, Never>?
    var detailTask: Task<Void, Never>?
    var versionsTask: Task<Void, Never>?
    var lastQuery: CommunitySearchQuery?
}

/// Downloading an archive and importing it use different monitors. Give the
/// combined operation one increasing revision, rejecting callbacks from an old
/// phase as soon as the next phase begins.
private final class CommunityProgressSequence: @unchecked Sendable {
    private let lock = NSLock()
    private var phase = 0
    private var phaseRevision: Int64 = -1
    private var revision: Int64 = 0
    private let progress: @Sendable (InstallProgress) -> Void
    init(progress: @escaping @Sendable (InstallProgress) -> Void) { self.progress = progress }
    func begin(_ phase: Int) {
        lock.lock(); defer { lock.unlock() }
        self.phase = phase; phaseRevision = -1
    }
    func publish(_ value: InstallProgress, phase: Int) {
        lock.lock()
        guard phase == self.phase else { lock.unlock(); return }
        if let snapshot = value.snapshot, snapshot.revision < phaseRevision { lock.unlock(); return }
        if let snapshot = value.snapshot { phaseRevision = snapshot.revision }
        revision += 1
        let snapshot = value.snapshot.map {
            InstallTaskSnapshot(title: $0.title, stages: $0.stages, totalFraction: $0.totalFraction,
                bytesPerSecond: $0.bytesPerSecond, remainingFiles: $0.remainingFiles,
                downloadedBytes: $0.downloadedBytes, revision: revision)
        }
        let publication = InstallProgress(message: value.message, completed: value.completed, total: value.total, snapshot: snapshot)
        lock.unlock()
        progress(publication)
    }
}

private enum CommunityArchiveCache {
    static func destination(for file: CommunityResourceFile, source: CommunitySource, root: URL) async throws -> URL {
        let token = file.sha512 ?? file.sha1 ?? file.md5 ?? UUID().uuidString
        guard token.allSatisfy({ $0.isHexDigit || $0 == "-" }) else { throw CommunityInstallError.invalidFile(file.filename) }
        var folder = root.resolvingSymlinksInPath()
        for component in ["PCL", "download-cache", "community-packs"] {
            folder.appendPathComponent(component)
            if (try? FileManager.default.destinationOfSymbolicLink(atPath: folder.path)) != nil {
                throw CommunityInstallError.unsafePath(folder.path)
            }
        }
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let suffix = source == .modrinth ? "mrpack" : "zip"
        let archive = try MinecraftVersionMetadata.safeFile(relativePath: token + "." + suffix, under: folder)
        guard (try? FileManager.default.destinationOfSymbolicLink(atPath: folder.appendingPathComponent(token + "." + suffix).path)) == nil else {
            throw CommunityInstallError.unsafePath(archive.path)
        }
        if FileManager.default.fileExists(atPath: archive.path) {
            let verification = Task.detached { try CommunityResourceInstaller.fileMatches(at: archive, file: file) }
            let matches = try await withTaskCancellationHandler { try await verification.value } onCancel: { verification.cancel() }
            try Task.checkCancellation()
            if !matches {
                // This directory is launcher-owned cache. Preserve the bad bytes
                // for inspection and let the normal downloader repair the cache.
                let invalid = folder.appendingPathComponent(".invalid-\(UUID().uuidString).\(suffix)")
                try FileManager.default.moveItem(at: archive, to: invalid)
            }
        }
        return archive
    }
}

@MainActor extension LauncherStore {
    func openCommunity(_ kind: CommunityResourceKind) {
        page = .downloads
        currentRemoteSelection = nil
        resetModdedSelection()
        if communityState.kind != kind {
            cancelCommunityRequests()
            communityState = CommunityBrowserState(kind: kind)
        }
        if communityState.projects.isEmpty && !communityState.searching { searchCommunity() }
    }

    func showVanillaDownloads() {
        cancelCommunityRequests()
        communityState.kind = nil
        communityState.selectedProject = nil
        page = .downloads
        if remoteVersions.isEmpty && !manifestLoading { Task { await fetchManifest() } }
    }

    func resetCommunitySearch() {
        let kind = communityState.kind
        cancelCommunityRequests()
        communityState = CommunityBrowserState(kind: kind)
        searchCommunity()
    }

    private func cancelCommunityRequests() {
        communityState.searchTask?.cancel()
        communityState.detailTask?.cancel()
        communityState.versionsTask?.cancel()
        communityState.searchRequest = UUID()
        communityState.detailRequest = UUID()
        communityState.versionsRequest = UUID()
        communityState.searching = false
        communityState.loadingMore = false
        communityState.loadingDetail = false
        communityState.loadingVersions = false
    }

    func searchCommunity(reset: Bool = true) {
        guard let kind = communityState.kind else { return }
        if !reset && (communityState.loadingMore || communityState.searching || !communityState.hasMore) { return }
        communityState.searchTask?.cancel()
        let request = UUID()
        communityState.searchRequest = request
        let query: CommunitySearchQuery
        if !reset, var previous = communityState.lastQuery {
            previous.offset = communityState.nextOffset
            query = previous
        } else {
            let version = communityState.minecraftVersion.trimmingCharacters(in: .whitespacesAndNewlines)
            let category = communityState.category.trimmingCharacters(in: .whitespacesAndNewlines)
            query = CommunitySearchQuery(kind: kind, text: communityState.queryText.trimmingCharacters(in: .whitespacesAndNewlines),
                minecraftVersion: version.isEmpty ? nil : version,
                loader: (kind == .mod || kind == .modpack) ? ModLoaderKind(rawValue: communityState.loader) : nil,
                category: category.isEmpty ? nil : category, source: communityState.source, sort: communityState.sort)
        }
        communityState.lastQuery = query
        communityState.error = nil
        if reset {
            communityState.projects = []; communityState.total = 0; communityState.hasMore = false
            communityState.nextOffset = 0; communityState.warnings = []; communityState.searching = true
        } else { communityState.loadingMore = true }
        communityState.searchTask = Task { [self] in
            defer {
                if communityState.searchRequest == request {
                    communityState.searching = false; communityState.loadingMore = false
                    communityState.searchTask = nil
                }
            }
            do {
                let result = try await communityClient.search(query)
                try Task.checkCancellation()
                guard communityState.searchRequest == request, communityState.kind == kind else { return }
                let existing = Set(communityState.projects.map(\.id))
                communityState.projects.append(contentsOf: result.projects.filter { !existing.contains($0.id) })
                communityState.total = result.total
                communityState.hasMore = result.hasMore
                // Provider offsets count requested slots, including partial failures.
                communityState.nextOffset = query.offset + query.limit
                communityState.warnings = Array(Set(communityState.warnings + result.warnings)).sorted()
            } catch is CancellationError {
            } catch {
                guard communityState.searchRequest == request else { return }
                communityState.error = error.localizedDescription
            }
        }
    }

    func loadMoreCommunity() { searchCommunity(reset: false) }

    func openCommunityProject(_ project: CommunityProject) {
        communityState.detailTask?.cancel()
        communityState.selectedProject = project
        communityState.detail = nil; communityState.detailError = nil; communityState.versions = []
        communityState.versionFilter = communityState.minecraftVersion
        communityState.loaderFilter = communityState.loader
        let request = UUID()
        communityState.detailRequest = request
        communityState.loadingDetail = true
        communityState.detailTask = Task { [self] in
            defer {
                if communityState.detailRequest == request { communityState.loadingDetail = false; communityState.detailTask = nil }
            }
            do {
                let detail = try await communityClient.detail(project)
                try Task.checkCancellation()
                guard communityState.detailRequest == request, communityState.selectedProject?.id == project.id else { return }
                communityState.detail = detail
            } catch is CancellationError {
            } catch {
                guard communityState.detailRequest == request else { return }
                communityState.detail = CommunityProjectDetail(project: project, body: project.summary)
                communityState.detailError = "项目简介获取失败：" + error.localizedDescription
            }
        }
        reloadCommunityVersions()
    }

    func closeCommunityProject() {
        communityState.detailTask?.cancel(); communityState.versionsTask?.cancel()
        communityState.detailRequest = UUID(); communityState.versionsRequest = UUID()
        communityState.selectedProject = nil; communityState.detail = nil
        communityState.loadingDetail = false; communityState.loadingVersions = false
    }

    func reloadCommunityVersions() {
        guard let project = communityState.selectedProject else { return }
        communityState.versionsTask?.cancel()
        let request = UUID()
        communityState.versionsRequest = request
        communityState.loadingVersions = true; communityState.detailError = nil
        let filter = communityState.versionFilter.trimmingCharacters(in: .whitespacesAndNewlines)
        let loader = project.kind == .mod || project.kind == .modpack ? ModLoaderKind(rawValue: communityState.loaderFilter) : nil
        communityState.versionsTask = Task { [self] in
            defer {
                if communityState.versionsRequest == request { communityState.loadingVersions = false; communityState.versionsTask = nil }
            }
            do {
                let versions = try await communityClient.versions(for: project, minecraftVersion: filter.isEmpty ? nil : filter, loader: loader)
                try Task.checkCancellation()
                guard communityState.versionsRequest == request, communityState.selectedProject?.id == project.id else { return }
                communityState.versions = versions
            } catch is CancellationError {
            } catch {
                guard communityState.versionsRequest == request else { return }
                communityState.versions = []; communityState.detailError = error.localizedDescription
            }
        }
    }

    var communityTargetVersions: [InstalledVersion] {
        guard let version = communityState.selectedVersion, let project = communityState.selectedProject else { return installedVersions }
        return installedVersions.filter { local in
            let minecraft = remoteVersionID(for: local)
            if !version.gameVersions.isEmpty && !version.gameVersions.contains(minecraft) { return false }
            if project.kind == .mod {
                guard let metadata = try? MinecraftVersionMetadata.resolve(versionID: local.id, gameDirectory: gameDirectory),
                      let loader = MinecraftVersionMetadata.modLoader(in: metadata)?.kind else { return false }
                return version.loaders.isEmpty || version.loaders.map { $0.lowercased() }.contains(loader.rawValue.lowercased())
            }
            return true
        }
    }

    var communityDownloadValidation: String? {
        guard let project = communityState.selectedProject, let version = communityState.selectedVersion else { return "请先选择资源版本。" }
        guard version.primaryFile != nil else { return "此版本没有可下载文件。" }
        if project.kind == .modpack {
            do {
                try MinecraftInstaller.validateProfileID(communityState.downloadName)
                let target = try MinecraftVersionMetadata.safeFile(relativePath: "versions/\(communityState.downloadName)", under: gameDirectory)
                if FileManager.default.fileExists(atPath: target.path) || installedVersions.contains(where: { $0.id.caseInsensitiveCompare(communityState.downloadName) == .orderedSame }) {
                    return "该版本名称已存在，请修改版本名！"
                }
            } catch { return error.localizedDescription }
        } else {
            guard communityTargetVersions.contains(where: { $0.id == communityState.targetVersionID }) else { return "请选择与此资源兼容的游戏版本，或另存为文件。" }
            if project.kind == .datapack {
                guard !communityState.targetWorldPath.isEmpty,
                      let target = installedVersions.first(where: { $0.id == communityState.targetVersionID }) else { return "请选择要安装数据包的存档。" }
                let runtime = profileSettings(for: target).isolateGameDirectory ? target.directory : gameDirectory
                let saves = runtime.appendingPathComponent("saves").resolvingSymlinksInPath()
                let world = URL(fileURLWithPath: communityState.targetWorldPath).resolvingSymlinksInPath()
                guard world.path.hasPrefix(saves.path + "/"), FileManager.default.fileExists(atPath: world.appendingPathComponent("level.dat").path) else {
                    return "请选择该游戏版本 saves 文件夹中的有效存档。"
                }
            }
        }
        return nil
    }

    func prepareCommunityDownload(_ version: CommunityVersion) {
        guard !isBusy, !isGameRunning, let project = communityState.selectedProject else { return }
        guard version.projectID == project.projectID && version.source == project.source else { return }
        communityState.selectedVersion = version
        communityState.includeDependencies = true; communityState.targetWorldPath = ""; communityState.downloadError = nil
        communityState.downloadName = suggestedCommunityProfile(project.name)
        let candidates = communityTargetVersions
        communityState.targetVersionID = candidates.first(where: { $0.id == preferences.selectedVersion })?.id ?? candidates.first?.id ?? ""
        communityState.showDownloadSheet = true
    }

    func dismissCommunityDownload() {
        communityState.showDownloadSheet = false
        communityState.downloadError = nil
    }

    private func suggestedCommunityProfile(_ text: String) -> String {
        let invalid = CharacterSet(charactersIn: "/\\:*?\"<>|!;").union(.controlCharacters)
        var name = text.unicodeScalars.map { invalid.contains($0) ? "_" : String($0) }.joined().trimmingCharacters(in: .whitespacesAndNewlines)
        while name.hasPrefix(".") { name.removeFirst() }
        while name.hasSuffix(".") { name.removeLast() }
        while name.utf8.count > 180 { name.removeLast() }
        if name.isEmpty { name = "整合包" }
        let existing = Set(installedVersions.map { $0.id.lowercased() })
        if !existing.contains(name.lowercased()) { return name }
        for index in 2...1000 where !existing.contains("\(name) (\(index))".lowercased()) { return "\(name) (\(index))" }
        return name + "-" + UUID().uuidString.prefix(8)
    }

    func chooseCommunityWorld() {
        guard let target = installedVersions.first(where: { $0.id == communityState.targetVersionID }) else { return }
        let panel = NSOpenPanel()
        panel.canChooseFiles = false; panel.canChooseDirectories = true; panel.allowsMultipleSelection = false
        panel.canCreateDirectories = false
        let runtime = profileSettings(for: target).isolateGameDirectory ? target.directory : gameDirectory
        panel.directoryURL = runtime.appendingPathComponent("saves")
        panel.message = "选择该游戏版本 saves 文件夹中的存档（包含 level.dat）"
        panel.prompt = "选择存档"
        presentCommunityPanel(panel) { [weak self] response in
            guard response == .OK, let url = panel.url, let self else { return }
            guard self.communityState.targetVersionID == target.id else { return }
            let saves = runtime.appendingPathComponent("saves").resolvingSymlinksInPath()
            guard url.resolvingSymlinksInPath().path.hasPrefix(saves.path + "/"),
                  FileManager.default.fileExists(atPath: url.appendingPathComponent("level.dat").path) else {
                self.communityState.downloadError = "请选择该游戏版本 saves 文件夹中包含 level.dat 的存档。"; return
            }
            self.communityState.targetWorldPath = url.path
            self.communityState.downloadError = nil
        }
    }

    func startCommunityDownload() {
        guard !isBusy, !isGameRunning, let project = communityState.selectedProject,
              let version = communityState.selectedVersion else { return }
        if let validation = communityDownloadValidation { communityState.downloadError = validation; return }
        let root = gameDirectory
        let dependencies = communityState.includeDependencies
        let world = communityState.targetWorldPath.isEmpty ? nil : URL(fileURLWithPath: communityState.targetWorldPath)
        if project.kind == .modpack {
            let profileID = communityState.downloadName
            dismissCommunityDownload()
            runModdedInstallation(profileID: profileID, directory: root, summary: "安装整合包 \(project.name)") { [self] progress in
                guard let file = version.primaryFile else { throw CommunityInstallError.invalidFile("没有整合包文件") }
                let sequence = CommunityProgressSequence(progress: progress)
                let archive = try await CommunityArchiveCache.destination(for: file, source: project.source, root: root)
                try await communityInstaller.download(version: version, to: archive) { sequence.publish($0, phase: 0) }
                try Task.checkCancellation()
                sequence.begin(1)
                let descriptor = try await modpackImporter.inspect(archive: archive)
                if remoteVersions.isEmpty { await fetchManifest() }
                let metadataURL = remoteVersions.first { $0.id == descriptor.minecraftVersion }?.url
                let java = try await installationJava(minecraftVersion: descriptor.minecraftVersion, metadataURL: metadataURL)
                try await modpackImporter.importPack(archive: archive, gameDirectory: root, profileID: profileID,
                    javaExecutable: java.executable, progress: { sequence.publish($0, phase: 1) })
            }
        } else if let target = installedVersions.first(where: { $0.id == communityState.targetVersionID }) {
            dismissCommunityDownload()
            runModdedInstallation(profileID: target.id, directory: root, summary: "安装\(project.kind.displayName) \(project.name)") { [self] progress in
                let result = try await communityInstaller.install(project: project, version: version, gameDirectory: root,
                    targetVersion: target, worldDirectory: world, includeDependencies: dependencies, progress: progress)
                appendLog("\(project.name)：复用 \(result.reusedFiles) 个文件，下载 \(result.downloadedFiles) 个文件。")
            }
        }
    }

    func saveCommunityFile(_ version: CommunityVersion) {
        guard !isBusy, !isGameRunning, let file = version.primaryFile else { return }
        let panel = NSSavePanel()
        panel.nameFieldStringValue = file.filename
        panel.canCreateDirectories = true
        panel.message = "另存所选版本文件"
        panel.prompt = "下载"
        if let type = UTType(filenameExtension: (file.filename as NSString).pathExtension) { panel.allowedContentTypes = [type] }
        presentCommunityPanel(panel) { [weak self] response in
            guard response == .OK, let destination = panel.url, let self, !self.isBusy, !self.isGameRunning else { return }
            self.dismissCommunityDownload()
            let root = self.gameDirectory
            self.runModdedInstallation(profileID: file.filename, directory: root, summary: "下载 \(file.filename)",
                selectVersionOnSuccess: false, successPage: .downloads, successMessage: "文件下载完成：\(file.filename)") { progress in
                try await self.communityInstaller.download(version: version, to: destination, progress: progress)
                self.appendLog("已保存：\(destination.path)")
            }
        }
    }

    private func presentCommunityPanel(_ panel: NSSavePanel, completion: @escaping (NSApplication.ModalResponse) -> Void) {
        if let window = NSApp.keyWindow ?? NSApp.mainWindow { panel.beginSheetModal(for: window, completionHandler: completion) }
        else { panel.begin(completionHandler: completion) }
    }
}
