import AppKit
import PCLCore
import SwiftUI

struct LauncherView: View {
    @EnvironmentObject private var store: LauncherStore

    var body: some View {
        GeometryReader { geometry in
            VStack(spacing: 0) {
                ClassicHeader()
                HStack(spacing: 0) {
                    Group {
                        if store.page == .launch { ClassicLaunchPanel() }
                        else { ClassicSectionSidebar() }
                    }
                    .frame(width: showsInstallProgress || store.page == .downloads || store.page == .versionSettings ? 200 : min(370, max(300, geometry.size.width * 0.342)))
                    Rectangle().fill(Color.pclBlue.opacity(0.08)).frame(width: 1)
                    ZStack {
                        ClassicTheme.background
                        if store.page != .launch {
                            ScrollView {
                                VStack(alignment: .leading, spacing: store.page == .versionSettings ? 15 : 22) {
                                    switch store.page {
                                    case .launch: EmptyView()
                                    case .library: PCLVersionLibraryPage()
                                    case .downloads: PCLDownloadsPage()
                                    case .activity: activityPage
                                    case .settings: settingsPage
                                    case .about: aboutPage
                                    case .versionSettings: VersionSettingsView()
                                    }
                                }
                                .padding(.horizontal, showsInstallProgress || store.page == .versionSettings ? 25 : 26)
                                .padding(.top, showsInstallProgress || store.page == .versionSettings ? 25 : 26)
                                .padding(.bottom, showsInstallProgress ? 10 : 26)
                                .frame(maxWidth: showsInstallProgress ? .infinity : 1100, alignment: .leading)
                                .frame(maxWidth: .infinity)
                            }
                        }
                    }
                    .overlay(alignment: .bottomTrailing) {
                        VStack(alignment: .trailing, spacing: 0) {
                            if store.page == .downloads && store.communityState.kind == nil && store.currentRemoteSelection != nil {
                                PCLInstallButton()
                            }
                            if store.showsClassicInstallProgress && store.page != .activity {
                                PCLTaskManagerButton()
                            }
                        }.padding(15)
                    }
                }
            }
        }
        .background(ClassicTheme.panel)
        .preferredColorScheme(.light)
        .tint(.pclBlue)
        .dropDestination(for: URL.self) { urls, _ in
            guard urls.count == 1, let archive = urls.first, archive.isFileURL,
                  ["zip", "mrpack"].contains(archive.pathExtension.lowercased()),
                  !store.isBusy, !store.isGameRunning else { return false }
            store.inspectModpackArchive(archive)
            return true
        }
        .sheet(isPresented: $store.showLaunchConsent) { launchConsent }
        .sheet(isPresented: $store.moddedState.showModpackSheet, onDismiss: store.dismissModpackPreview) { PCLModpackImportSheet() }
        .sheet(isPresented: $store.communityState.showDownloadSheet, onDismiss: store.dismissCommunityDownload) { PCLCommunityDownloadSheet() }
        .alert("操作未完成", isPresented: Binding(get: { store.errorMessage != nil }, set: { if !$0 { store.errorMessage = nil } })) {
            Button("好的", role: .cancel) { store.errorMessage = nil }
            Button("查看日志") { store.errorMessage = nil; store.page = .activity }
        } message: { Text(store.errorMessage ?? "") }
        .onChange(of: store.page) { _, page in
            store.search = ""
            if page == .downloads && store.communityState.kind == nil && store.remoteVersions.isEmpty { Task { await store.fetchManifest() } }
        }
    }

    private var showsInstallProgress: Bool {
        store.page == .activity && store.showsClassicInstallProgress
    }

    @ViewBuilder private var activityPage: some View {
        if store.showsClassicInstallProgress {
            PCLDownloadProgressCard()
        } else {
            activityLogs
        }
    }

