import AppKit
import PCLCore
import SwiftUI

/// PageResource: the five community sections share the original search card,
/// compact resource rows, and a page bar below the results.
struct PCLCommunityResourcesPage: View {
    @EnvironmentObject private var store: LauncherStore
    @State private var pageIndex = 0
    @State private var awaitingNextPage = false
    @State private var pageRanges: [Range<Int>] = []
    @State private var pendingPageStart = 0
    private let pageSize = 40

    var body: some View {
        VStack(alignment: .leading, spacing: 15) {
            if let project = store.communityState.selectedProject {
                PCLCommunityProjectPage(project: project)
            } else {
                searchCard
                results
            }
        }
        .onAppear { if pageRanges.isEmpty { rebuildPages() } }
        .onChange(of: store.communityState.kind) { _, _ in
            pageIndex = 0; awaitingNextPage = false; pageRanges = []
        }
        .onChange(of: store.communityState.searchRequest) { _, _ in
            if store.communityState.searching { pageIndex = 0; awaitingNextPage = false; pageRanges = [] }
        }
        .onChange(of: store.communityState.projects.count) { _, count in
            if awaitingNextPage, count > pendingPageStart {
                pageRanges.append(pendingPageStart..<count)
                pageIndex = pageRanges.count - 1
                awaitingNextPage = false
            } else if !awaitingNextPage { rebuildPages() }
            if count == 0 { pageIndex = 0; pageRanges = [] }
        }
        .onChange(of: store.communityState.loadingMore) { _, loading in
            if !loading { awaitingNextPage = false }
        }
    }

    private var searchCard: some View {
        PCLCard(title: "搜索\(store.communityState.kind?.displayName ?? "社区资源")") {
            VStack(alignment: .leading, spacing: 15) {
                Grid(horizontalSpacing: 15, verticalSpacing: 9) {
                    GridRow {
                        label("名称")
                        TextField("搜索名称", text: $store.communityState.queryText)
                            .textFieldStyle(.roundedBorder).onSubmit(runSearch)
                        label("来源")
                        PCLComboBox(title: "来源", selection: $store.communityState.source, options: [
                            .init(title: "全部", value: CommunitySource.all),
                            .init(title: "CurseForge", value: CommunitySource.curseForge),
                            .init(title: "Modrinth", value: CommunitySource.modrinth)
                        ]).frame(minWidth: 135, maxWidth: .infinity).frame(height: 28)
                    }
                    GridRow {
                        label("版本")
                        HStack(spacing: 10) {
                            TextField("全部（也可自行输入）", text: $store.communityState.minecraftVersion)
                                .textFieldStyle(.roundedBorder).onSubmit(runSearch)
                            if store.communityState.kind == .mod || store.communityState.kind == .modpack {
                                PCLComboBox(title: "Mod 加载器", selection: $store.communityState.loader, options: [
                                    PCLComboBoxOption(title: "任意 Mod 加载器", value: "")
                                ] + ModLoaderKind.allCases.map { PCLComboBoxOption(title: $0.displayName, value: $0.rawValue) })
                                .frame(maxWidth: 160).frame(height: 28)
                            }
                        }
                        label("类型")
                        PCLComboBox(title: "类型", selection: $store.communityState.category,
                                    options: CommunityCategory.options(for: store.communityState.kind ?? .mod).map {
                                        PCLComboBoxOption(title: $0.name, value: $0.tag)
                                    })
                            .frame(minWidth: 135, maxWidth: .infinity).frame(height: 28)
                    }
                }
                .font(.custom("PCL English", size: 13))
                HStack(spacing: 20) {
                    Button(action: runSearch) { Text("搜索").frame(minWidth: 112) }
                        .buttonStyle(PCLActionStyle(highlight: true))
                    Button {
                        pageIndex = 0; awaitingNextPage = false
                        store.resetCommunitySearch()
                    } label: { Text("重置条件").frame(minWidth: 112) }
                    .buttonStyle(PCLActionStyle())
                    if store.communityState.kind == .modpack {
                        Button { store.chooseModpackArchive() } label: { Text("安装已有整合包").frame(minWidth: 112) }
                            .buttonStyle(PCLActionStyle()).disabled(store.isBusy || store.isGameRunning)
                    }
                    Spacer(minLength: 0)
                }
                .padding(.top, 4).padding(.bottom, 6)
            }.padding(.horizontal, 5)
        }
    }

