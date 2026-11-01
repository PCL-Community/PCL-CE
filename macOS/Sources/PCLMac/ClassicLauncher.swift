import AppKit
import PCLCore
import SwiftUI

enum ClassicTheme {
    static let blue = Color(red: 0.08, green: 0.46, blue: 0.81)
    static let text = Color(red: 0.35, green: 0.37, blue: 0.39)
    static let panel = Color(red: 0.993, green: 0.993, blue: 1)
    static let background = LinearGradient(
        colors: [Color(red: 0.85, green: 0.89, blue: 0.97), Color(red: 0.80, green: 0.93, blue: 0.96)],
        startPoint: .bottomLeading, endPoint: .topTrailing)
}

struct ClassicHeader: View {
    static let height: CGFloat = 60
    @EnvironmentObject private var store: LauncherStore

    var body: some View {
        ZStack {
            HStack {
                if hasInnerTitle {
                    Button(action: goBack) {
                        HStack(spacing: 14) { PCLIcon("ArrowLeft").frame(width: 18, height: 18); Text(innerTitle).lineLimit(1) }
                            .font(.custom("PCL English", size: 18)).foregroundStyle(.white)
                    }.buttonStyle(.plain).padding(.leading, 90)
                } else {
                    Text("PCL").font(.custom("PCL English", size: 25)).tracking(1).padding(.leading, 90)
                }
                Spacer()
            }.foregroundStyle(.white)
            if !hasInnerTitle {
                HStack(spacing: 12.5) {
                    tab("启动", icon: .launch, selected: store.page == .launch) { store.page = .launch }
                    tab("下载", icon: .download, selected: store.page == .downloads) { store.page = .downloads }
                    tab("设置", icon: .settings, selected: store.page == .settings) { store.page = .settings }
                    tab("更多", icon: .more, selected: store.page == .activity || store.page == .about) { store.page = .about }
                }
            }
        }
        .frame(height: Self.height)
        .background(LinearGradient(colors: [Color(red: 0.06, green: 0.43, blue: 0.77), ClassicTheme.blue], startPoint: .leading, endPoint: .trailing))
    }

    private var hasInnerTitle: Bool {
        store.page == .library || store.page == .versionSettings ||
            (store.page == .downloads && store.communityState.selectedProject != nil) ||
            (store.page == .activity && store.showsClassicInstallProgress)
    }

    private var innerTitle: String {
        if store.page == .library { return "版本选择" }
        if store.page == .versionSettings { return "版本设置 · \(store.versionSettingsTarget?.id ?? store.preferences.selectedVersion)" }
        if store.page == .activity { return "任务管理" }
        if store.page == .downloads, let project = store.communityState.selectedProject { return project.name }
        return "安装游戏"
    }

    private func goBack() {
        switch store.page {
        case .versionSettings: store.closeVersionSettings()
        case .downloads:
            if store.communityState.selectedProject != nil { store.closeCommunityProject() }
            else { store.backToDownloadList() }
        default: store.page = .launch
        }
    }

    private func tab(_ name: String, icon: ClassicTopNavigationIcon, selected: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack(spacing: 10) {
                icon.image.frame(width: 20, height: 20).scaleEffect(icon.logoScale).accessibilityHidden(true)
                Text(name).font(.custom("PCL English", size: 16))
            }
            .foregroundStyle(selected ? ClassicTheme.blue : .white)
            .padding(.horizontal, 17.5).frame(height: 34)
            .background(selected ? Color.white : .clear, in: Capsule()).contentShape(Capsule())
        }.buttonStyle(.plain)
    }
}

/// FormMain's filled geometries, with MyRadioButton's original per-icon scale.
/// The 20-point box is its 16-point source box at this launcher's 1.25 UI scale.
private enum ClassicTopNavigationIcon: String, CaseIterable {
    case launch = "Launch", download = "Download", settings = "Settings", more = "More"
    var logoScale: CGFloat {
        switch self { case .launch, .download: return 0.9; case .settings: return 1.1; case .more: return 0.93 }
    }
    private static let templates: [String: NSImage] = {
        var result: [String: NSImage] = [:]
        for icon in Self.allCases {
            if let url = PCLResources.url(icon.rawValue, extension: "pdf", subdirectory: "TopNavigation"),
               let image = NSImage(contentsOf: url) {
                image.isTemplate = true
                result[icon.rawValue] = image
            }
        }
        return result
    }()
    @ViewBuilder var image: some View {
        if let template = Self.templates[rawValue] { Image(nsImage: template).resizable().renderingMode(.template).scaledToFit() }
        else { Color.clear }
    }
}