    private var activityLogs: some View {
        Group {
            PCLCard(title: "任务") {
                HStack(spacing: 10) {
                    Circle().fill(store.isGameRunning ? Color.green : Color.pclBlue).frame(width: 6, height: 6)
                    Text(store.statusMessage).font(.custom("PCL English", size: 13))
                    Spacer()
                    if store.isGameRunning {
                        Button("结束游戏") { store.stopGame() }.buttonStyle(PCLActionStyle())
                    }
                }.padding(.vertical, 6)
            }
            PCLCard(title: "日志输出") {
                VStack(spacing: 12) {
                    ScrollView([.vertical, .horizontal]) {
                        Text(store.logs.isEmpty ? "暂无日志" : store.logs.joined(separator: "\n"))
                            .font(.system(size: 11, design: .monospaced)).lineSpacing(4).textSelection(.enabled)
                            .frame(maxWidth: .infinity, alignment: .topLeading)
                    }.frame(minHeight: 240, maxHeight: 400)
                    HStack(spacing: 12) {
                        Button("导出日志") { store.exportLogs() }.buttonStyle(PCLActionStyle()).disabled(store.logs.isEmpty)
                        Button("清空") { store.logs.removeAll() }.buttonStyle(PCLActionStyle()).disabled(store.logs.isEmpty)
                        Spacer()
                        Text("最多显示 2000 行").font(.custom("PCL English", size: 11)).foregroundStyle(.secondary)
                    }
                }
            }
        }
    }

