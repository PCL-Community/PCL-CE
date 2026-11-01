import PCLCore
import SwiftUI

struct VersionSettingsView: View {
    @EnvironmentObject private var store: LauncherStore
    @State private var widthText = "1100"
    @State private var heightText = "700"
    @State private var synchronizingWindowFields = false

    var body: some View {
        Group {
            if let version = store.versionSettingsTarget {
                if let error = store.profileSaveError ?? store.versionWindowInputError { PCLHint(text: error, warning: true) }
                switch store.versionSettingsTab {
                case .overview: overview(version)
                case .settings: launchSettings(version)
                case .modify: VersionModifyView()
                case .mods: VersionModView()
                case .export: VersionExportView()
                }
            } else {
                PCLCard(title: "版本设置") {
                    if store.scanning || store.versionSettingsTargetID == nil {
                        HStack(spacing: 12) { ProgressView().controlSize(.small); Text("正在获取版本信息") }
                    } else { Text("该版本不存在，请返回版本选择重新选择。") }
                }
            }
        }
        .onAppear { synchronizeWindowFields() }
        .onChange(of: store.versionSettingsTarget?.id) { _, _ in synchronizeWindowFields() }
        .onChange(of: store.versionSettingsDraft.width) { _, _ in synchronizeWindowFields() }
        .onChange(of: store.versionSettingsDraft.height) { _, _ in synchronizeWindowFields() }
        .onChange(of: widthText) { _, _ in commitWindowFields() }
        .onChange(of: heightText) { _, _ in commitWindowFields() }
        .onChange(of: store.versionSettingsDraft.windowConfigurationMode) { _, mode in
            if mode == "global" { store.versionWindowInputError = nil; synchronizeWindowFields() }
            else { commitWindowFields() }
        }
        .onChange(of: store.versionSettingsDraft) { _, _ in store.saveVersionSettings() }
    }