struct ClassicLaunchPanel: View {
    @EnvironmentObject private var store: LauncherStore

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 16) {
                accountLabel("正版", icon: "AccountPremium", scale: 1)
                    .font(.custom("PCL English", size: 16)).foregroundStyle(ClassicTheme.blue.opacity(0.65))
                    .padding(.horizontal, 14).frame(height: 34)
                    .help("正版登录暂未启用")
                accountLabel("离线", icon: "AccountOffline", scale: 0.85)
                    .font(.custom("PCL English", size: 16)).foregroundStyle(.white)
                    .padding(.horizontal, 14).frame(height: 34)
                    .background(ClassicTheme.blue, in: Capsule())
                    .accessibilityLabel("离线账户，当前已选择")
            }.padding(.top, 28)
            Spacer(minLength: 16)
            VStack(spacing: 0) {
                if let warning = store.offlineNameWarning {
                    // HintChinese uses MyHint in PageLoginLegacy; its text has no leading glyph.
                    Text(warning).lineSpacing(3)
                        .font(.custom("PCL English", size: 12)).foregroundStyle(Color(red: 0.74, green: 0.25, blue: 0.23))
                        .padding(10).frame(maxWidth: .infinity, alignment: .leading)
                        .background(Color(red: 1, green: 0.93, blue: 0.92), in: RoundedRectangle(cornerRadius: 3))
                        .padding(.bottom, 19)
                }
                Button { store.page = .settings } label: { PCLOfflineSkin(playerName: store.preferences.playerName, model: store.offlineSkinModel) }
                    .buttonStyle(.plain).help("皮肤设置")
                PCLOfflineNameField().padding(.top, 19)
            }.padding(.horizontal, 25).padding(.bottom, 6)
            Spacer(minLength: 28)
            VStack(spacing: 12) {
                Button {
                    if store.isGameRunning { store.page = .activity } else { store.requestLaunch() }
                } label: {
                    VStack(spacing: 5) {
                        HStack(spacing: 8) {
                            if store.isLaunching { ProgressView().controlSize(.mini) }
                            Text(launchTitle).font(.custom("PCL English", size: 16))
                        }.foregroundStyle(ClassicTheme.blue)
                        Text(launchSubtitle).font(.custom("PCL English", size: 12)).foregroundStyle(.gray).lineLimit(1)
                    }
                    .frame(maxWidth: .infinity).frame(height: 68)
                    .background(Color.white.opacity(0.65), in: RoundedRectangle(cornerRadius: 3))
                    .overlay { RoundedRectangle(cornerRadius: 3).strokeBorder(ClassicTheme.blue, lineWidth: 1) }
                    .contentShape(Rectangle())
                }.buttonStyle(.plain).disabled(store.isBusy || store.scanning)
                HStack(spacing: 12) {
                    outlineButton("版本选择") { store.page = .library }
                    if let version = store.selectedVersion { outlineButton("版本设置") { store.openVersionSettings(version) } }
                }.disabled(store.scanning)
            }.padding(.horizontal, 19).padding(.bottom, 18)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity).background(ClassicTheme.panel)
    }

    private func accountLabel(_ title: String, icon: String, scale: CGFloat) -> some View {
        // PageLaunchLeft / MyRadioButton: 16-point source box at the Mac UI scale.
        HStack(spacing: 10) {
            PCLIcon(icon).frame(width: 20, height: 20).scaleEffect(scale)
            Text(title)
        }
    }

    private var launchTitle: String {
        if store.scanning { return "正在加载" }
        if store.isGameRunning { return "游戏运行中" }
        if store.isLaunching { return "正在启动" }
        return store.selectedVersion == nil ? "下载游戏" : "启动游戏"
    }

    private var launchSubtitle: String {
        if let version = store.installingID { return "正在安装 \(version) · \(Int((store.installProgress?.fraction ?? 0) * 100))%" }
        if store.scanning { return "正在加载中，请稍候" }
        return store.selectedVersion?.id ?? "未找到可用的游戏版本"
    }

    private func outlineButton(_ name: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(name).font(.custom("PCL English", size: 15)).foregroundStyle(ClassicTheme.text)
                .frame(maxWidth: .infinity).frame(height: 44)
                .background(.white.opacity(0.65), in: RoundedRectangle(cornerRadius: 3))
                .overlay { RoundedRectangle(cornerRadius: 3).strokeBorder(Color.gray.opacity(0.85), lineWidth: 1) }
                .contentShape(Rectangle())
        }.buttonStyle(.plain)
    }
}

struct ClassicSectionSidebar: View {
    @EnvironmentObject private var store: LauncherStore

    var body: some View {
        if store.page == .activity && store.showsClassicInstallProgress {
            PCLInstallProgressSidebar()
        } else {
            navigation
        }
    }

