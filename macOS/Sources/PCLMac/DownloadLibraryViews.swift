import AppKit
import PCLCore
import SwiftUI

struct PCLVersionLibraryPage: View {
    @EnvironmentObject private var store: LauncherStore

    var body: some View {
        Group {
            if store.scanning {
                PCLCard(title: "") {
                    HStack(spacing: 12) {
                        ProgressView().controlSize(.small)
                        Text("正在获取版本列表").font(.custom("PCL English", size: 13))
                    }.frame(maxWidth: .infinity).padding(.vertical, 20)
                }
            } else if store.installedVersions.isEmpty {
                emptyLibrary
            } else {
                HStack(spacing: 10) {
                    PCLSearchField(text: $store.search, prompt: "搜索版本")
                    Toggle("显示隐藏版本", isOn: $store.showHiddenVersions).toggleStyle(.checkbox).font(.custom("PCL English", size: 12))
                    Button { Task { await store.refreshLocal() } } label: { PCLIcon("Refresh").frame(width: 14, height: 14) }
                        .buttonStyle(PCLActionStyle()).help("刷新版本列表").accessibilityLabel("刷新版本列表")
                }
                ForEach(groups, id: \.name) { group in
                    PCLCard(title: group.name == "收藏夹" ? group.name : "\(group.name) (\(group.versions.count))",
                            collapsible: true, expanded: groups.count == 1 || !["不常用版本", "愚人节版本"].contains(group.name)) {
                        LazyVStack(spacing: 2) { ForEach(group.versions) { PCLInstalledVersionRow(version: $0) } }
                    }.id("\(group.name)-\(groups.count == 1)-\(store.search.isEmpty)")
                }
                if groups.isEmpty {
                    PCLCard(title: store.showHiddenVersions ? "无隐藏版本" : "版本列表") {
                        Text(store.showHiddenVersions ? "没有版本被隐藏，你可以在版本设置的版本分类选项中隐藏版本。\n再次关闭隐藏版本查看模式即可返回。" : "没有找到匹配的版本。")
                            .font(.custom("PCL English", size: 13)).foregroundStyle(.secondary).lineSpacing(5).padding(.vertical, 15)
                    }
                }
            }
        }
    }

    private var emptyLibrary: some View {
        VStack {
            Spacer(minLength: 55)
            VStack(spacing: 0) {
                Text("无可用版本").font(.custom("PCL English", size: 19)).foregroundStyle(Color.pclBlue)
                Rectangle().fill(Color.pclBlue).frame(height: 2).padding(.top, 9)
                Text("未找到任何版本的游戏，请先下载任意版本的游戏。\n若有已存在的游戏，请在左边的列表中选择“添加已有文件夹”，选择 .minecraft 文件夹将其导入。")
                    .font(.custom("PCL English", size: 13)).lineSpacing(5).frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal, 10).padding(.top, 15)
                Button("下载游戏") { store.page = .downloads }
                    .buttonStyle(PCLActionStyle(highlight: true)).padding(.top, 15)
            }.padding(.horizontal, 20).padding(.vertical, 17).pclCardBox()
            Spacer(minLength: 55)
        }.frame(minHeight: 430).padding(.horizontal, 15)
    }

    private struct GroupedVersions { let name: String; let versions: [InstalledVersion] }
    private var groups: [GroupedVersions] {
        let orderedNames = ["收藏夹", "可安装 Mod", "常规版本", "不常用版本", "愚人节版本", "隐藏的版本"]
        var grouped: [String: [InstalledVersion]] = [:]
        for version in store.visibleInstalledVersions {
            let profile = store.profileSettings(for: version)
            let name: String
            if profile.category == "hidden" { name = "隐藏的版本" }
            else if profile.isFavorite { name = "收藏夹" }
            else {
                switch profile.category {
                case "modded": name = "可安装 Mod"
                case "regular": name = "常规版本"
                case "rare": name = "不常用版本"
                case "fool": name = "愚人节版本"
                default:
                    if version.canInstallMods { name = "可安装 Mod" }
                    else if version.type == "release" { name = "常规版本" }
                    else if version.type == "special" { name = "愚人节版本" }
                    else { name = "不常用版本" }
                }
            }
            grouped[name, default: []].append(version)
        }
        return orderedNames.compactMap { name in
            guard let versions = grouped[name] else { return nil }
            // PageSelectRight names a single-loader API group after its loader.
            // A mixed group retains the original generic title.
            let kinds = Set(versions.flatMap { version -> [String] in
                var loaders = version.modLoader.map { [$0.kind.displayName] } ?? []
                if version.liteLoaderVersion != nil { loaders.append("LiteLoader") }
                return loaders
            })
            let title = name == "可安装 Mod" && kinds.count == 1
                ? "\(kinds.first!) 版本" : name
            return GroupedVersions(name: title, versions: versions)
        }
    }
}

struct PCLInstalledVersionRow: View {
    @EnvironmentObject private var store: LauncherStore
    let version: InstalledVersion
    @State private var hovering = false