    private func overview(_ version: InstalledVersion) -> some View {
        Group {
            HStack(spacing: 10) {
                versionIcon(version).frame(width: 32, height: 32)
                VStack(alignment: .leading, spacing: 4) {
                    HStack(alignment: .firstTextBaseline, spacing: 6) {
                        Text(version.id).font(.custom("PCL English", size: 14)).foregroundStyle(ClassicTheme.text)
                        if !store.versionSettingsDraft.displayDescription.isEmpty {
                            Text("|  \(store.versionDisplayName(for: version))").font(.custom("PCL English", size: 12)).foregroundStyle(.tertiary)
                        }
                    }.lineLimit(1)
                    Text(store.versionSettingsDraft.displayDescription.isEmpty ? store.versionDisplayName(for: version) : store.versionSettingsDraft.displayDescription)
                        .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).lineLimit(1)
                }
                Spacer(minLength: 0)
            }.frame(height: 42).padding(.horizontal, 10).padding(.vertical, 7).pclCardBox()

            PCLCard(title: "个性化") {
                VStack(alignment: .leading, spacing: 12) {
                    HStack(spacing: 25) {
                        nameLabel("图标")
                        PCLComboBox(title: "图标", selection: Binding(get: { store.versionSettingsDraft.iconName }, set: { value in
                            if value == "PCL/Logo.png" { store.chooseVersionIcon() }
                            else { store.versionSettingsDraft.iconName = value }
                        }), options: [
                            PCLComboBoxOption(title: "自动", value: ""),
                            PCLComboBoxOption(title: "自定义…", value: "PCL/Logo.png")
                        ] + Self.icons.map { PCLComboBoxOption(title: $0.title, value: $0.value) })
                        .frame(maxWidth: .infinity).frame(height: 28)
                        .contextMenu { Button("选择自定义图标…") { store.chooseVersionIcon() } }
                    }
                    HStack(spacing: 25) {
                        nameLabel("分类")
                        PCLComboBox(title: "分类", selection: $store.versionSettingsDraft.category, options: [
                            .init(title: "自动", value: "auto"),
                            .init(title: "从版本列表中隐藏", value: "hidden"),
                            .init(title: "可安装 Mod 的版本", value: "modded"),
                            .init(title: "常规版本", value: "regular"),
                            .init(title: "不常用版本", value: "rare"),
                            .init(title: "愚人节版本", value: "fool")
                        ]).frame(maxWidth: .infinity).frame(height: 28)
                    }
                    actionGrid {
                        Button("修改版本名") { store.renameVersion() }
                        Button("修改版本描述") { store.editVersionDescription() }
                        Button(store.versionSettingsDraft.isFavorite ? "从收藏夹中移除" : "加入收藏夹") { store.toggleVersionFavorite() }
                    }.disabled(store.isBusy || store.isGameRunning)
                }.font(.custom("PCL English", size: 13))
            }

            PCLCard(title: "快捷方式") {
                actionGrid {
                    Button("版本文件夹") { store.revealVersionSubfolder("") }
                    Button("存档文件夹") { store.revealVersionSubfolder("saves") }
                    if version.canInstallMods || store.versionSettingsDraft.category == "modded" {
                        Button("Mod 文件夹") { store.revealVersionSubfolder("mods") }
                    }
                    Button("截图文件夹") { store.revealVersionSubfolder("screenshots") }
                }
            }

            PCLCard(title: "高级管理") {
                actionGrid {
                    Button("导出启动脚本") { store.exportVersionLaunchScript() }
                    Button("补全文件") { store.repair(version) }.help("校验版本依赖文件，并重新下载缺失或校验失败的文件。")
                    Button("删除版本") { store.deleteVersion() }.buttonStyle(PCLActionStyle(destructive: true))
                }.disabled(store.isBusy || store.isGameRunning)
            }
        }
    }

    @ViewBuilder private func versionIcon(_ version: InstalledVersion) -> some View {
        if let image = store.customVersionIcon(for: version, iconName: store.versionSettingsDraft.iconName) {
            Image(nsImage: image).resizable().scaledToFit()
        } else {
            PCLResourceImage(name: store.versionSettingsDraft.iconName.isEmpty ? store.defaultVersionIcon(for: version) : store.versionSettingsDraft.iconName, fallback: "GameVersion")
        }
    }

    private func actionGrid<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        LazyVGrid(columns: [GridItem(.adaptive(minimum: 140), spacing: 20)], alignment: .leading, spacing: 13, content: content)
            .buttonStyle(PCLActionStyle()).padding(.top, 2)
    }

    private func launchSettings(_ version: InstalledVersion) -> some View {
        Group {
            PCLHint(text: "这些设置只对该游戏版本生效，不影响其他版本。")
            PCLCard(title: "启动选项") {
                VStack(alignment: .leading, spacing: 12) {
                    settingRow("版本隔离") {
                        PCLComboBox(title: "版本隔离", selection: $store.versionSettingsDraft.isolateGameDirectory, options: [
                            .init(title: "开启", value: true), .init(title: "关闭", value: false)
                        ]).frame(maxWidth: .infinity).frame(height: 28)
                            .help("开启后，当前版本独立保存存档、Mod 等文件。")
                    }
                    settingRow("游戏窗口标题") {
                        TextField("跟随全局设置", text: .constant("")).textFieldStyle(.roundedBorder).disabled(true)
                            .help("macOS 游戏窗口标题暂不支持修改。")
                    }
                    settingRow("自定义信息") {
                        TextField("跟随全局设置", text: $store.versionSettingsDraft.customInfo).textFieldStyle(.roundedBorder)
                            .help("显示在游戏主界面与调试信息中；Minecraft 26.1 已移除此设置。")
                    }
                    settingRow("Java") {
                        PCLComboBox(title: "Java", selection: modeBinding(\.javaMode, effective: \.javaConfigurationMode), options: [
                            .init(title: "跟随全局设置", value: "global"),
                            .init(title: "自动选择", value: "auto"),
                            .init(title: "自动选择指定版本的 Java", value: "range"),
                            .init(title: "使用版本文件夹中的 Java", value: "folder"),
                            .init(title: "使用指定的 Java", value: "custom")
                        ]).frame(maxWidth: .infinity).frame(height: 28)
                    }
                    if store.versionSettingsDraft.javaConfigurationMode == "range" {
                        settingRow("") {
                            TextField("版本区间…", text: $store.versionSettingsDraft.javaVersionRange).textFieldStyle(.roundedBorder)
                                .help("例如 [17.0.1, 25.0)。从已安装的 Java 中选择；方括号包含边界，圆括号不包含。")
                        }
                    }
                    if store.versionSettingsDraft.javaConfigurationMode == "custom" {
                        settingRow("") {
                            PCLComboBox(title: "指定 Java", selection: $store.versionSettingsDraft.javaPath, options: [
                                PCLComboBoxOption(title: "请选择 Java", value: "")
                            ] + store.javaRuntimes.map { PCLComboBoxOption(title: $0.displayName, value: $0.executable.path) })
                            .frame(maxWidth: .infinity).frame(height: 28)
                            Button { Task { await store.refreshLocal() } } label: { PCLIcon("Refresh").frame(width: 14, height: 14) }
                                .buttonStyle(PCLActionStyle()).help("重新搜索 Java").accessibilityLabel("重新搜索 Java")
                            Button("导入") { store.chooseVersionJava() }.buttonStyle(PCLActionStyle())
                        }
                    }
                    settingRow("") {
                        VersionJavaPreview(selection: VersionJavaSelection(version: version, profile: store.versionSettingsDraft,
                                                                           globalJavaPath: store.preferences.javaPath, runtimes: store.javaRuntimes),
                                           scanning: store.scanning)
                    }.padding(.top, -4)
                }.font(.custom("PCL English", size: 13))
            }

            PCLCard(title: "内存分配") {
                VStack(alignment: .leading, spacing: 12) {
                    Picker("内存配置", selection: modeBinding(\.memoryMode, effective: \.memoryConfigurationMode)) {
                        Text("跟随全局设置").tag("global")
                        Text("自动配置").tag("auto")
                        Text("自定义").tag("custom")
                    }.pickerStyle(.radioGroup).labelsHidden()
                    HStack(spacing: 14) {
                        Slider(value: Binding(get: { Double(max(256, min(store.memoryLimit, store.versionSettingsDraft.memoryMB))) },
                                              set: { store.versionSettingsDraft.memoryMB = Int($0) }),
                               in: 256...Double(max(256, store.memoryLimit)), step: 256)
                            .disabled(store.versionSettingsDraft.memoryConfigurationMode != "custom")
                        Text(String(format: "%.1f GB", Double(store.effectiveVersionMemoryMB(for: version, profile: store.versionSettingsDraft)) / 1024))
                            .font(.custom("PCL English", size: 16)).foregroundStyle(Color.pclBlue).frame(width: 65, alignment: .trailing)
                    }
                    memoryDisplay(version)
                }.font(.custom("PCL English", size: 13))
            }

            PCLCard(title: "服务器") {
                VStack(spacing: 12) {
                    settingRow("登录方式") { Text("仅离线登录").frame(maxWidth: .infinity, alignment: .leading).foregroundStyle(.secondary) }
                    settingRow("自动进入服务器") {
                        TextField("", text: $store.versionSettingsDraft.serverAddress).textFieldStyle(.roundedBorder)
                            .help("填写服务器地址，可用英文冒号附加端口，例如 example.org:25565。离线账户无法连接要求正版验证的服务器。")
                    }
                }.font(.custom("PCL English", size: 13))
            }

            PCLCard(title: "高级选项", collapsible: true, expanded: false) {
                VStack(alignment: .leading, spacing: 12) {
                    settingRow("Java 虚拟机参数") {
                        PCLComboBox(title: "虚拟机参数来源", selection: modeBinding(\.jvmArgumentsMode, effective: \.jvmConfigurationMode), options: [
                            .init(title: "跟随全局设置", value: "global"), .init(title: "自定义", value: "custom")
                        ]).frame(maxWidth: .infinity).frame(height: 28)
                    }
                    if store.versionSettingsDraft.jvmConfigurationMode == "custom" {
                        settingRow("") {
                            TextField("Java 虚拟机参数", text: $store.versionSettingsDraft.extraJVMArguments, axis: .vertical)
                                .lineLimit(2...4).textFieldStyle(.roundedBorder)
                        }
                    }
                    settingRow("游戏参数") {
                        TextField("跟随全局设置", text: $store.versionSettingsDraft.extraGameArguments, axis: .vertical)
                            .lineLimit(1...3).textFieldStyle(.roundedBorder)
                    }
                    settingRow("内存管理") {
                        PCLComboBox(title: "内存管理", selection: $store.versionSettingsDraft.garbageCollector, options: [
                            .init(title: "跟随全局设置", value: "global"),
                            .init(title: "尽量使用 ZGC", value: "zgc"),
                            .init(title: "尽量使用分代 ZGC", value: "generational-zgc"),
                            .init(title: "标准 G1GC", value: "g1"),
                            .init(title: "调优 G1GC", value: "g1tuned"),
                            .init(title: "不指定（可自定义）", value: "none")
                        ]).frame(maxWidth: .infinity).frame(height: 28)
                    }
                    settingRow("游戏窗口大小") {
                        PCLComboBox(title: "窗口大小来源", selection: modeBinding(\.windowMode, effective: \.windowConfigurationMode), options: [
                            .init(title: "跟随全局设置", value: "global"), .init(title: "自定义", value: "custom")
                        ]).frame(maxWidth: .infinity).frame(height: 28)
                    }
                    if store.versionSettingsDraft.windowConfigurationMode == "custom" {
                        settingRow("") {
                            TextField("宽度", text: $widthText).textFieldStyle(.roundedBorder).frame(width: 72).onSubmit { commitWindowFields() }
                            Text("×")
                            TextField("高度", text: $heightText).textFieldStyle(.roundedBorder).frame(width: 72).onSubmit { commitWindowFields() }
                            Button("应用") { commitWindowFields() }.buttonStyle(PCLActionStyle())
                            Spacer(minLength: 0)
                        }
                    }
                    settingRow("启动前执行命令") {
                        TextField("", text: .constant("")).textFieldStyle(.roundedBorder).disabled(true)
                            .help("此 Windows 启动命令选项尚未移植。")
                    }
                }.font(.custom("PCL English", size: 13))
            }
            HStack {
                Spacer()
                Button("全局设置") { store.saveVersionSettings(); store.page = .settings }.buttonStyle(PCLActionStyle())
                Spacer()
            }.padding(.top, 3)
        }
    }

    private func memoryDisplay(_ version: InstalledVersion) -> some View {
        let total = Double(ProcessInfo.processInfo.physicalMemory) / 1_048_576
        let available = Double(store.versionAvailableMemoryMB)
        let game = Double(store.effectiveVersionMemoryMB(for: version, profile: store.versionSettingsDraft))
        let used = max(0, total - available)
        return VStack(spacing: 5) {
            HStack {
                Text("已使用内存"); Spacer(); Text("游戏分配")
            }.font(.custom("PCL English", size: 11)).foregroundStyle(.secondary)
            GeometryReader { proxy in
                HStack(spacing: 0) {
                    Color.gray.opacity(0.45).frame(width: proxy.size.width * min(1, used / total))
                    Color.pclBlue.opacity(0.5).frame(width: proxy.size.width * min(max(0, 1 - used / total), game / total))
                    Color.pclBlue.opacity(0.15)
                }
            }.frame(height: 6).clipShape(Capsule())
            HStack {
                Text("\(String(format: "%.1f", used / 1024)) / \(String(format: "%.1f", total / 1024)) GB")
                Spacer(); Text(String(format: "%.1f GB", game / 1024))
            }.font(.custom("PCL English", size: 14)).foregroundStyle(ClassicTheme.text)
        }.padding(.top, 1)
    }

    private func settingRow<Content: View>(_ title: String, @ViewBuilder content: () -> Content) -> some View {
        HStack(spacing: 25) { nameLabel(title); HStack(spacing: 8, content: content).frame(maxWidth: .infinity, alignment: .leading) }
    }

    private func modeBinding(_ key: WritableKeyPath<VersionProfileSettings, String?>, effective: KeyPath<VersionProfileSettings, String>) -> Binding<String> {
        Binding(get: { store.versionSettingsDraft[keyPath: effective] }, set: { store.versionSettingsDraft[keyPath: key] = $0 })
    }

    private func nameLabel(_ text: String) -> some View { Text(text).frame(width: 110, alignment: .leading) }

    private func synchronizeWindowFields() {
        synchronizingWindowFields = true
        widthText = String(store.versionSettingsDraft.width)
        heightText = String(store.versionSettingsDraft.height)
        store.versionWindowInputError = nil
        DispatchQueue.main.async { synchronizingWindowFields = false }
    }

    private func commitWindowFields() {
        guard !synchronizingWindowFields else { return }
        guard store.versionSettingsDraft.windowConfigurationMode == "custom" else { store.versionWindowInputError = nil; return }
        guard let width = Int(widthText), let height = Int(heightText), (320...16_384).contains(width), (240...16_384).contains(height) else {
            store.versionWindowInputError = "游戏窗口宽度应为 320–16384，高度应为 240–16384。"
            return
        }
        store.versionWindowInputError = nil
        var settings = store.versionSettingsDraft
        settings.width = width; settings.height = height
        if store.versionSettingsDraft != settings { store.versionSettingsDraft = settings }
    }

    private static let icons: [(title: String, value: String)] = [
        ("圆石", "CobbleStone"), ("命令方块", "CommandBlock"), ("金块", "GoldBlock"),
        ("草方块", "Grass"), ("土径", "GrassPath"), ("铁砧", "Anvil"),
        ("红石块", "RedstoneBlock"), ("红石灯（开）", "RedstoneLampOn"), ("红石灯（关）", "RedstoneLampOff"),
        ("鸡蛋", "Egg"), ("布料（Fabric）", "Fabric"), ("狐狸（NeoForge）", "NeoForge")
    ]
}

