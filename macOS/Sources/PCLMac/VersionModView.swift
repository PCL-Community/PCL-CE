import AppKit
import PCLCore
import SwiftUI

struct VersionModView: View {
    @EnvironmentObject private var store: LauncherStore
    var body: some View {
        Group {
            if let version = store.versionSettingsTarget,
               version.canInstallMods || store.profileSettings(for: version).category == "modded" {
                VersionModContent(state: store.versionModState).environmentObject(store)
            } else {
                VStack(spacing: 15) {
                    Text("该版本不可使用 Mod").font(.custom("PCL English", size: 19)).foregroundStyle(Color.pclBlue)
                    Rectangle().fill(Color.pclBlue).frame(height: 2)
                    Text("你需要先安装 Forge、Fabric 等 Mod 加载器才能使用 Mod，请在下载页面安装这些版本。\n如果你已经安装过了 Mod 加载器，那么你很可能选择了错误的版本，请点击版本选择按钮切换版本。")
                        .font(.custom("PCL English", size: 13)).lineSpacing(5)
                    HStack(spacing: 15) {
                        Button("转到下载页面") { store.showVanillaDownloads() }.buttonStyle(PCLActionStyle(highlight: true))
                        Button("版本选择") { store.page = .library }.buttonStyle(PCLActionStyle())
                    }
                }.padding(20).pclCardBox().padding(.vertical, 70)
            }
        }
    }
}

private struct VersionModContent: View {
    @EnvironmentObject private var store: LauncherStore
    @ObservedObject var state: VersionModState
    private var blocked: Bool { store.isBusy || store.isGameRunning || state.loading }

    var body: some View {
        VStack(spacing: 15) {
            if let error = state.error { PCLHint(text: error, warning: true) }
            if state.loading {
                HStack { ProgressView().controlSize(.small); Text("正在加载 Mod 列表") }
                    .font(.custom("PCL English", size: 13)).frame(maxWidth: .infinity).padding(20).pclCardBox()
            } else if state.entries.isEmpty && state.error == nil {
                empty
            } else {
                PCLSearchField(text: $state.search, prompt: "搜索 Mod 名称 / 描述 / 标签")
                toolbar
                VStack(spacing: 0) {
                    HStack(spacing: 18) {
                        filterButton(.all, count: state.entries.count)
                        if state.entries.contains(where: { !$0.enabled }) {
                            filterButton(.enabled, count: state.entries.filter(\.enabled).count)
                            filterButton(.disabled, count: state.entries.filter { !$0.enabled }.count)
                        }
                        Spacer()
                        Button { store.refreshVersionMods() } label: { PCLIcon("Refresh").frame(width: 14, height: 14) }
                            .buttonStyle(.plain).foregroundStyle(Color.pclBlue).help("刷新 Mod 列表").accessibilityLabel("刷新 Mod 列表")
                    }.frame(height: 28).padding(.bottom, 7)
                    if state.visible.isEmpty {
                        Text("没有找到符合条件的 Mod").foregroundStyle(.secondary).padding(.vertical, 15)
                    } else {
                        LazyVStack(spacing: 0) {
                            ForEach(state.visible) { file in VersionLocalModRow(file: file, selected: state.selected.contains(file.id)).environmentObject(store) }
                        }
                    }
                }.padding(.horizontal, 18).padding(.top, 13).padding(.bottom, 22).pclCardBox()
                if !state.selected.isEmpty { selectionBar }
            }
        }
        .task(id: store.versionModTargetKey) { store.refreshVersionMods(clearSearch: true) }
        .onChange(of: state.search) { _, _ in state.selected.formIntersection(Set(state.visible.map(\.id))) }
        .onChange(of: state.filter) { _, _ in state.selected.formIntersection(Set(state.visible.map(\.id))) }
    }

    private var toolbar: some View {
        HStack(spacing: 15) {
            Button("打开文件夹") { store.revealVersionModFolder() }.buttonStyle(PCLActionStyle(highlight: true))
            Button("从文件安装") { store.importVersionMods() }.buttonStyle(PCLActionStyle()).disabled(blocked)
            Button("下载新 Mod") { store.downloadVersionMods() }.buttonStyle(PCLActionStyle()).disabled(blocked)
            Button(Set(state.visible.map(\.id)).isSubset(of: state.selected) ? "取消全选" : "全选") { store.selectAllVersionMods() }
                .buttonStyle(PCLActionStyle()).disabled(blocked || state.visible.isEmpty)
            Spacer(minLength: 0)
        }.padding(15).pclCardBox()
    }

    private var empty: some View {
        VStack(spacing: 15) {
            Text("尚未安装 Mod").font(.custom("PCL English", size: 19)).foregroundStyle(Color.pclBlue)
            Rectangle().fill(Color.pclBlue).frame(height: 2)
            Text("你可以下载新的 Mod，或者从已经下载好的文件安装 Mod。\n如果你已经安装了 Mod，可能是版本隔离设置有误，请在设置中调整版本隔离选项。")
                .font(.custom("PCL English", size: 13)).frame(maxWidth: .infinity, alignment: .leading).lineSpacing(5)
            HStack(spacing: 15) {
                Button("下载 Mod") { store.downloadVersionMods() }.buttonStyle(PCLActionStyle(highlight: true))
                Button("从文件安装 Mod") { store.importVersionMods() }.buttonStyle(PCLActionStyle())
                Button("打开 Mod 文件夹") { store.revealVersionModFolder() }.buttonStyle(PCLActionStyle())
            }.disabled(blocked)
        }.padding(20).pclCardBox().padding(.vertical, 55)
    }