    var body: some View {
        let profile = store.profileSettings(for: version)
        ZStack(alignment: .trailing) {
            Button { store.selectVersion(version) } label: {
                HStack(spacing: 10) {
                    Group {
                        if let image = store.customVersionIcon(for: version, iconName: profile.iconName) {
                            Image(nsImage: image).resizable().scaledToFit()
                        } else {
                            PCLResourceImage(name: profile.iconName.isEmpty ? store.defaultVersionIcon(for: version) : profile.iconName, fallback: "GameVersion")
                        }
                    }.frame(width: 32, height: 32)
                    VStack(alignment: .leading, spacing: 4) {
                        HStack(alignment: .firstTextBaseline, spacing: 6) {
                            Text(version.id).font(.custom("PCL English", size: 14)).foregroundStyle(hovering ? Color.pclBlue : ClassicTheme.text)
                            if !profile.displayDescription.isEmpty {
                                Text("|  \(store.versionDisplayName(for: version))")
                                    .font(.custom("PCL English", size: 12)).foregroundStyle(.tertiary).lineLimit(1)
                            }
                        }
                        Text(profile.displayDescription.isEmpty ? store.versionDisplayName(for: version) : profile.displayDescription)
                            .font(.custom("PCL English", size: 11)).foregroundStyle(.secondary).lineLimit(2)
                    }
                    Spacer(minLength: 0)
                }
                .padding(.trailing, hovering ? 80 : 4)
                .frame(maxWidth: .infinity, alignment: .leading).frame(height: 42).contentShape(Rectangle())
                .animation(.linear(duration: hovering ? 0.036 : 0.072).delay(hovering ? 0.084 : 0), value: hovering)
            }.buttonStyle(.plain).disabled(store.isBusy || store.isGameRunning)
                .accessibilityAction(named: Text(profile.isFavorite ? "取消收藏" : "收藏")) { store.toggleVersionFavorite(version) }
                .accessibilityAction(named: Text("删除")) { store.deleteVersion(version) }
                .accessibilityAction(named: Text("设置")) { store.openVersionSettings(version) }
            HStack(spacing: 0) {
                PCLVersionActionButton(icon: profile.isFavorite ? "FavoriteFilled" : "Favorite",
                                       title: profile.isFavorite ? "取消收藏" : "收藏",
                                       identifier: "version.\(version.id).favorite") { store.toggleVersionFavorite(version) }
                PCLVersionActionButton(icon: "Delete", title: "删除", identifier: "version.\(version.id).delete") {
                    store.deleteVersion(version)
                }
                PCLVersionActionButton(icon: "Settings", title: "设置", identifier: "version.\(version.id).settings") {
                    store.openVersionSettings(version)
                }
            }
            .padding(.trailing, 5)
            .opacity(hovering ? 1 : 0)
            .animation(.linear(duration: hovering ? 0.084 : 0.072).delay(hovering ? 0.036 : 0), value: hovering)
            .allowsHitTesting(hovering)
            .accessibilityHidden(!hovering)
            .disabled(store.isBusy || store.isGameRunning)
        }
        .overlay {
            PCLVersionRowSecondaryClick(enabled: !store.isBusy && !store.isGameRunning) {
                store.openVersionSettings(version)
            }
        }
        .padding(.horizontal, 5).background(hovering ? Color.pclBlue.opacity(0.07) : .clear, in: RoundedRectangle(cornerRadius: 3))
        .onHover { hovering = $0 }
    }
}

struct PCLDownloadsPage: View {
    @EnvironmentObject private var store: LauncherStore

    var body: some View {
        Group {
            if store.communityState.kind != nil { PCLCommunityResourcesPage() }
            else if let version = store.currentRemoteSelection { selection(version) }
            else { catalogue }
        }
    }

    private var catalogue: some View {
        Group {
            if store.manifestLoading && store.remoteVersions.isEmpty {
                PCLCard(title: "") {
                    HStack(spacing: 12) { ProgressView().controlSize(.small); Text("正在加载").font(.custom("PCL English", size: 13)) }
                        .frame(maxWidth: .infinity).padding(.vertical, 20)
                }
            } else if store.remoteVersions.isEmpty {
                PCLCard(title: "版本列表") {
                    Text("获取版本列表失败，请检查网络连接后重试。").font(.custom("PCL English", size: 13)).foregroundStyle(.secondary).padding(.vertical, 12)
                    Button("重试") { Task { await store.fetchManifest() } }.buttonStyle(PCLActionStyle())
                }
            } else {
                HStack(spacing: 12) {
                    PCLSearchField(text: $store.search, prompt: "搜索版本")
                    Button { Task { await store.fetchManifest() } } label: { PCLIcon("Refresh").frame(width: 14, height: 14) }
                        .buttonStyle(PCLActionStyle()).disabled(store.manifestLoading).help("刷新版本列表").accessibilityLabel("刷新版本列表")
                }
                if store.search.isEmpty {
                    PCLCard(title: "最新版本") { VStack(spacing: 2) { ForEach(latestVersions) { PCLRemoteVersionRow(version: $0, latest: true) } } }
                }
                ForEach(PCLDownloadCategory.allCases) { category in
                    let versions = filteredVersions.filter { PCLDownloadCategory.category(for: $0) == category }
                    if !versions.isEmpty {
                        PCLCard(title: category.rawValue, collapsible: true, expanded: !store.search.isEmpty) {
                            LazyVStack(spacing: 2) { ForEach(versions) { PCLRemoteVersionRow(version: $0) } }
                        }.id("\(category.rawValue)-\(store.search.isEmpty)")
                    }
                }
                if !store.search.isEmpty && filteredVersions.isEmpty {
                    PCLCard(title: "版本列表") { Text("没有找到匹配的版本。").font(.custom("PCL English", size: 13)).foregroundStyle(.secondary).padding(.vertical, 15) }
                }
            }
        }
    }

