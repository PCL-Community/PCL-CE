import AppKit
import SwiftUI

@main struct PCLMacApp: App {
    init() { PCLResources.registerFont() }
    @StateObject private var store = LauncherStore()
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        WindowGroup("PCL Mac版") {
            LauncherView().environmentObject(store)
                .frame(minWidth: 940, minHeight: 600)
                .ignoresSafeArea(.container, edges: .top)
                .overlay(alignment: .topLeading) {
                    PCLWindowChrome().frame(width: 80, height: ClassicHeader.height)
                        .ignoresSafeArea(.container, edges: .top)
                }
                .task { appDelegate.store = store; await store.start() }
        }
        .defaultSize(width: 1080, height: 640)
        .windowStyle(.hiddenTitleBar)
        .windowResizability(.contentMinSize)
        .commands {
            CommandGroup(replacing: .appInfo) {
                Button("关于 PCL Mac版") { store.page = .about }.keyboardShortcut("i", modifiers: [.command, .option])
            }
            CommandGroup(after: .newItem) {
                Button("打开游戏文件夹") { store.revealGameDirectory() }.keyboardShortcut("o", modifiers: [.command, .shift])
                Button("刷新本地版本") { Task { await store.refreshLocal() } }.keyboardShortcut("r")
            }
            CommandGroup(replacing: .appSettings) {
                Button("设置…") { store.page = .settings }.keyboardShortcut(",")
            }
        }
    }
}

@MainActor final class AppDelegate: NSObject, NSApplicationDelegate {
    weak var store: LauncherStore?
    func applicationDidFinishLaunching(_ notification: Notification) {
        PCLResources.registerFont()
        NSApplication.shared.setActivationPolicy(.regular)
        NSApplication.shared.activate(ignoringOtherApps: true)
    }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        if store?.isLaunching == true {
            let alert = NSAlert()
            alert.messageText = "游戏正在准备启动"
            alert.informativeText = "请等待启动完成后再退出启动器。"
            alert.runModal()
            return .terminateCancel
        }
        if store?.isGameRunning == true || store?.installingID != nil || store?.versionExportState.isExporting == true || store?.versionModState.mutating == true {
            let alert = NSAlert()
            alert.messageText = "结束当前任务并退出？"
            alert.informativeText = "退出将停止运行中的游戏并取消下载。已完成的下载文件会保留。"
            alert.addButton(withTitle: "停止并退出")
            alert.addButton(withTitle: "取消")
            if alert.runModal() != .alertFirstButtonReturn { return .terminateCancel }
        }
        store?.prepareToQuit()
        return .terminateNow
    }
}

/// Forward SwiftUI's window delegate while supplying the fullscreen presentation policy.
@MainActor final class FullscreenWindowDelegate: NSObject, NSWindowDelegate {
    weak var original: NSWindowDelegate?

    func attach(to window: NSWindow) {
        guard window.delegate !== self else { return }
        var delegate = window.delegate
        while let proxy = delegate as? FullscreenWindowDelegate {
            guard proxy !== self else { return }
            delegate = proxy.original
        }
        original = delegate
        window.delegate = self
    }

    override func responds(to selector: Selector!) -> Bool {
        super.responds(to: selector) || original?.responds(to: selector) == true
    }

    override func forwardingTarget(for selector: Selector!) -> Any? {
        if original?.responds(to: selector) == true { return original }
        return super.forwardingTarget(for: selector)
    }

    func window(_ window: NSWindow, willUseFullScreenPresentationOptions proposedOptions: NSApplication.PresentationOptions) -> NSApplication.PresentationOptions {
        var options = original?.window?(window, willUseFullScreenPresentationOptions: proposedOptions) ?? proposedOptions
        // Keep the Dock and system menu bar's normal fullscreen edge reveal.
        // Only the launcher's separate titlebar overlay is suppressed.
        options.subtract([.autoHideToolbar, .hideMenuBar, .hideDock])
        options.formUnion([.fullScreen, .autoHideMenuBar, .autoHideDock])
        return options
    }
}