    @ViewBuilder private var results: some View {
        ForEach(store.communityState.warnings, id: \.self) { PCLHint(text: $0, warning: true) }
        if store.communityState.searching {
            loading("正在获取\(store.communityState.kind?.displayName ?? "资源")列表")
        } else if let error = store.communityState.error, store.communityState.projects.isEmpty {
            PCLCard(title: "获取失败") {
                Text(error).font(.custom("PCL English", size: 13)).foregroundStyle(ClassicTheme.text).textSelection(.enabled)
                Button("重试", action: runSearch).buttonStyle(PCLActionStyle(highlight: true))
            }
        } else if store.communityState.projects.isEmpty {
            PCLCard(title: "搜索结果") {
                Text("没有符合条件的结果，请调整搜索条件后重试。")
                    .font(.custom("PCL English", size: 13)).foregroundStyle(ClassicTheme.text).padding(.vertical, 12)
            }
        } else {
            VStack(spacing: 0) {
                ForEach(visibleProjects) { project in
                    PCLCommunityProjectRow(project: project) { store.openCommunityProject(project) }
                }
            }.padding(12).pclCardBox()
            if let error = store.communityState.error { PCLHint(text: error, warning: true) }
            pageBar
        }
    }

    private var visibleProjects: [CommunityProject] {
        let range = pageRanges.indices.contains(pageIndex) ? pageRanges[pageIndex] : 0..<min(pageSize, store.communityState.projects.count)
        return Array(store.communityState.projects.dropFirst(range.lowerBound).prefix(range.count))
    }

    private var hasNextPage: Bool { pageIndex + 1 < pageRanges.count || store.communityState.hasMore }

    private func rebuildPages() {
        let count = store.communityState.projects.count
        if pageRanges.last?.upperBound == count { return }
        pageRanges = stride(from: 0, to: count, by: pageSize).map { $0..<min(count, $0 + pageSize) }
        pageIndex = min(pageIndex, max(0, pageRanges.count - 1))
    }

    private var pageBar: some View {
        HStack(spacing: 15) {
            Button { pageIndex = 0 } label: { PCLIcon("PageFirst").frame(width: 14, height: 14) }
                .disabled(pageIndex == 0).help("第一页").accessibilityLabel("第一页")
            Button { pageIndex = max(0, pageIndex - 1) } label: { PCLIcon("PagePrevious").frame(width: 14, height: 14) }
                .disabled(pageIndex == 0).help("上一页").accessibilityLabel("上一页")
            Text(String(pageIndex + 1)).font(.custom("PCL English", size: 15)).foregroundStyle(Color.pclBlue)
                .frame(minWidth: 15)
            Button(action: nextPage) {
                if store.communityState.loadingMore { ProgressView().controlSize(.mini).frame(width: 12, height: 12) }
                else { PCLIcon("PageNext").frame(width: 14, height: 14) }
            }.disabled(!hasNextPage || store.communityState.loadingMore).help("下一页").accessibilityLabel("下一页")
        }
        .buttonStyle(.plain).foregroundStyle(Color.pclBlue)
        .padding(.horizontal, 15).frame(height: 37)
        .background(.white.opacity(0.96), in: RoundedRectangle(cornerRadius: 5))
        .shadow(color: Color.pclBlue.opacity(0.12), radius: 3, y: 1)
        .frame(maxWidth: .infinity).padding(.bottom, 10)
        .accessibilityElement(children: .contain).accessibilityLabel("搜索结果分页，第 \(pageIndex + 1) 页")
    }