    private func selection(_ version: RemoteVersion) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 14) {
                Button { store.backToDownloadList() } label: { PCLIcon("ArrowLeft").frame(width: 16, height: 16).frame(width: 26, height: 30) }
                    .buttonStyle(.plain).foregroundStyle(ClassicTheme.text).help("返回版本列表").accessibilityLabel("返回版本列表")
                PCLResourceImage(name: PCLDownloadCategory.category(for: version).icon, fallback: "GameVersion").frame(width: 32, height: 32)
                TextField("版本名称", text: $store.installNameDraft).textFieldStyle(.plain).font(.custom("PCL English", size: 15))
                    .accessibilityLabel("安装版本名称")
                Button { store.installNameDraft = store.suggestedInstallName } label: { PCLIcon("Restore").frame(width: 13, height: 13).frame(width: 24, height: 30) }
                    .buttonStyle(.plain).foregroundStyle(Color.pclBlue.opacity(0.7)).help("恢复默认版本名").accessibilityLabel("恢复默认版本名")
                    .disabled(store.installNameDraft == store.suggestedInstallName)
            }.padding(.horizontal, 15).frame(height: 64).pclCardBox()
            if let error = store.installNameError {
                PCLHint(text: error, warning: true)
            }
            component("Minecraft", icon: "Grass", value: version.id, enabled: true) { store.backToDownloadList() }
            ForEach(ModLoaderKind.allCases, id: \.self) { kind in
                PCLLoaderComponent(kind: kind)
            }
            if let selected = store.moddedState.selectedLoader {
                PCLHint(text: "已选择 \(selected.kind.displayName) \(selected.version)。组件会安装到独立游戏目录。")
            }
            if store.moddedState.selectedLoader?.kind == .fabric {
                PCLFabricAPIComponent()
                if let error = store.fabricAPISelectionError, !store.moddedState.loadingFabricAPI {
                    PCLHint(text: error, warning: true)
                } else if store.moddedState.selectedFabricAPI == nil && !store.moddedState.loadingFabricAPI {
                    PCLHint(text: "未选择 Fabric API，大部分 Fabric Mod 需要安装它才能使用。", warning: true)
                }
            }
            ForEach(ExtraComponentKind.allCases, id: \.self) { kind in PCLExtraComponent(kind: kind) }
            if let error = store.extraComponentSelectionError { PCLHint(text: error, warning: true) }
            Text(store.gameDirectory.appendingPathComponent("versions").appendingPathComponent(store.installNameDraft).path)
                .font(.custom("PCL English", size: 11)).foregroundStyle(.secondary).lineLimit(2).textSelection(.enabled)
                .padding(.horizontal, 4).padding(.top, 3)
        }.padding(.bottom, 58)
    }

    private func component(_ title: String, icon: String, value: String, enabled: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack(spacing: 0) {
                Text(title).font(.custom("PCL English", size: 13).weight(.bold)).foregroundStyle(ClassicTheme.text).frame(width: 115, alignment: .leading)
                PCLResourceImage(name: icon, fallback: "GameVersion").frame(width: 18, height: 18).padding(.trailing, 7)
                Text(value).font(.custom("PCL English", size: 13)).foregroundStyle(enabled ? ClassicTheme.text : .secondary)
                Spacer(minLength: 6)
                PCLIcon("ChevronRight").frame(width: 10, height: 10).foregroundStyle(ClassicTheme.text.opacity(0.7))
            }.padding(.horizontal, 16).frame(height: 40).contentShape(Rectangle())
        }.buttonStyle(.plain).pclCardBox().disabled(!enabled || store.isBusy || store.isGameRunning)
            .opacity(enabled ? 1 : 0.55).help(enabled ? "重新选择 Minecraft 版本" : "\(title) 组件自动安装尚未接入")
    }

    private var filteredVersions: [RemoteVersion] { store.remoteVersions.filter { store.search.isEmpty || $0.id.localizedCaseInsensitiveContains(store.search) } }
    private var latestVersions: [RemoteVersion] {
        let release = store.remoteVersions.first { PCLDownloadCategory.category(for: $0) == .release }
        let preview = store.remoteVersions.first { PCLDownloadCategory.category(for: $0) == .preview }
        var result: [RemoteVersion] = []
        if let release { result.append(release) }
        if let preview, (preview.releaseTime ?? "") > (release?.releaseTime ?? "") { result.append(preview) }
        return result
    }
}