    private var navigation: some View {
        VStack(alignment: .leading, spacing: 0) {
            ScrollView {
                VStack(alignment: .leading, spacing: store.page == .versionSettings ? 4 : 0) {
                    switch store.page {
                    case .versionSettings:
                        instanceRow(.overview, icon: "Overview")
                        instanceRow(.settings, icon: "LaunchSettings", accessory: "Reset", accessoryTitle: "初始化") { store.requestResetVersionSettings() }
                        instanceRow(.modify, icon: "Modify", accessory: "Refresh", accessoryTitle: "刷新") { store.prepareVersionModification(force: true) }
                        instanceRow(.mods, icon: "Mods", accessory: "Refresh", accessoryTitle: "刷新") { store.refreshVersionMods() }
                        instanceRow(.export, icon: "Export", accessory: "Refresh", accessoryTitle: "刷新") { store.refreshVersionExport() }
                    case .library:
                        section("文件夹列表")
                        row(store.gameDirectory.lastPathComponent, icon: nil, active: true) { store.revealGameDirectory() }
                        section("添加或导入")
                        row("添加已有文件夹", icon: "AddFolder") { store.chooseGameDirectory() }.disabled(store.isBusy || store.isGameRunning)
                        row("导入整合包", icon: "ImportPack") { store.chooseModpackArchive() }.disabled(store.isBusy || store.isGameRunning)
                        row("刷新", icon: "Refresh") { Task { await store.refreshLocal() } }
                    case .downloads:
                        row("原版游戏", icon: "Vanilla", active: store.communityState.kind == nil) { store.showVanillaDownloads() }
                        row("导入整合包", icon: "ImportPack") { store.chooseModpackArchive() }.disabled(store.isBusy || store.isGameRunning)
                        section("社区资源")
                        row("Mod", icon: "Mod", active: store.communityState.kind == .mod) { store.openCommunity(.mod) }
                        row("整合包", icon: "Modpack", active: store.communityState.kind == .modpack) { store.openCommunity(.modpack) }
                        row("数据包", icon: "Datapack", active: store.communityState.kind == .datapack) { store.openCommunity(.datapack) }
                        row("资源包", icon: "Resourcepack", active: store.communityState.kind == .resourcepack) { store.openCommunity(.resourcepack) }
                        row("光影包", icon: "Shader", active: store.communityState.kind == .shader) { store.openCommunity(.shader) }
                    case .settings:
                        row("启动", icon: "SetupLaunch", active: true) {}
                        row("联机", icon: "Online") {}.disabled(true)
                        row("个性化", icon: "Personalize") {}.disabled(true)
                        row("其他", icon: "SetupOther") {}.disabled(true)
                    default:
                        row("帮助", icon: "Help") {}.disabled(true)
                        row("关于与鸣谢", icon: "About", active: store.page == .about) { store.page = .about }
                        row("百宝箱", icon: "Tools") {}.disabled(true)
                        row("启动日志", icon: "Log", active: store.page == .activity) { store.page = .activity }
                        section(" ")
                        row("反馈", icon: "Feedback") { NSWorkspace.shared.open(URL(string: "https://github.com/Meloong-Git/PCL/issues")!) }
                        row("新功能投票", icon: "Vote") { NSWorkspace.shared.open(URL(string: "https://github.com/Meloong-Git/PCL/discussions/2")!) }
                    }
                }.padding(.top, store.page == .versionSettings ? 18 : 15)
            }
            Spacer(minLength: 0)
        }.frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading).background(ClassicTheme.panel)
    }

    private func section(_ name: String) -> some View {
        Text(name).font(.custom("PCL English", size: 12)).foregroundStyle(ClassicTheme.text.opacity(0.6))
            .padding(.leading, 16).padding(.top, 22).padding(.bottom, 5)
    }

    private func row(_ name: String, icon: String?, active: Bool = false, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack(spacing: 8) {
                if let icon { PCLIcon(icon).frame(width: 20, height: 20).scaleEffect(sidebarLogoScale(icon)) }
                Text(name)
            }.font(.custom("PCL English", size: 14))
                .foregroundStyle(active ? ClassicTheme.blue : ClassicTheme.text)
                .frame(maxWidth: .infinity, alignment: .leading).padding(.horizontal, 23).frame(height: 45)
                .background(active ? ClassicTheme.blue.opacity(0.08) : .clear)
                .overlay(alignment: .leading) { if active { Rectangle().fill(ClassicTheme.blue).frame(width: 3) } }
                .contentShape(Rectangle())
        }.buttonStyle(.plain)
    }

    /// MyListItem's per-page LogoScale values from the original left sidebars.
    private func sidebarLogoScale(_ icon: String) -> CGFloat {
        switch icon {
        case "Mod", "Help", "Tools": return 0.97
        case "Modpack", "About": return 0.98
        case "Datapack": return 0.96
        case "Resourcepack": return 0.81
        case "Shader": return 1.04
        case "SetupLaunch", "SetupOther": return 0.8
        case "Online": return 0.77
        case "Personalize", "Feedback": return 0.9
        case "Vote": return 0.92
        default: return 1
        }
    }

    private func instanceRow(_ tab: VersionSettingsTab, icon: String, accessory: String? = nil,
                             accessoryTitle: String? = nil, accessoryAction: @escaping () -> Void = {}) -> some View {
        PCLInstanceNavigationRow(title: tab.rawValue, icon: icon, selected: store.versionSettingsTab == tab,
                                 accessory: accessory, accessoryTitle: accessoryTitle,
                                 action: { store.selectVersionSettingsTab(tab) }, accessoryAction: accessoryAction)
            .disabled(store.isBusy)
    }
}