    private func nextPage() {
        if pageIndex + 1 < pageRanges.count { pageIndex += 1 }
        else {
            if pageRanges.isEmpty { rebuildPages() }
            pendingPageStart = store.communityState.projects.count
            awaitingNextPage = true
            store.loadMoreCommunity()
        }
    }

    private func runSearch() {
        pageIndex = 0; awaitingNextPage = false
        store.searchCommunity()
    }

    private func label(_ text: String) -> some View {
        Text(text).foregroundStyle(ClassicTheme.text).frame(width: 28, alignment: .leading)
    }

    private func loading(_ message: String) -> some View {
        HStack(spacing: 12) { ProgressView().controlSize(.small); Text(message).font(.custom("PCL English", size: 13)) }
            .padding(20).background(.white.opacity(0.96), in: RoundedRectangle(cornerRadius: 5))
            .shadow(color: Color.pclBlue.opacity(0.12), radius: 3, y: 1)
            .frame(maxWidth: .infinity).padding(.vertical, 35)
    }
}

/// MyResourceItem uses a 50-point logo inside a 64-point row.
struct PCLCommunityProjectRow: View {
    let project: CommunityProject
    var action: (() -> Void)? = nil
    @State private var hovering = false

    @ViewBuilder var body: some View {
        if let action {
            Button(action: action) { content }
                .buttonStyle(.plain).onHover { hovering = $0 }
                .accessibilityLabel("\(project.name)，\(project.source.displayName)，\(project.summary)")
        } else { content }
    }