struct PCLInstallButton: View {
    @EnvironmentObject private var store: LauncherStore
    var body: some View {
        Button { store.installSelectedVersion() } label: {
            PCLIconLabel("开始下载", icon: "Download").font(.custom("PCL English", size: 15)).foregroundStyle(.white)
                .padding(.horizontal, 19).frame(height: 40)
                .background(Color.pclBlue, in: Capsule()).shadow(color: Color.pclBlue.opacity(0.2), radius: 4, y: 2)
        }.buttonStyle(.plain).disabled(store.installNameError != nil || store.fabricAPISelectionError != nil || store.extraComponentSelectionError != nil || store.isBusy || store.isGameRunning)
            .opacity(store.installNameError == nil && store.fabricAPISelectionError == nil && store.extraComponentSelectionError == nil && !store.isBusy && !store.isGameRunning ? 1 : 0.5)
    }
}

private struct PCLRemoteVersionRow: View {
    @EnvironmentObject private var store: LauncherStore
    let version: RemoteVersion
    var latest = false
    @State private var hovering = false
    var body: some View {
        let category = PCLDownloadCategory.category(for: version)
        return Button { store.selectRemoteVersion(version) } label: {
            HStack(spacing: 10) {
                PCLResourceImage(name: category.icon, fallback: "GameVersion").frame(width: 32, height: 32)
                VStack(alignment: .leading, spacing: 4) {
                    Text(version.id).font(.custom("PCL English", size: 14)).foregroundStyle(hovering ? Color.pclBlue : ClassicTheme.text)
                    Text("\(latest ? "最新" : "")\(category.rawValue)，发布于 \(releaseDate)")
                        .font(.custom("PCL English", size: 11)).foregroundStyle(.secondary)
                }
                Spacer(minLength: 6)
                if store.installedVersions.contains(where: { store.remoteVersionID(for: $0) == version.id }) {
                    Text("已安装").font(.custom("PCL English", size: 11)).foregroundStyle(.secondary)
                }
                PCLIcon("Download").frame(width: 14, height: 14).foregroundStyle(Color.pclBlue)
                    .frame(width: 25).opacity(hovering ? 1 : 0)
            }.frame(maxWidth: .infinity, alignment: .leading).frame(minHeight: 48).padding(.horizontal, 5).contentShape(Rectangle())
        }.buttonStyle(.plain).background(hovering ? Color.pclBlue.opacity(0.07) : .clear, in: RoundedRectangle(cornerRadius: 3))
            .onHover { hovering = $0 }.disabled(store.isBusy || store.isGameRunning)
            .contextMenu {
                Button("安装为新版本") { store.selectRemoteVersion(version) }.disabled(store.isBusy || store.isGameRunning)
                if let installed = store.installedVersions.first(where: { store.remoteVersionID(for: $0) == version.id }) {
                    Button("补全文件") { store.repair(installed) }.disabled(store.isBusy || store.isGameRunning)
                }
            }
    }
    private var releaseDate: String {
        guard let text = version.releaseTime, let date = ISO8601DateFormatter().date(from: text) else { return "未知" }
        let formatter = DateFormatter(); formatter.locale = Locale(identifier: "zh_CN"); formatter.dateFormat = "yyyy/MM/dd HH:mm"
        return formatter.string(from: date)
    }
}

struct PCLSearchField: View {
    @Binding var text: String
    let prompt: String
    var body: some View {
        HStack(spacing: 8) {
            PCLIcon("Search").frame(width: 14, height: 14).foregroundStyle(Color.pclBlue.opacity(0.7))
            TextField(prompt, text: $text).textFieldStyle(.plain)
            if !text.isEmpty { Button { text = "" } label: { PCLIcon("Clear").frame(width: 12, height: 12) }.buttonStyle(.plain).foregroundStyle(.secondary).help("清空搜索").accessibilityLabel("清空搜索") }
        }.font(.custom("PCL English", size: 13)).padding(.horizontal, 10).frame(height: 35)
            .background(.white.opacity(0.95), in: RoundedRectangle(cornerRadius: 3))
            .overlay { RoundedRectangle(cornerRadius: 3).strokeBorder(Color.pclBlue.opacity(0.35), lineWidth: 1) }
    }
}

struct PCLHint: View {
    let text: String
    var warning = false
    var body: some View {
        HStack(alignment: .top, spacing: 8) {
            // MyHint.xaml presents the hint text without a leading symbol.
            Text(text).lineSpacing(3)
        }.font(.custom("PCL English", size: 12)).foregroundStyle(warning ? Color(red: 0.74, green: 0.25, blue: 0.23) : Color.pclBlue)
            .padding(10).frame(maxWidth: .infinity, alignment: .leading)
            .background(warning ? Color(red: 1, green: 0.93, blue: 0.92) : Color.pclBlue.opacity(0.06), in: RoundedRectangle(cornerRadius: 3))
    }
}