    private func filterButton(_ filter: VersionModState.Filter, count: Int) -> some View {
        Button { state.filter = filter } label: {
            Text("\(filter.rawValue) (\(count))").font(.custom("PCL English", size: 13))
                .foregroundStyle(state.filter == filter ? Color.pclBlue : ClassicTheme.text)
                .padding(.bottom, 5).overlay(alignment: .bottom) { if state.filter == filter { Rectangle().fill(Color.pclBlue).frame(height: 2) } }
        }.buttonStyle(.plain)
    }

    private var selectionBar: some View {
        VStack(spacing: 10) {
            Text("已选择 \(state.selected.count) 个文件").font(.custom("PCL English", size: 13)).foregroundStyle(Color.pclBlue)
            HStack(spacing: 16) {
                Button("启用") { store.setVersionModsEnabled(true) }.disabled(blocked || !state.selectedEntries.contains(where: { !$0.enabled }))
                Button("禁用") { store.setVersionModsEnabled(false) }.disabled(blocked || !state.selectedEntries.contains(where: \.enabled))
                Button("删除") { store.deleteVersionMods() }.disabled(blocked)
                Button("取消选择") { state.selected = [] }
            }.buttonStyle(PCLActionStyle())
        }.padding(12).pclCardBox()
    }
}

private struct VersionLocalModRow: View {
    @EnvironmentObject private var store: LauncherStore
    let file: LocalModFile
    let selected: Bool
    @State private var hovering = false

    var body: some View {
        HStack(spacing: 7) {
            Button { store.toggleVersionModSelection(file) } label: {
                HStack(spacing: 7) {
                    ZStack(alignment: .bottomTrailing) {
                        if let data = file.iconData, let image = NSImage(data: data) {
                            Image(nsImage: image).resizable().scaledToFit().frame(width: 34, height: 34)
                                .clipShape(RoundedRectangle(cornerRadius: 6))
                        } else {
                            if let image = PCLSourceIcons.image("DefaultMod") {
                                Image(nsImage: image).renderingMode(.original).resizable().interpolation(.high).scaledToFit().frame(width: 34, height: 34)
                                    .clipShape(RoundedRectangle(cornerRadius: 6))
                            } else {
                                PCLIcon("GameVersion").frame(width: 34, height: 34)
                            }
                        }
                        if !file.enabled, let image = PCLSourceIcons.image("ModDisabled") {
                            Image(nsImage: image).renderingMode(.original).resizable().interpolation(.high).scaledToFit()
                                .frame(width: 20, height: 20).offset(x: 5, y: 3)
                        }
                    }.frame(width: 34, height: 34)
                    VStack(alignment: .leading, spacing: 3) {
                        HStack(spacing: 7) {
                            Text(file.name).font(.custom("PCL English", size: 14)).foregroundStyle(selected ? Color.pclBlue.opacity(file.enabled ? 1 : 0.55) : file.enabled ? ClassicTheme.text : Color.secondary)
                            if !file.version.isEmpty { Text(file.version).font(.custom("PCL English", size: 12)).foregroundStyle(.tertiary) }
                        }.lineLimit(1)
                        Text(file.error ?? (file.summary.isEmpty ? file.filename : file.summary))
                            .font(.custom("PCL English", size: 12)).foregroundStyle(file.error == nil ? Color.secondary : Color.red.opacity(0.8)).lineLimit(1)
                    }
                    Spacer(minLength: 0)
                }.frame(maxWidth: .infinity, alignment: .leading).frame(height: 44).contentShape(Rectangle())
            }.buttonStyle(.plain)
                .accessibilityLabel("\(file.name)，\(file.version)，\(file.enabled ? "已启用" : "已禁用")")
                .accessibilityAction(named: Text("详情")) { store.showVersionModDetails(file) }
                .accessibilityAction(named: Text(file.enabled ? "禁用" : "启用")) { store.setVersionModsEnabled(!file.enabled, files: [file]) }
                .accessibilityAction(named: Text("删除")) { store.deleteVersionMods([file]) }
            if hovering {
                HStack(spacing: 0) {
                    action("Info", "详情") { store.showVersionModDetails(file) }
                    action("OpenFile", "打开文件位置") { NSWorkspace.shared.activateFileViewerSelecting([file.url]) }
                    action(file.enabled ? "Disable" : "Enable", file.enabled ? "禁用" : "启用") { store.setVersionModsEnabled(!file.enabled, files: [file]) }
                    action("Delete", "删除") { store.deleteVersionMods([file]) }
                }.disabled(store.isBusy || store.isGameRunning)
            }
        }
        .overlay { PCLVersionRowSecondaryClick(enabled: true) { store.showVersionModDetails(file) } }
        .padding(.horizontal, 5).background(selected || hovering ? Color.pclBlue.opacity(0.07) : .clear, in: RoundedRectangle(cornerRadius: 3))
        .overlay(alignment: .leading) {
            if selected { RoundedRectangle(cornerRadius: 2).fill(Color.pclBlue).frame(width: 5, height: 32).offset(x: -3) }
        }
        .onHover { hovering = $0 }
    }
    private func action(_ symbol: String, _ title: String, action: @escaping () -> Void) -> some View {
        Button(action: action) { PCLIcon(symbol).frame(width: 15, height: 15).foregroundStyle(Color.pclBlue.opacity(0.75)).frame(width: 25, height: 25).contentShape(Rectangle()) }
            .buttonStyle(.plain).help(title).accessibilityLabel(title)
    }
}