    private var settingsPage: some View {
        Group {
            PCLCard(title: "启动选项") {
                VStack(alignment: .leading, spacing: 13) {
                    HStack(spacing: 15) {
                        settingLabel("玩家名")
                        TextField("游戏用户名", text: $store.preferences.playerName)
                            .textFieldStyle(.roundedBorder).font(.custom("PCL English", size: 13)).frame(maxWidth: .infinity)
                            .onSubmit { store.completeOfflineLogin(reportErrors: true) }
                    }
                    if let warning = store.offlineNameWarning {
                        Text(warning).font(.custom("PCL English", size: 11)).foregroundStyle(.orange)
                            .padding(.leading, 115).lineSpacing(3)
                    }
                    HStack(alignment: .center, spacing: 15) {
                        settingLabel("游戏文件夹")
                        Text(store.preferences.gamePath).font(.custom("PCL English", size: 11)).foregroundStyle(.secondary)
                            .lineLimit(2).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading)
                        Button("更改") { store.chooseGameDirectory() }.buttonStyle(PCLActionStyle())
                            .disabled(store.isBusy || store.isGameRunning)
                    }
                    HStack { Spacer().frame(width: 100); Button("打开游戏文件夹") { store.revealGameDirectory() }.buttonStyle(PCLActionStyle()) }
                }
            }
            PCLCard(title: "内存分配") {
                VStack(alignment: .leading, spacing: 15) {
                    HStack(spacing: 15) {
                        HStack(spacing: 7) {
                            PCLIcon("CircleFilled").frame(width: 14, height: 14).foregroundStyle(Color.pclBlue)
                            Text("自定义").font(.custom("PCL English", size: 13))
                        }.frame(width: 100, alignment: .leading)
                        Slider(value: Binding(get: { Double(store.preferences.memoryMB) }, set: { store.preferences.memoryMB = Int($0) }),
                               in: 1024...Double(store.memoryLimit), step: 512)
                    }
                    HStack(alignment: .top) {
                        VStack(alignment: .leading, spacing: 4) {
                            Text("游戏分配").font(.custom("PCL English", size: 11)).foregroundStyle(.secondary)
                            Text(store.memoryLabel).font(.custom("PCL English", size: 16)).foregroundStyle(Color.pclBlue)
                        }
                        Spacer()
                        VStack(alignment: .trailing, spacing: 4) {
                            Text("物理内存").font(.custom("PCL English", size: 11)).foregroundStyle(.secondary)
                            Text(String(format: "%.1f GB", Double(ProcessInfo.processInfo.physicalMemory) / 1024 / 1024 / 1024)).font(.custom("PCL English", size: 16)).foregroundStyle(.secondary)
                        }
                    }
                }
            }
            PCLCard(title: "离线皮肤", collapsible: true, expanded: false) {
                VStack(alignment: .leading, spacing: 14) {
                    Text("由于技术问题，此功能只保证对 1.19.2 以前的版本有效！")
                        .font(.custom("PCL English", size: 12)).foregroundStyle(Color(red: 0.57, green: 0.40, blue: 0.12))
                        .padding(10).frame(maxWidth: .infinity, alignment: .leading)
                        .background(Color(red: 1, green: 0.97, blue: 0.86), in: RoundedRectangle(cornerRadius: 3))
                    HStack(spacing: 22) {
                        Picker("离线皮肤", selection: $store.offlineSkinModel) {
                            Text("随机").tag(OfflineSkinModel.automatic)
                            Text("Steve").tag(OfflineSkinModel.steve)
                            Text("Alex").tag(OfflineSkinModel.alex)
                        }.pickerStyle(.radioGroup).horizontalRadioGroupLayout().labelsHidden()
                        PCLIconLabel("正版皮肤", icon: "Circle").foregroundStyle(.tertiary).help("此选项尚未移植")
                        PCLIconLabel("自定义", icon: "Circle").foregroundStyle(.tertiary).help("此选项尚未移植")
                    }.font(.custom("PCL English", size: 13))
                }
            }
            PCLCard(title: "高级选项", collapsible: true, expanded: false) {
                VStack(alignment: .leading, spacing: 13) {
                    HStack(alignment: .top, spacing: 15) {
                        settingLabel("Java 虚拟机参数").padding(.top, 6)
                        TextEditor(text: $store.preferences.extraJVMArguments)
                            .font(.custom("PCL English", size: 12)).frame(height: 90)
                            .padding(4).background(.white, in: RoundedRectangle(cornerRadius: 3))
                            .overlay { RoundedRectangle(cornerRadius: 3).stroke(Color.pclBlue.opacity(0.35), lineWidth: 1) }
                    }
                    HStack(spacing: 15) {
                        settingLabel("Java 列表")
                        PCLComboBox(title: "Java 列表", selection: $store.preferences.javaPath,
                                    options: [PCLComboBoxOption(title: "自动选择合适的 Java", value: "")] +
                                        store.javaRuntimes.map { PCLComboBoxOption(title: $0.displayName, value: $0.executable.path) })
                            .frame(maxWidth: .infinity).frame(height: 28)
                        Button { Task { await store.refreshLocal() } } label: { PCLIcon("Refresh").frame(width: 14, height: 14) }
                            .buttonStyle(PCLActionStyle()).disabled(store.scanning).help("重新搜索 Java")
                    }
                    HStack(spacing: 12) {
                        Spacer().frame(width: 103)
                        Button("手动导入 Java") { store.chooseJava() }.buttonStyle(PCLActionStyle())
                    }
                    if let java = store.selectedJava {
                        Text(java.executable.path).font(.custom("PCL English", size: 10)).foregroundStyle(.secondary).lineLimit(2).textSelection(.enabled)
                    } else {
                        Text("未找到可用的 Java，请安装后重新搜索，或手动导入。").font(.custom("PCL English", size: 11)).foregroundStyle(.orange)
                    }
                }
            }
        }
    }

    private func settingLabel(_ name: String) -> some View {
        Text(name).font(.custom("PCL English", size: 13)).frame(width: 100, alignment: .leading)
    }

    private var aboutPage: some View {
        Group {
            PCLCard(title: "关于") {
                VStack(spacing: 5) {
                    HStack(spacing: 10) {
                        PCLResourceImage(name: "LTCat", extensionName: "jpg", fallback: "Account").frame(width: 34, height: 34).clipShape(Circle())
                        VStack(alignment: .leading, spacing: 4) {
                            Text("龙腾猫跃").font(.custom("PCL English", size: 14))
                            Text("Plain Craft Launcher 的开发者！").font(.custom("PCL English", size: 11)).foregroundStyle(.secondary)
                        }
                        Spacer()
                        Link("赞助 PCL !", destination: URL(string: "https://meloong.com/afd/a/LTCat")!)
                            .buttonStyle(PCLActionStyle(highlight: true))
                    }.frame(minHeight: 45)
                    HStack(spacing: 10) {
                        PCLResourceImage(name: "HerobrineXia", fallback: "Account").frame(width: 34, height: 34).clipShape(Circle())
                        VStack(alignment: .leading, spacing: 4) {
                            Text("HerobrineXia").font(.custom("PCL English", size: 14))
                            Text("原项目的协力开发者！").font(.custom("PCL English", size: 11)).foregroundStyle(.secondary)
                        }
                        Spacer()
                    }.frame(minHeight: 45)
                    HStack(spacing: 10) {
                        PCLResourceImage(name: "Logo", fallback: "Vanilla").frame(width: 34, height: 34)
                        VStack(alignment: .leading, spacing: 4) {
                            Text("Plain Craft Launcher (PCL) macOS").font(.custom("PCL English", size: 14))
                            Text("当前版本：\(Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0.1.0")，第三方独立改编版")
                                .font(.custom("PCL English", size: 11)).foregroundStyle(.secondary)
                        }
                        Spacer()
                    }.frame(minHeight: 45)
                }
            }
            PCLCard(title: "法律信息") {
                VStack(alignment: .leading, spacing: 13) {
                    Text("本应用是第三方基于 PCL 独立进行二次创作的产物，并非原作者发布的官方 macOS 版本。")
                        .font(.custom("PCL English", size: 13)).lineSpacing(5)
                    HStack(spacing: 12) {
                        Button("PCL 分发有限许可") {
                            if let license = Bundle.main.url(forResource: "LICENCE", withExtension: nil) { NSWorkspace.shared.open(license) }
                        }.buttonStyle(PCLActionStyle())
                        Link("原项目开源代码", destination: URL(string: "https://github.com/Meloong-Git/PCL")!).buttonStyle(PCLActionStyle())
                    }
                    Text("Minecraft 及相关商标属于各自权利人。本应用未与 Mojang 或 Microsoft 建立关联。")
                        .font(.custom("PCL English", size: 11)).foregroundStyle(.secondary).lineSpacing(4)
                }
            }
            PCLCard(title: "功能说明", collapsible: true, expanded: false) {
                Text("已提供原版游戏与 Forge、NeoForge、Fabric、Quilt 安装、本地整合包导入、补全文件、版本选择、Java 检测与离线启动。版本设置支持组件修改、Mod 管理、完整 ZIP 与 Modrinth 整合包导出。社区资源支持 Mod、整合包、数据包、资源包与光影包的搜索、筛选、版本详情和下载安装，可自动补全必需前置 Mod。微软正版授权与联机服务暂未接入。")
                    .font(.custom("PCL English", size: 12)).foregroundStyle(.secondary).lineSpacing(5)
            }
        }
    }

    private var launchConsent: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text("考虑一下正版？").font(.custom("PCL English", size: 19)).foregroundStyle(Color.pclBlue)
            Divider()
            Text("如果觉得 Minecraft 还不错，可以购买正版支持一下，毕竟开发游戏也真的很不容易……")
                .font(.custom("PCL English", size: 13)).lineSpacing(5)
            Link("支持正版游戏！", destination: URL(string: "https://www.minecraft.net/store/minecraft-java-bedrock-edition-pc")!)
            Text("如果 PCL 还算好用的话，也欢迎赞助原作者龙腾猫跃，支持 PCL 持续开发！")
                .font(.custom("PCL English", size: 13)).lineSpacing(5)
            Link("支持 PCL！", destination: URL(string: "https://meloong.com/afd/a/LTCat")!)
            HStack {
                Spacer()
                Button("取消", role: .cancel) { store.showLaunchConsent = false }.buttonStyle(PCLActionStyle()).keyboardShortcut(.cancelAction)
                Button("继续离线启动") { store.launchGame() }.buttonStyle(PCLActionStyle(highlight: true)).keyboardShortcut(.defaultAction)
            }.padding(.top, 4)
        }.padding(25).frame(width: 470)
    }

}