enum PCLDownloadCategory: String, CaseIterable, Identifiable {
    case release = "正式版", preview = "预览版", ancient = "远古版", aprilFools = "愚人节版"
    var id: String { rawValue }
    var icon: String {
        switch self { case .release: return "Grass"; case .preview: return "CommandBlock"; case .ancient: return "CobbleStone"; case .aprilFools: return "CommandBlock" }
    }
    private static let specialIDs: Set<String> = ["20w14infinite", "20w14∞", "3d shareware v1.34", "1.rv-pre1", "15w14a", "2.0", "22w13oneblockatatime", "23w13a_or_b", "24w14potato", "25w14craftmine", "26w14a"]
    private static let dateParser = ISO8601DateFormatter()
    static func category(for version: RemoteVersion) -> Self {
        let id = version.id.lowercased()
        if version.type == "release" { return .release }
        if version.type == "special" || specialIDs.contains(id) { return .aprilFools }
        if version.type == "snapshot" {
            if id.hasPrefix("1.") && !["combat", "rc", "experimental", "pre"].contains(where: id.contains) { return .release }
            if let value = version.releaseTime, let date = dateParser.date(from: value) {
                var calendar = Calendar(identifier: .gregorian); calendar.timeZone = TimeZone(secondsFromGMT: 7200)!
                let parts = calendar.dateComponents([.month, .day], from: date)
                if parts.month == 4 && parts.day == 1 { return .aprilFools }
            }
            return .preview
        }
        return .ancient
    }
}

private struct PCLLoaderComponent: View {
    @EnvironmentObject private var store: LauncherStore
    let kind: ModLoaderKind

    private var selected: ModLoaderVersion? {
        store.moddedState.selectedLoader.flatMap { $0.kind == kind ? $0 : nil }
    }
    private var expanded: Bool { store.moddedState.expandedLoader == kind }

    var body: some View {
        VStack(spacing: 0) {
            Button { store.toggleLoaderCatalogue(kind) } label: {
                HStack(spacing: 0) {
                    Text(kind.displayName).font(.custom("PCL English", size: 13).weight(.bold))
                        .foregroundStyle(ClassicTheme.text).frame(width: 115, alignment: .leading)
                    PCLResourceImage(name: kind.pclIcon, fallback: "Mod").frame(width: 18, height: 18).padding(.trailing, 7)
                    Text(store.initialLoaderConflict(kind) ?? selected?.version ?? "未选择").font(.custom("PCL English", size: 13))
                        .foregroundStyle(selected == nil ? ClassicTheme.text : .pclBlue)
                    Spacer(minLength: 6)
                    PCLIcon(expanded ? "ChevronDown" : "ChevronRight").frame(width: 11, height: 11)
                        .font(.system(size: 10, weight: .medium)).foregroundStyle(ClassicTheme.text.opacity(0.7))
                }.padding(.horizontal, 16).frame(height: 40).contentShape(Rectangle())
            }.buttonStyle(.plain).disabled(store.isBusy || store.isGameRunning || store.initialLoaderConflict(kind) != nil)
                .accessibilityLabel("选择 \(kind.displayName)，\(selected?.version ?? "未选择")")
            if expanded { versions.padding(.horizontal, 12).padding(.bottom, 10) }
        }.pclCardBox()
    }

    @ViewBuilder private var versions: some View {
        Divider().padding(.bottom, 5)
        Button { store.chooseLoader(nil) } label: {
            HStack {
                Text("不安装 Mod 加载器（原版）")
                Spacer()
                if store.moddedState.selectedLoader == nil { PCLIcon("Check").frame(width: 12, height: 12).foregroundStyle(Color.pclBlue) }
            }.font(.custom("PCL English", size: 12)).foregroundStyle(ClassicTheme.text)
                .padding(.horizontal, 6).frame(height: 32).contentShape(Rectangle())
        }.buttonStyle(.plain)
        if store.moddedState.loadingLoaders.contains(kind) {
            HStack(spacing: 10) {
                ProgressView().controlSize(.small)
                Text("正在获取 \(kind.displayName) 版本列表…")
            }.font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).padding(12)
                .frame(maxWidth: .infinity, alignment: .leading)
        } else if let error = store.moddedState.loaderErrors[kind] {
            Text(error).font(.custom("PCL English", size: 12)).foregroundStyle(.red).padding(7)
                .frame(maxWidth: .infinity, alignment: .leading)
            Button("重新获取") { store.loadLoaderCatalogue(kind) }.buttonStyle(PCLActionStyle())
        } else if !store.initialLoaderVersions(kind).isEmpty {
            let versions = store.initialLoaderVersions(kind)
            ScrollView {
                LazyVStack(spacing: 2) {
                    ForEach(versions) { version in
                        PCLLoaderVersionRow(version: version, selected: selected?.id == version.id) { store.chooseLoader(version) }
                    }
                }
            }.frame(height: min(238, CGFloat(versions.count) * 35))
        } else {
            Text("\(kind.displayName) 没有适用于此 Minecraft 版本的可安装版本。")
                .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).padding(12)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

private struct PCLLoaderVersionRow: View {
    let version: ModLoaderVersion
    let selected: Bool
    let action: () -> Void
    @State private var hovering = false
    var body: some View {
        Button(action: action) {
            HStack(spacing: 8) {
                Text(version.version).font(.custom("PCL English", size: 13)).foregroundStyle(ClassicTheme.text)
                if version.recommended { Text("推荐").foregroundStyle(Color.pclBlue) }
                else if !version.stable { Text("预览版").foregroundStyle(.secondary) }
                Spacer(minLength: 6)
                if selected { PCLIcon("Check").frame(width: 12, height: 12).foregroundStyle(Color.pclBlue) }
            }.font(.custom("PCL English", size: 11)).padding(.horizontal, 8).frame(height: 33).contentShape(Rectangle())
        }.buttonStyle(.plain).background(hovering || selected ? Color.pclBlue.opacity(0.07) : .clear, in: RoundedRectangle(cornerRadius: 3))
            .onHover { hovering = $0 }
    }
}

private struct PCLFabricAPIComponent: View {
    @EnvironmentObject private var store: LauncherStore

