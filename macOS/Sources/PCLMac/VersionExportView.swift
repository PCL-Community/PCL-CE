import PCLCore
import SwiftUI

struct VersionExportView: View {
    @EnvironmentObject private var store: LauncherStore

    var body: some View {
        VersionExportContent(state: store.versionExportState)
            .task(id: store.versionSettingsTarget?.directory.path) { store.refreshVersionExport() }
    }
}

private struct VersionExportContent: View {
    @EnvironmentObject private var store: LauncherStore
    @ObservedObject var state: VersionExportState

    var body: some View {
        Group {
            if let error = state.error { PCLHint(text: error, warning: true) }
            PCLCard(title: "") {
                HStack(spacing: 16) {
                    Text("整合包名称")
                    TextField(store.versionSettingsTarget?.id ?? "", text: $state.options.name).textFieldStyle(.roundedBorder)
                    Text("整合包版本").padding(.leading, 12)
                    TextField("1.0.0", text: $state.options.version).textFieldStyle(.roundedBorder).frame(width: 110)
                }.font(.custom("PCL English", size: 13)).disabled(state.isExporting)
            }
            PCLCard(title: "导出内容列表") {
                VStack(alignment: .leading, spacing: 5) {
                    Toggle("游戏本体", isOn: .constant(true)).disabled(true)
                    option("游戏本体设置", detail: "键位、音量、视频设置等", value: $state.options.includeOptions).padding(.leading, 30)
                    option("Mod", detail: "模组", value: $state.options.includeMods)
                    if state.options.includeMods {
                        option("已禁用的 Mod", value: $state.options.includeDisabledMods).padding(.leading, 30)
                        option("整合包重要数据", detail: "脚本文件、内置资源包、数据包等", value: $state.options.includePackData).padding(.leading, 30)
                        option("Mod 设置", value: $state.options.includeConfig).padding(.leading, 30)
                    }
                    option("资源包", detail: "纹理包/材质包", value: $state.options.includeResourcePacks)
                    if state.options.includeResourcePacks {
                        entries(state.resourcePacks, selection: $state.options.selectedResourcePacks)
                    }
                    option("光影包", value: $state.options.includeShaderPacks)
                    if state.options.includeShaderPacks {
                        option("光影包设置", detail: "已选光影包的自定义设置", value: $state.options.includeShaderPackSettings).padding(.leading, 30)
                        entries(state.shaderPacks, selection: $state.options.selectedShaderPacks)
                    }
                    option("截图", value: $state.options.includeScreenshots)
                    option("导出的结构", detail: "schematics 文件夹", value: $state.options.includeStructures)
                    option("录像回放", detail: "Replay Mod 的录像文件", value: $state.options.includeReplays)
                    option("单人游戏存档", detail: "世界/地图", value: $state.options.includeSaves)
                    if state.options.includeSaves {
                        entries(state.saves, selection: $state.options.selectedSaves)
                    }
                    option("协议", detail: "Licence 文件", value: $state.options.includeLicences)
                    option("多人游戏服务器列表", value: $state.options.includeServers)
                    HStack { Spacer(); Button("重置") { store.resetVersionExportOptions() }.buttonStyle(.plain).foregroundStyle(Color.pclBlue) }
                }
                .font(.custom("PCL English", size: 13)).toggleStyle(.checkbox)
                .disabled(state.isExporting)
            }
            PCLCard(title: "高级选项", collapsible: true, expanded: false) {
                VStack(alignment: .leading, spacing: 12) {
                    HStack(spacing: 20) {
                        Text("保存类型")
                        PCLComboBox(title: "保存类型", selection: $state.options.format,
                                    options: VersionPackExportFormat.allCases.map { PCLComboBoxOption(title: $0.displayName, value: $0) })
                            .frame(maxWidth: .infinity).frame(height: 28)
                    }
                    Text(state.options.format == .completeZip
                         ? "完整版本 ZIP 包含游戏本体及当前 Mac 所需的支持文件。Mod、资源包、光影包直接打包。"
                         : "Modrinth 格式会直接打包选中的资源文件，导入时再安装 Minecraft 与加载器。")
                        .foregroundStyle(.secondary).font(.custom("PCL English", size: 12))
                    if state.options.format == .modrinth {
                        PCLHint(text: "受 Modrinth 整合包规范限制，导出的整合包中不会包含 OptiFine。", warning: true)
                    }
                    HStack(spacing: 20) {
                        Button("读取配置") { store.readVersionExportConfiguration() }.buttonStyle(PCLActionStyle(highlight: true))
                        Button("保存配置") { store.saveVersionExportConfiguration() }.buttonStyle(PCLActionStyle())
                    }
                }.font(.custom("PCL English", size: 13)).disabled(state.isExporting)
            }
            if state.isExporting {
                PCLCard(title: "导出整合包") {
                    ProgressView(value: state.progress?.fraction ?? 0)
                    Text(state.progress?.currentFile ?? "准备导出内容")
                        .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).lineLimit(2)
                    if let progress = state.progress, progress.totalFiles > 0 {
                        Text("\(progress.completedFiles) / \(progress.totalFiles) 个文件")
                            .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary)
                    }
                    Button("取消导出") { store.cancelVersionExport() }.buttonStyle(PCLActionStyle())
                }
            } else if let result = state.result {
                PCLCard(title: "导出完成") {
                    Text(result.url.lastPathComponent).font(.custom("PCL English", size: 13))
                    Button("在 Finder 中显示") { store.revealVersionPackExport() }.buttonStyle(PCLActionStyle())
                }
            }
            HStack {
                Spacer()
                Button { store.startVersionPackExport() } label: { PCLIconLabel("开始导出", icon: "Export") }
                    .buttonStyle(PCLActionStyle(highlight: true)).disabled(store.isBusy || store.isGameRunning)
                Spacer()
            }.padding(.vertical, 10)
        }
    }

    private func option(_ title: String, detail: String = "", value: Binding<Bool>) -> some View {
        Toggle(isOn: value) {
            HStack(spacing: 12) {
                Text(title)
                if !detail.isEmpty { Text(detail).foregroundStyle(.secondary).font(.custom("PCL English", size: 12)) }
            }
        }.frame(minHeight: 26)
    }

    private func entries(_ items: [VersionExportSelectableEntry], selection: Binding<[String]?>) -> some View {
        ForEach(items) { item in
            Toggle(item.name, isOn: Binding(get: {
                guard let values = selection.wrappedValue else { return true }
                return values.contains(item.relativePath) || values.contains((item.relativePath as NSString).lastPathComponent)
            }, set: { enabled in
                let old = selection.wrappedValue
                var values = Set(items.filter {
                    old == nil || old!.contains($0.relativePath) || old!.contains(($0.relativePath as NSString).lastPathComponent)
                }.map(\.relativePath))
                if enabled { values.insert(item.relativePath) } else { values.remove(item.relativePath) }
                selection.wrappedValue = values.sorted()
            })).frame(minHeight: 26).padding(.leading, 30)
        }
    }
}