private struct VersionJavaPreview: View {
    let selection: VersionJavaSelection
    let scanning: Bool
    @State private var resolvedSelection: VersionJavaSelection?
    @State private var message = "正在查找 Java……"
    @State private var warning = false

    private struct Request: Hashable {
        let selection: VersionJavaSelection
        let scanning: Bool
    }

    var body: some View {
        let pending = scanning || resolvedSelection != selection
        let hasWarning = !pending && warning
        Text(pending ? "正在查找 Java……" : message)
            .font(.custom("PCL English", size: 13)).lineSpacing(1)
            .foregroundStyle(hasWarning ? Color(red: 0xbf / 255, green: 0x0b / 255, blue: 0x0b / 255)
                                       : Color(red: 0x0f / 255, green: 0x64 / 255, blue: 0xb8 / 255))
            .padding(.vertical, 9).padding(.leading, 15).padding(.trailing, 12)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(hasWarning ? Color(red: 1, green: 0xdd / 255, blue: 0xdf / 255)
                                   : Color(red: 0xd9 / 255, green: 0xec / 255, blue: 1))
            .overlay(alignment: .leading) {
                Rectangle().fill(hasWarning ? Color(red: 0xd8 / 255, green: 0x29 / 255, blue: 0x29 / 255)
                                            : Color(red: 0x11 / 255, green: 0x72 / 255, blue: 0xd4 / 255))
                    .frame(width: 3)
            }
            .clipShape(RoundedRectangle(cornerRadius: 2))
            .textSelection(.enabled)
            .task(id: Request(selection: selection, scanning: scanning)) {
                guard !scanning else { return }
                let snapshot = selection
                let result = await Task.detached(priority: .userInitiated) { () -> (String, Bool) in
                    do { return ("将会使用：\(try snapshot.resolve().displayName)", false) }
                    catch { return (error.localizedDescription, true) }
                }.value
                guard !Task.isCancelled else { return }
                message = result.0
                warning = result.1
                resolvedSelection = snapshot
            }
    }
}