    private var state: ModdedInstallState { store.moddedState }
    private var label: String {
        if let selected = state.selectedFabricAPI { return String(selected.versionNumber.split(separator: "+").first ?? Substring(selected.versionNumber)) }
        if state.loadingFabricAPI { return "正在获取版本列表…" }
        if state.fabricAPIError != nil { return "获取失败，请重试" }
        return "未选择"
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 0) {
                Button { store.toggleFabricAPICatalogue() } label: {
                    HStack(spacing: 0) {
                        Text("Fabric API").font(.custom("PCL English", size: 13).weight(.bold))
                            .foregroundStyle(ClassicTheme.text).frame(width: 115, alignment: .leading)
                        PCLResourceImage(name: "Fabric", fallback: "Mod").frame(width: 18, height: 18).padding(.trailing, 7)
                        Text(label).font(.custom("PCL English", size: 13))
                            .foregroundStyle(state.selectedFabricAPI == nil ? ClassicTheme.text : Color.pclBlue)
                        Spacer(minLength: 6)
                        PCLIcon(state.expandedFabricAPI ? "ChevronDown" : "ChevronRight").frame(width: 11, height: 11)
                            .font(.system(size: 10, weight: .medium)).foregroundStyle(ClassicTheme.text.opacity(0.7))
                    }.padding(.horizontal, 16).frame(height: 40).contentShape(Rectangle())
                }.buttonStyle(.plain).accessibilityLabel("选择 Fabric API，\(label)")
                if state.selectedFabricAPI != nil {
                    Button { store.chooseFabricAPI(nil) } label: {
                        PCLIcon("Clear").frame(width: 11, height: 11).foregroundStyle(Color.pclBlue)
                            .frame(width: 28, height: 30).contentShape(Rectangle())
                    }.buttonStyle(.plain).padding(.trailing, 9).help("清除 Fabric API 选择").accessibilityLabel("清除 Fabric API 选择")
                }
            }.disabled(store.isBusy || store.isGameRunning)
            if state.expandedFabricAPI { versions.padding(.horizontal, 12).padding(.bottom, 10) }
        }.pclCardBox()
    }

    @ViewBuilder private var versions: some View {
        Divider().padding(.bottom, 5)
        Button { store.chooseFabricAPI(nil) } label: {
            HStack {
                Text("不安装 Fabric API")
                Spacer()
                if state.selectedFabricAPI == nil && !state.fabricAPIAutoSelection {
                    PCLIcon("Check").frame(width: 12, height: 12).foregroundStyle(Color.pclBlue)
                }
            }.font(.custom("PCL English", size: 12)).foregroundStyle(ClassicTheme.text)
                .padding(.horizontal, 6).frame(height: 32).contentShape(Rectangle())
        }.buttonStyle(.plain).disabled(store.isBusy || store.isGameRunning)
        if state.loadingFabricAPI {
            HStack(spacing: 10) {
                ProgressView().controlSize(.small)
                Text("正在获取 Fabric API 版本列表…")
            }.font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).padding(12)
                .frame(maxWidth: .infinity, alignment: .leading)
        } else if let error = state.fabricAPIError {
            Text(error).font(.custom("PCL English", size: 12)).foregroundStyle(.red).padding(7)
                .frame(maxWidth: .infinity, alignment: .leading)
            Button("重新获取") { store.loadFabricAPICatalogue() }.buttonStyle(PCLActionStyle())
        } else if !state.fabricAPIVersions.isEmpty {
            ScrollView {
                LazyVStack(spacing: 2) {
                    ForEach(state.fabricAPIVersions) { version in
                        PCLFabricAPIVersionRow(version: version, selected: state.selectedFabricAPI?.id == version.id) {
                            store.chooseFabricAPI(version)
                        }
                    }
                }
            }.frame(height: min(238, CGFloat(state.fabricAPIVersions.count) * 35))
                .disabled(store.isBusy || store.isGameRunning)
        } else {
            Text("Fabric API 没有适用于此 Minecraft 版本的可安装版本。")
                .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).padding(12)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