    private var content: some View {
        HStack(spacing: 8) {
            CommunityProjectIcon(url: project.iconURL, kind: project.kind).frame(width: 50, height: 50)
            VStack(alignment: .leading, spacing: 0) {
                Text(project.name).font(.custom("PCL English", size: 14))
                    .foregroundStyle(hovering && action != nil ? Color.pclBlue : ClassicTheme.text)
                    .lineLimit(1).frame(height: 18, alignment: .leading)
                Text(project.summary).font(.custom("PCL English", size: 12)).foregroundStyle(ClassicTheme.text.opacity(0.8))
                    .lineLimit(1).frame(height: 18, alignment: .leading)
                HStack(spacing: 15) {
                    if !project.gameVersions.isEmpty {
                        Label { Text(CommunityText.versionRange(project.gameVersions)) } icon: { PCLIcon("GameVersion").frame(width: 12, height: 11) }
                            .lineLimit(1).frame(maxWidth: 140, alignment: .leading)
                    } else if !project.author.isEmpty {
                        Text(project.author).lineLimit(1).frame(maxWidth: 140, alignment: .leading)
                    }
                    Label { Text(CommunityText.downloadCount(project.downloads)) } icon: { PCLIcon("Download").frame(width: 11, height: 10.5) }
                    if let date = project.updated {
                        Label { Text(CommunityText.date(date)) } icon: { PCLIcon("Date").frame(width: 11.5, height: 11.5) }
                            .lineLimit(1).layoutPriority(-1)
                    }
                    Label { Text(project.source.displayName) } icon: { PCLIcon("Globe").frame(width: 11.5, height: 11.5) }
                    Spacer(minLength: 0)
                }
                .font(.custom("PCL English", size: 11)).foregroundStyle(ClassicTheme.text.opacity(0.65))
                .frame(height: 16, alignment: .leading)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(.horizontal, 7).frame(height: 64)
        .background(hovering && action != nil ? Color.pclBlue.opacity(0.07) : .clear, in: RoundedRectangle(cornerRadius: 4))
        .contentShape(Rectangle())
    }
}

struct PCLCommunityProjectPage: View {
    @EnvironmentObject private var store: LauncherStore
    let project: CommunityProject
    @State private var copiedName = false

    var body: some View {
        VStack(alignment: .leading, spacing: 15) {
            introduction
            if store.communityState.loadingDetail || store.communityState.loadingVersions {
                HStack(spacing: 12) { ProgressView().controlSize(.small); Text("正在获取版本列表") }
                    .font(.custom("PCL English", size: 13)).padding(20).pclCardBox()
            } else {
                if let error = store.communityState.detailError {
                    PCLHint(text: error, warning: true)
                    if store.communityState.versions.isEmpty {
                        Button("重试") { store.reloadCommunityVersions() }.buttonStyle(PCLActionStyle(highlight: true))
                    }
                }
                if let detail = store.communityState.detail, !detail.body.isEmpty {
                    PCLCard(title: "项目介绍", collapsible: true, expanded: false) {
                        Text(CommunityText.description(detail.body))
                            .font(.custom("PCL English", size: 13)).foregroundStyle(ClassicTheme.text)
                            .lineSpacing(5).textSelection(.enabled)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .environment(\.openURL, OpenURLAction { url in
                                ["https", "http"].contains(url.scheme?.lowercased() ?? "") ? .systemAction : .discarded
                            })
                    }.id(project.id + "-description")
                }
                filters
                if groups.isEmpty {
                    PCLCard(title: "版本列表") {
                        Text("没有符合当前条件的可下载版本，请尝试其他游戏版本或 Mod 加载器。")
                            .font(.custom("PCL English", size: 13)).foregroundStyle(ClassicTheme.text).padding(.vertical, 12)
                    }
                } else {
                    ForEach(Array(groups.enumerated()), id: \.element.name) { index, group in
                        PCLCard(title: "\(group.name) (\(group.versions.count))", collapsible: true, expanded: index == 0) {
                            VStack(spacing: 0) {
                                ForEach(group.versions) { version in
                                    CommunityVersionRow(version: version) { store.prepareCommunityDownload(version) }
                                        .disabled(store.isBusy || store.isGameRunning || version.primaryFile == nil)
                                }
                            }
                        }.id(project.id + "-" + group.name + "-" + store.communityState.versionFilter + "-" + store.communityState.loaderFilter)
                    }
                }
            }
        }
    }

    private var introduction: some View {
        VStack(alignment: .leading, spacing: 14) {
            PCLCommunityProjectRow(project: store.communityState.detail?.project ?? project).padding(.horizontal, -7)
            HStack(spacing: 20) {
                Link(destination: project.websiteURL) { Text("转到 \(project.source.displayName)").frame(minWidth: 112) }
                    .buttonStyle(PCLActionStyle(highlight: true))
                if let url = wikiURL {
                    Link(destination: url) { Text("转到 MC 百科").frame(minWidth: 112) }.buttonStyle(PCLActionStyle())
                }
                Button {
                    NSPasteboard.general.clearContents()
                    copiedName = NSPasteboard.general.setString(project.name, forType: .string)
                } label: { Text(copiedName ? "已复制名称" : "复制名称").frame(minWidth: 112) }
                .buttonStyle(PCLActionStyle())
                Spacer(minLength: 0)
            }
        }.padding(EdgeInsets(top: 18, leading: 22, bottom: 19, trailing: 22)).pclCardBox()
    }

    private var filters: some View {
        HStack(spacing: 15) {
            Text("游戏版本").font(.custom("PCL English", size: 13)).foregroundStyle(ClassicTheme.text)
            TextField("全部", text: $store.communityState.versionFilter).textFieldStyle(.roundedBorder)
                .frame(maxWidth: 140).onSubmit { store.reloadCommunityVersions() }
            if project.kind == .mod || project.kind == .modpack {
                PCLComboBox(title: "Mod 加载器", selection: $store.communityState.loaderFilter, options: [
                    PCLComboBoxOption(title: "全部加载器", value: "")
                ] + ModLoaderKind.allCases.map { PCLComboBoxOption(title: $0.displayName, value: $0.rawValue) })
                .frame(maxWidth: 160).frame(height: 28)
                .onChange(of: store.communityState.loaderFilter) { _, _ in store.reloadCommunityVersions() }
            }
            Button("筛选") { store.reloadCommunityVersions() }.buttonStyle(PCLActionStyle(highlight: true))
            Spacer(minLength: 0)
        }.padding(10).pclCardBox()
    }

    private struct VersionGroup { let name: String; let versions: [CommunityVersion] }
    private var groups: [VersionGroup] {
        let values = Dictionary(grouping: store.communityState.versions) { version in
            if !store.communityState.versionFilter.isEmpty { return store.communityState.versionFilter }
            return version.gameVersions.sorted { $0.localizedStandardCompare($1) == .orderedDescending }.first ?? "其他版本"
        }
        return values.keys.sorted { $0.localizedStandardCompare($1) == .orderedDescending }
            .map { VersionGroup(name: $0, versions: values[$0] ?? []) }
    }

    private var wikiURL: URL? {
        var components = URLComponents(string: "https://search.mcmod.cn/s")
        components?.queryItems = [URLQueryItem(name: "key", value: project.name)]
        return components?.url
    }
}

private struct CommunityVersionRow: View {
    let version: CommunityVersion
    let action: () -> Void
    @State private var hovering = false

    var body: some View {
        Button(action: action) {
            HStack(spacing: 12) {
                PCLResourceImage(name: version.releaseType == "release" ? "Grass" : "CommandBlock", fallback: "Modpack")
                    .frame(width: 28, height: 28)
                VStack(alignment: .leading, spacing: 4) {
                    Text(version.name).font(.custom("PCL English", size: 13)).foregroundStyle(hovering ? Color.pclBlue : ClassicTheme.text).lineLimit(1)
                    HStack(spacing: 10) {
                        Text(version.loaders.map(CommunityText.loader).joined(separator: " / "))
                        if let date = version.published { Text(CommunityText.date(date)) }
                        if let file = version.primaryFile { Text(CommunityText.fileSize(file.size)) }
                        if version.releaseType != "release" { Text(version.releaseType.capitalized) }
                    }.font(.custom("PCL English", size: 11)).foregroundStyle(ClassicTheme.text.opacity(0.65)).lineLimit(1)
                }
                Spacer(minLength: 0)
                PCLIcon("Download").frame(width: 14, height: 14).foregroundStyle(Color.pclBlue)
                    .frame(width: 30, height: 30).opacity(hovering ? 1 : 0.5)
            }.padding(.horizontal, 6).frame(height: 52)
                .background(hovering ? Color.pclBlue.opacity(0.07) : .clear, in: RoundedRectangle(cornerRadius: 3))
                .contentShape(Rectangle())
        }.buttonStyle(.plain).onHover { hovering = $0 }.help(version.primaryFile?.filename ?? "此版本暂无可下载文件")
            .accessibilityLabel("下载 \(version.name)")
    }
}

struct PCLCommunityDownloadSheet: View {
    @EnvironmentObject private var store: LauncherStore

    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            Text("\(kind.displayName)下载").font(.custom("PCL English", size: 19)).foregroundStyle(Color.pclBlue)
            if let version = store.communityState.selectedVersion {
                VStack(alignment: .leading, spacing: 7) {
                    Text(store.communityState.selectedProject?.name ?? version.name).font(.custom("PCL English", size: 15))
                    Text(version.name).font(.custom("PCL English", size: 12)).foregroundStyle(.secondary)
                    if let file = version.primaryFile {
                        Text("\(file.filename) · \(CommunityText.fileSize(file.size))")
                            .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).textSelection(.enabled)
                    }
                }
                if kind == .modpack {
                    HStack(spacing: 15) {
                        Text("版本名称").frame(width: 75, alignment: .leading)
                        TextField("安装后的游戏版本名称", text: $store.communityState.downloadName).textFieldStyle(.roundedBorder)
                    }
                    PCLHint(text: "下载后自动导入整合包，补全所需游戏文件与加载器。")
                } else {
                    HStack(spacing: 15) {
                        Text("目标版本").frame(width: 75, alignment: .leading)
                        PCLComboBox(title: "目标版本", selection: $store.communityState.targetVersionID,
                                    options: (store.communityState.targetVersionID.isEmpty
                                        ? [PCLComboBoxOption(title: "请选择游戏版本", value: "")] : [])
                                        + store.communityTargetVersions.map { PCLComboBoxOption(title: $0.id, value: $0.id) })
                            .frame(maxWidth: .infinity).frame(height: 28)
                    }
                    .onChange(of: store.communityState.targetVersionID) { _, _ in store.communityState.targetWorldPath = "" }
                    if kind == .datapack {
                        HStack(alignment: .center, spacing: 15) {
                            Text("目标存档").frame(width: 75, alignment: .leading)
                            Text(store.communityState.targetWorldPath.isEmpty ? "请选择要添加数据包的存档" : URL(fileURLWithPath: store.communityState.targetWorldPath).lastPathComponent)
                                .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).lineLimit(2)
                                .frame(maxWidth: .infinity, alignment: .leading)
                            Button("选择存档") { store.chooseCommunityWorld() }.buttonStyle(PCLActionStyle())
                                .disabled(store.communityState.targetVersionID.isEmpty || store.isBusy || store.isGameRunning)
                        }
                    }
                    if kind == .mod && version.dependencies.contains(where: \.required) {
                        Toggle("同时安装所需的前置 Mod", isOn: $store.communityState.includeDependencies).toggleStyle(.checkbox)
                            .padding(.leading, 90)
                    }
                    if store.communityTargetVersions.isEmpty {
                        PCLHint(text: "还没有兼容的已安装游戏版本。可先保存文件，或返回下载页安装所需游戏和加载器。", warning: true)
                    } else if kind == .shader {
                        PCLHint(text: "下载后在游戏的光影设置中启用；请先确认游戏已安装对应的光影加载器。")
                    } else if kind == .resourcepack {
                        PCLHint(text: "下载后在游戏的资源包设置中启用。")
                    }
                }
                if let error = store.communityState.downloadError ?? store.communityDownloadValidation {
                    PCLHint(text: error, warning: true)
                }
                HStack(spacing: 12) {
                    Button("取消") { store.dismissCommunityDownload() }.buttonStyle(PCLActionStyle())
                    Spacer()
                    Button("保存到文件") { store.saveCommunityFile(version) }
                        .buttonStyle(PCLActionStyle()).disabled(store.isBusy || store.isGameRunning || version.primaryFile == nil)
                    Button(kind == .modpack ? "开始安装" : "下载并安装") { store.startCommunityDownload() }
                        .buttonStyle(PCLActionStyle(highlight: true)).disabled(!canInstall || store.isBusy || store.isGameRunning)
                }
            }
        }
        .font(.custom("PCL English", size: 13)).foregroundStyle(ClassicTheme.text)
        .padding(25).frame(width: 590).background(ClassicTheme.panel)
    }

    private var kind: CommunityResourceKind { store.communityState.selectedProject?.kind ?? store.communityState.kind ?? .mod }
    private var canInstall: Bool { store.communityDownloadValidation == nil }

}

private struct CommunityProjectIcon: View {
    let url: URL?
    let kind: CommunityResourceKind
    var body: some View {
        AsyncImage(url: url) { phase in
            if let image = phase.image { image.resizable().scaledToFit() }
            else {
                // MyResourceItem uses the original NoIcon bitmap for missing
                // thumbnails, including while a remote project image is loading.
                if let image = PCLSourceIcons.image("DefaultMod") {
                    Image(nsImage: image).renderingMode(.original).resizable().interpolation(.high).scaledToFit()
                } else {
                    PCLIcon("GameVersion").frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
        }.clipShape(RoundedRectangle(cornerRadius: 6)).accessibilityHidden(true)
    }
}

private enum CommunityText {
    static func loader(_ value: String) -> String {
        ModLoaderKind.allCases.first { $0.rawValue.lowercased() == value.lowercased() }?.displayName ?? value.capitalized
    }
    static func downloadCount(_ count: Int64) -> String {
        if count >= 100_000_000 { return String(format: "%.1f 亿", Double(count) / 100_000_000) }
        if count >= 10_000 { return String(format: "%.1f 万", Double(count) / 10_000) }
        return String(count)
    }
    static func fileSize(_ size: Int64) -> String { ByteCountFormatter.string(fromByteCount: size, countStyle: .file) }
    static func date(_ value: Date) -> String { value.formatted(date: .numeric, time: .omitted) }
    static func versionRange(_ versions: [String]) -> String {
        let sorted = versions.sorted { $0.localizedStandardCompare($1) == .orderedAscending }
        guard let first = sorted.first, let last = sorted.last else { return "" }
        return first == last ? first : first + " - " + last
    }
    static func description(_ body: String) -> AttributedString {
        var text = body
        // CurseForge supplies HTML while Modrinth supplies Markdown. Strip tags
        // without an HTML importer that could fetch embedded remote resources.
        if text.range(of: #"</?[A-Za-z][^>]*>"#, options: .regularExpression) != nil {
            text = text.replacingOccurrences(of: #"(?is)<(script|style)\b[^>]*>.*?</\1>"#, with: "", options: .regularExpression)
            text = text.replacingOccurrences(of: #"(?i)<br\s*/?>|</(?:p|div|li|h[1-6])\s*>"#, with: "\n", options: .regularExpression)
            text = text.replacingOccurrences(of: #"<[^>]+>"#, with: "", options: .regularExpression)
            for (entity, value) in [("&nbsp;", " "), ("&lt;", "<"), ("&gt;", ">"), ("&quot;", "\""), ("&#39;", "'"), ("&amp;", "&")] {
                text = text.replacingOccurrences(of: entity, with: value)
            }
        }
        return (try? AttributedString(markdown: text, options: .init(interpretedSyntax: .inlineOnlyPreservingWhitespace))) ?? AttributedString(text)
    }
}

private struct CommunityCategory {
    let name: String
    let tag: String
    static func options(for kind: CommunityResourceKind) -> [CommunityCategory] {
        switch kind {
        case .mod: return [
            .init(name: "全部", tag: ""),
            .init(name: "世界元素", tag: "406/worldgen"),
            .init(name: "生物群系", tag: "407/"),
            .init(name: "维度", tag: "410/"),
            .init(name: "矿物与资源", tag: "408/"),
            .init(name: "天然结构", tag: "409/"),
            .init(name: "科技", tag: "412/technology"),
            .init(name: "管道与物流", tag: "415/"),
            .init(name: "自动化", tag: "4843/"),
            .init(name: "能源", tag: "417/"),
            .init(name: "红石", tag: "4558/"),
            .init(name: "食物与烹饪", tag: "436/food"),
            .init(name: "农业", tag: "416/"),
            .init(name: "游戏机制", tag: "/game-mechanics"),
            .init(name: "运输", tag: "414/transportation"),
            .init(name: "仓储", tag: "420/storage"),
            .init(name: "魔法", tag: "419/magic"),
            .init(name: "冒险", tag: "422/adventure"),
            .init(name: "装饰", tag: "424/decoration"),
            .init(name: "生物", tag: "411/mobs"),
            .init(name: "实用", tag: "5191/utility"),
            .init(name: "装备与工具", tag: "434/equipment"),
            .init(name: "创造模式", tag: "9026/"),
            .init(name: "性能优化", tag: "6814/optimization"),
            .init(name: "信息显示", tag: "423/"),
            .init(name: "服务器", tag: "435/social"),
            .init(name: "支持库", tag: "421/library")
        ]
        case .modpack: return [
            .init(name: "全部", tag: ""),
            .init(name: "多人", tag: "4484/"),
            .init(name: "性能优化", tag: "/optimization"),
            .init(name: "硬核", tag: "4479/challenging"),
            .init(name: "战斗", tag: "4483/combat"),
            .init(name: "任务", tag: "4478/quests"),
            .init(name: "科技", tag: "4472/technology"),
            .init(name: "魔法", tag: "4473/magic"),
            .init(name: "冒险", tag: "4475/adventure"),
            .init(name: "水槽包", tag: "/kitchen-sink"),
            .init(name: "探索", tag: "4476/"),
            .init(name: "小游戏", tag: "4477/"),
            .init(name: "科幻", tag: "4474/"),
            .init(name: "空岛", tag: "4736/"),
            .init(name: "原版改良", tag: "5128/"),
            .init(name: "FTB", tag: "4487/"),
            .init(name: "基于地图", tag: "4480/"),
            .init(name: "轻量整合", tag: "4481/lightweight"),
            .init(name: "大型整合", tag: "4482/")
        ]
        case .datapack: return [
            .init(name: "全部", tag: ""),
            .init(name: "世界元素", tag: "/worldgen"),
            .init(name: "科技", tag: "6951/technology"),
            .init(name: "游戏机制", tag: "/game-mechanics"),
            .init(name: "运输", tag: "/transportation"),
            .init(name: "仓储", tag: "/storage"),
            .init(name: "魔法", tag: "6952/magic"),
            .init(name: "冒险", tag: "6948/adventure"),
            .init(name: "幻想", tag: "6949/"),
            .init(name: "装饰", tag: "/decoration"),
            .init(name: "生物", tag: "/mobs"),
            .init(name: "实用", tag: "6953/utility"),
            .init(name: "装备与工具", tag: "/equipment"),
            .init(name: "性能优化", tag: "/optimization"),
            .init(name: "服务器", tag: "/social"),
            .init(name: "支持库", tag: "6950/library"),
            .init(name: "Mod 相关", tag: "6946/")
        ]
        case .resourcepack: return [
            .init(name: "全部", tag: ""),
            .init(name: "原版风", tag: "403/vanilla-like"),
            .init(name: "写实风", tag: "400/realistic"),
            .init(name: "现代风", tag: "401/"),
            .init(name: "中世纪", tag: "402/"),
            .init(name: "蒸汽朋克", tag: "399/"),
            .init(name: "主题化", tag: "/themed"),
            .init(name: "简洁", tag: "/simplistic"),
            .init(name: "装饰", tag: "/decoration"),
            .init(name: "战斗", tag: "/combat"),
            .init(name: "实用", tag: "/utility"),
            .init(name: "改良", tag: "/tweaks"),
            .init(name: "鬼畜", tag: "/cursed"),
            .init(name: "含实体", tag: "/entities"),
            .init(name: "含声音", tag: "/audio"),
            .init(name: "含字体", tag: "5244/fonts"),
            .init(name: "含模型", tag: "/models"),
            .init(name: "含语言", tag: "/locale"),
            .init(name: "含 UI", tag: "/gui"),
            .init(name: "核心着色器", tag: "/core-shaders"),
            .init(name: "动态效果", tag: "404/"),
            .init(name: "兼容 Mod", tag: "4465/modded"),
            .init(name: "8x 或更低", tag: "/8x-"),
            .init(name: "16x", tag: "393/16x"),
            .init(name: "32x", tag: "394/32x"),
            .init(name: "48x", tag: "/48x"),
            .init(name: "64x", tag: "395/64x"),
            .init(name: "128x", tag: "396/128x"),
            .init(name: "256x", tag: "397/256x"),
            .init(name: "512x 或更高", tag: "398/512x+")
        ]
        case .shader: return [
            .init(name: "全部", tag: ""),
            .init(name: "原版风", tag: "6555/vanilla-like"),
            .init(name: "幻想风", tag: "6554/fantasy"),
            .init(name: "写实风", tag: "6553/realistic"),
            .init(name: "半写实风", tag: "/semi-realistic"),
            .init(name: "卡通风", tag: "/cartoon"),
            .init(name: "彩色光照", tag: "/colored-lighting"),
            .init(name: "路径追踪", tag: "/path-tracing"),
            .init(name: "PBR", tag: "/pbr"),
            .init(name: "反射", tag: "/reflections"),
            .init(name: "极低", tag: "/potato"),
            .init(name: "低", tag: "/low"),
            .init(name: "中", tag: "/medium"),
            .init(name: "高", tag: "/high"),
            .init(name: "原版可用", tag: "/vanilla"),
            .init(name: "Iris", tag: "/iris"),
            .init(name: "OptiFine", tag: "/optifine")
        ]
        }
    }
}