private struct PCLFabricAPIVersionRow: View {
    let version: CommunityVersion
    let selected: Bool
    let action: () -> Void
    @State private var hovering = false

    var body: some View {
        Button(action: action) {
            HStack(spacing: 8) {
                Text(version.versionNumber).font(.custom("PCL English", size: 13)).foregroundStyle(ClassicTheme.text)
                if version.releaseType != "release" { Text("预览版").foregroundStyle(.secondary) }
                Spacer(minLength: 6)
                if selected { PCLIcon("Check").frame(width: 12, height: 12).foregroundStyle(Color.pclBlue) }
            }.font(.custom("PCL English", size: 11)).padding(.horizontal, 8).frame(height: 33).contentShape(Rectangle())
        }.buttonStyle(.plain).background(hovering || selected ? Color.pclBlue.opacity(0.07) : .clear, in: RoundedRectangle(cornerRadius: 3))
            .onHover { hovering = $0 }
    }
}

struct PCLModpackImportSheet: View {
    @EnvironmentObject private var store: LauncherStore
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack(spacing: 10) {
                PCLIcon("Modpack").frame(width: 18, height: 18).foregroundStyle(Color.pclBlue)
                Text("导入整合包").font(.custom("PCL English", size: 19))
                Spacer()
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    if store.moddedState.inspectingModpack {
                        HStack(spacing: 12) {
                            ProgressView().controlSize(.small)
                            Text("正在识别整合包，请稍候…").foregroundStyle(.secondary)
                        }.font(.custom("PCL English", size: 13)).frame(maxWidth: .infinity, minHeight: 115, alignment: .center)
                    } else if let descriptor = store.moddedState.modpackDescriptor {
                        VStack(alignment: .leading, spacing: 12) {
                            Text(descriptor.name).font(.custom("PCL English", size: 16)).foregroundStyle(Color.pclBlue)
                            info("导出格式", descriptor.format.displayName)
                            info("Minecraft", descriptor.minecraftVersion)
                            info("Mod 加载器", descriptor.loaderKind.map { "\($0.displayName) \(descriptor.loaderVersion ?? "")" } ?? "无 / 包内配置")
                            info("包内文件", String(descriptor.fileCount))
                            if descriptor.format == .curseForge {
                                Text("CurseForge 是整合包的导出格式，Mod 加载器以以上识别结果为准。")
                                    .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).lineSpacing(3)
                            }
                            if !descriptor.summary.isEmpty {
                                Text(descriptor.summary).font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).lineSpacing(3)
                            }
                        }.padding(15).pclCardBox()
                        HStack(spacing: 12) {
                            Text("版本名称").font(.custom("PCL English", size: 13)).frame(width: 82, alignment: .leading)
                            TextField("导入后的版本名称", text: $store.moddedState.modpackNameDraft)
                                .textFieldStyle(.roundedBorder).font(.custom("PCL English", size: 13))
                                .accessibilityLabel("整合包版本名称")
                        }
                        if let error = store.modpackNameError { PCLHint(text: error, warning: true) }
                        ForEach(Array(descriptor.warnings.enumerated()), id: \.offset) { _, warning in PCLHint(text: warning, warning: true) }
                        Text("整合包会使用独立游戏目录，配置、模组和存档与其他版本分开保存。")
                            .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).lineSpacing(3)
                    }
                    if let error = store.moddedState.modpackError { PCLHint(text: error, warning: true) }
                    if let archive = store.moddedState.modpackArchive {
                        Text(archive.lastPathComponent).font(.custom("PCL English", size: 11)).foregroundStyle(.secondary).lineLimit(1)
                    }
                }.frame(maxWidth: .infinity, alignment: .leading)
            }.frame(height: store.moddedState.modpackDescriptor == nil ? 160 : 430)
            HStack(spacing: 12) {
                Spacer()
                Button("取消") { store.dismissModpackPreview() }.buttonStyle(PCLActionStyle()).keyboardShortcut(.cancelAction)
                Button("开始导入") { store.importSelectedModpack() }.buttonStyle(PCLActionStyle(highlight: true))
                    .disabled(store.moddedState.modpackDescriptor == nil || store.moddedState.inspectingModpack || store.modpackNameError != nil || store.isBusy || store.isGameRunning)
                    .keyboardShortcut(.defaultAction)
            }
        }.padding(24).frame(width: 560).background(ClassicTheme.panel)
            .onDisappear { store.dismissModpackPreview() }
    }

    private func info(_ label: String, _ value: String) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Text(label).foregroundStyle(.secondary).frame(width: 82, alignment: .leading)
            Text(value).foregroundStyle(ClassicTheme.text).frame(maxWidth: .infinity, alignment: .leading)
        }.font(.custom("PCL English", size: 12))
    }
}


private struct PCLExtraComponent: View {
    @EnvironmentObject private var store: LauncherStore
    let kind: ExtraComponentKind
    private var state: ModdedInstallState { store.moddedState }
    private var selection: ExtraComponentVersion? { store.initialExtraSelection(kind) }
    private var unavailable: String? {
        store.extraComponentUnavailableReason(kind, minecraft: store.currentRemoteSelection?.id ?? "", loader: state.selectedLoader?.kind, optiFine: state.selectedOptiFine)
    }
    private var label: String {
        if let unavailable { return unavailable }
        if let selection { return selection.title }
        if state.extraCatalogue.loading.contains(kind) { return "正在获取版本列表…" }
        if state.extraCatalogue.errors[kind] != nil { return "获取失败，请重试" }
        return "未选择"
    }
    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 0) {
                Button { store.toggleExtraComponentCatalogue(kind) } label: {
                    HStack(spacing: 0) {
                        Text(kind.title).font(.custom("PCL English", size: 13).weight(.bold))
                            .foregroundStyle(ClassicTheme.text).frame(width: 115, alignment: .leading)
                        PCLResourceImage(name: kind.icon, fallback: "Mod").frame(width: 18, height: 18).padding(.trailing, 7)
                        Text(label).font(.custom("PCL English", size: 13)).foregroundStyle(selection == nil ? ClassicTheme.text : Color.pclBlue)
                        Spacer(minLength: 6)
                        PCLIcon(state.extraCatalogue.expanded == kind ? "ChevronDown" : "ChevronRight").frame(width: 11, height: 11)
                            .font(.system(size: 10, weight: .medium)).foregroundStyle(ClassicTheme.text.opacity(0.7))
                    }.padding(.horizontal, 16).frame(height: 40).contentShape(Rectangle())
                }.buttonStyle(.plain).disabled(unavailable != nil)
                    .accessibilityLabel("选择 \(kind.title)，\(label)")
                if selection != nil {
                    Button { store.chooseExtraComponent(nil, kind: kind) } label: {
                        PCLIcon("Clear").frame(width: 11, height: 11).foregroundStyle(Color.pclBlue).frame(width: 28, height: 30)
                    }.buttonStyle(.plain).padding(.trailing, 9).help("清除 \(kind.title) 选择").accessibilityLabel("清除 \(kind.title) 选择")
                }
            }.disabled(store.isBusy || store.isGameRunning)
            if state.extraCatalogue.expanded == kind { versions.padding(.horizontal, 12).padding(.bottom, 10) }
        }.pclCardBox().disabled(store.isBusy || store.isGameRunning)
    }
    @ViewBuilder private var versions: some View {
        Divider().padding(.bottom, 5)
        Button { store.chooseExtraComponent(nil, kind: kind) } label: {
            HStack { Text("不安装 \(kind.title)"); Spacer(); if selection == nil { PCLIcon("Check").frame(width: 12, height: 12).foregroundStyle(Color.pclBlue) } }
                .font(.custom("PCL English", size: 12)).foregroundStyle(ClassicTheme.text).padding(.horizontal, 6).frame(height: 32).contentShape(Rectangle())
        }.buttonStyle(.plain)
        if state.extraCatalogue.loading.contains(kind) {
            HStack(spacing: 10) { ProgressView().controlSize(.small); Text("正在获取 \(kind.title) 版本列表…") }
                .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).padding(12).frame(maxWidth: .infinity, alignment: .leading)
        } else if let error = state.extraCatalogue.errors[kind] {
            Text(error).font(.custom("PCL English", size: 12)).foregroundStyle(.red).padding(7).frame(maxWidth: .infinity, alignment: .leading)
            Button("重新获取") { store.loadExtraComponentCatalogue(kind) }.buttonStyle(PCLActionStyle())
        } else if !store.initialExtraVersions(kind).isEmpty {
            let entries = store.initialExtraVersions(kind)
            ScrollView {
                LazyVStack(spacing: 2) {
                    ForEach(entries) { entry in
                        PCLExtraComponentRow(entry: entry, selected: selection == entry) { store.chooseExtraComponent(entry, kind: kind) }
                    }
                }
            }.frame(height: min(238, CGFloat(entries.count) * 35))
        } else {
            Text("\(kind.title) 没有适用于此 Minecraft 和组件组合的版本。")
                .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).padding(12).frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

private struct PCLExtraComponentRow: View {
    let entry: ExtraComponentVersion
    let selected: Bool
    let action: () -> Void
    @State private var hovering = false
    var body: some View {
        Button(action: action) {
            HStack(spacing: 8) { Text(entry.title); Spacer(minLength: 6); if selected { PCLIcon("Check").frame(width: 12, height: 12).foregroundStyle(Color.pclBlue) } }
                .font(.custom("PCL English", size: 13)).foregroundStyle(ClassicTheme.text).padding(.horizontal, 8).frame(height: 33).contentShape(Rectangle())
        }.buttonStyle(.plain).background(hovering || selected ? Color.pclBlue.opacity(0.07) : .clear, in: RoundedRectangle(cornerRadius: 3))
            .onHover { hovering = $0 }
    }
}
