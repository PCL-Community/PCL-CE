import AppKit
import CoreText
import PCLCore
import SwiftUI

enum PCLResources {
    static func url(_ name: String, extension ext: String, subdirectory: String? = nil) -> URL? {
        if let bundled = Bundle.main.url(forResource: name, withExtension: ext, subdirectory: subdirectory) { return bundled }
        if let flattened = Bundle.main.url(forResource: name, withExtension: ext) { return flattened }
        // SwiftPM's executable is also runnable directly from this source checkout.
        let resources = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent().appendingPathComponent("Resources")
        let directory = subdirectory.map { resources.appendingPathComponent($0) } ?? resources
        let candidate = directory.appendingPathComponent(name).appendingPathExtension(ext)
        return FileManager.default.fileExists(atPath: candidate.path) ? candidate : nil
    }

    static func registerFont() {
        if let url = url("Font", extension: "ttf") { CTFontManagerRegisterFontsForURL(url as CFURL, .process, nil) }
    }
}

/// Mirrors MySkin: an 8×8 face at 48/64 size with its hat layer at 56/64 size.
struct PCLOfflineSkin: View {
    let playerName: String
    var model: OfflineSkinModel = .automatic
    private static let steve = load("Steve")
    private static let alex = load("Alex")

    var body: some View {
        let skin = skinName == "Alex" ? Self.alex : Self.steve
        ZStack {
            if let face = skin.face { Image(decorative: face, scale: 1).resizable().interpolation(.none).frame(width: 60, height: 60) }
            if let hat = skin.hat { Image(decorative: hat, scale: 1).resizable().interpolation(.none).frame(width: 70, height: 70) }
        }
        .frame(width: 80, height: 80)
        .shadow(color: ClassicTheme.blue.opacity(0.20), radius: 6)
        .accessibilityLabel("离线皮肤 \(skinName)")
    }

    private var skinName: String {
        OfflineLogin.skinName(forUUID: OfflineLogin.uuid(for: OfflineLogin.normalizedName(playerName), skinModel: model))
    }

    private static func load(_ name: String) -> (face: CGImage?, hat: CGImage?) {
        guard let url = PCLResources.url(name, extension: "png", subdirectory: "Skins"),
              let image = NSImage(contentsOf: url), let cg = image.cgImage(forProposedRect: nil, context: nil, hints: nil) else { return (nil, nil) }
        let scale = CGFloat(cg.width) / 64
        return (cg.cropping(to: CGRect(x: 8 * scale, y: 8 * scale, width: 8 * scale, height: 8 * scale)),
                cg.cropping(to: CGRect(x: 40 * scale, y: 8 * scale, width: 8 * scale, height: 8 * scale)))
    }
}

struct PCLOfflineNameField: View {
    @EnvironmentObject private var store: LauncherStore
    @FocusState private var focused: Bool
    @State private var showHistory = false

    var body: some View {
        HStack(spacing: 0) {
            TextField("游戏用户名", text: $store.preferences.playerName)
                .textFieldStyle(.plain).font(.custom("PCL English", size: 16))
                .padding(.leading, 9).focused($focused)
                .onSubmit { _ = store.completeOfflineLogin(reportErrors: true); focused = false }
                .onKeyPress(.space) {
                    guard let editor = NSApp.keyWindow?.firstResponder as? NSTextView, !editor.hasMarkedText() else { return .ignored }
                    let cursor = editor.selectedRange().location
                    return cursor == 0 || cursor == (editor.string as NSString).length ? .handled : .ignored
                }
                .onKeyPress(.downArrow) {
                    guard !store.offlineNameHistory.isEmpty else { return .ignored }
                    showHistory = true; return .handled
                }
            Button { showHistory.toggle() } label: {
                PCLIcon("ChevronDown").frame(width: 10, height: 10)
                    .foregroundStyle(Color.gray).frame(width: 30, height: 33).contentShape(Rectangle())
            }.buttonStyle(.plain).help("选择离线登录记录")
                .popover(isPresented: $showHistory, arrowEdge: .bottom) { history }
        }
        .frame(height: 35).background(.white, in: RoundedRectangle(cornerRadius: 3))
        .overlay { RoundedRectangle(cornerRadius: 3).strokeBorder(focused ? ClassicTheme.blue : Color.gray.opacity(0.5), lineWidth: 1) }
        .onChange(of: focused) { old, new in if old && !new { store.completeOfflineLogin() } }
    }

    private var history: some View {
        ScrollView {
            VStack(spacing: 0) {
                if store.offlineNameHistory.isEmpty {
                    Text("暂无登录记录").font(.system(size: 13)).foregroundStyle(.secondary).padding(12)
                }
                ForEach(store.offlineNameHistory, id: \.self) { name in
                    HStack(spacing: 0) {
                        Button { store.selectOfflineName(name); showHistory = false } label: {
                            Text(name).font(.custom("PCL English", size: 15)).lineLimit(1)
                                .frame(maxWidth: .infinity, alignment: .leading).padding(.horizontal, 10).frame(height: 34)
                                .contentShape(Rectangle())
                        }
                        Button {
                            store.deleteOfflineName(name)
                            if store.offlineNameHistory.isEmpty { showHistory = false }
                        } label: {
                            PCLIcon("Close").frame(width: 11, height: 11).foregroundStyle(.secondary).frame(width: 30, height: 34)
                        }.help("删除 \(name) 的登录记录").accessibilityLabel("删除登录记录 \(name)")
                    }.buttonStyle(.plain)
                }
            }
        }.frame(width: 285, height: min(238, CGFloat(max(1, store.offlineNameHistory.count)) * 34 + 8)).padding(4)
    }
}

/// Preserve native titlebar tracking and actions in ordinary windows.
struct PCLWindowChrome: NSViewRepresentable {
    func makeNSView(context: Context) -> NSView { ChromeView() }
    func updateNSView(_ nsView: NSView, context: Context) {}
    static func dismantleNSView(_ nsView: NSView, coordinator: ()) { (nsView as? ChromeView)?.detach() }

    private final class ChromeView: NSView {
        private static let types: [NSWindow.ButtonType] = [.closeButton, .miniaturizeButton, .zoomButton]
        private weak var configuredWindow: NSWindow?
        private var fullscreenButtons: [NSButton] = []
        private let emptyTitlebar = NSTitlebarAccessoryViewController()
        private let fullscreenTitlebar = FullscreenTitlebarView()
        private let presentationDelegate = FullscreenWindowDelegate()
        private var hoverTracking: NSTrackingArea?
        private var fullscreenTransition = false
        private var updatingLayout = false
        override var isFlipped: Bool { true }

        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            guard let window, configuredWindow !== window else { return }
            detach()
            configuredWindow = window
            window.styleMask.insert([.fullSizeContentView, .closable, .miniaturizable, .resizable])
            window.titleVisibility = .hidden
            window.titlebarAppearsTransparent = true
            window.titlebarSeparatorStyle = .none
            window.toolbar = nil
            window.isMovableByWindowBackground = true
            emptyTitlebar.layoutAttribute = .top
            fullscreenTitlebar.ownerWindow = window
            emptyTitlebar.view = fullscreenTitlebar
            fullscreenButtons = Self.types.compactMap { type in
                guard let button = NSWindow.standardWindowButton(type, for: window.styleMask) else { return nil }
                button.target = self
                switch type {
                case .closeButton: button.action = #selector(closeWindow)
                case .miniaturizeButton: button.action = #selector(miniaturizeWindow)
                default: button.action = #selector(toggleFullscreen)
                }
                addSubview(button)
                return button
            }
            for name in [NSWindow.willEnterFullScreenNotification, NSWindow.didEnterFullScreenNotification,
                         NSWindow.didExitFullScreenNotification, NSWindow.didResizeNotification,
                         NSWindow.didBecomeKeyNotification, NSWindow.willBeginSheetNotification,
                         NSWindow.didEndSheetNotification, NSWindow.didMiniaturizeNotification,
                         NSWindow.didDeminiaturizeNotification] {
                NotificationCenter.default.addObserver(self, selector: #selector(windowChanged(_:)), name: name, object: window)
            }
            updateControls()
        }

        override func layout() { super.layout(); updateControls() }

        @objc private func windowChanged(_ notification: Notification) {
            if notification.name == NSWindow.willEnterFullScreenNotification { fullscreenTransition = true }
            if notification.name == NSWindow.didEnterFullScreenNotification { fullscreenTransition = false }
            if notification.name == NSWindow.didExitFullScreenNotification { fullscreenTransition = false }
            updateControls()
        }

        private var usesFullscreenControls: Bool {
            fullscreenTransition || configuredWindow?.styleMask.contains(.fullScreen) == true
        }

        private func updateControls() {
            guard let window = configuredWindow, !updatingLayout else { return }
            updatingLayout = true
            defer { updatingLayout = false }
            presentationDelegate.attach(to: window)
            let fullscreen = usesFullscreenControls
            fullscreenTitlebar.suppressionEnabled = fullscreen
            let hasAccessory = window.titlebarAccessoryViewControllers.contains(emptyTitlebar)
            if fullscreen && !hasAccessory { window.addTitlebarAccessoryViewController(emptyTitlebar) }
            if !fullscreen, let index = window.titlebarAccessoryViewControllers.firstIndex(of: emptyTitlebar) {
                window.removeTitlebarAccessoryViewController(at: index)
            }
            for type in Self.types {
                if let button = window.standardWindowButton(type), button.isHidden != fullscreen { button.isHidden = fullscreen }
            }
            for (index, button) in fullscreenButtons.enumerated() {
                button.isHidden = !fullscreen
                button.isEnabled = window.attachedSheet == nil && index != 1
                button.setFrameOrigin(NSPoint(x: 12 + CGFloat(index) * 22, y: (bounds.height - button.frame.height) / 2))
            }
            guard !fullscreen, !window.isMiniaturized,
                  let close = window.standardWindowButton(.closeButton),
                  let minimize = window.standardWindowButton(.miniaturizeButton),
                  let zoom = window.standardWindowButton(.zoomButton),
                  let container = close.superview?.superview, let frameView = container.superview else { return }
            // AppKit's existing parent manages group hover and the green-button menu.
            let height = ClassicHeader.height
            var frame = container.frame
            frame.origin.y = frameView.bounds.height - height
            frame.size.height = height
            frame.size.width = bounds.width
            if container.frame != frame { container.frame = frame }
            for (index, button) in [close, minimize, zoom].enumerated() {
                let origin = NSPoint(x: 12 + CGFloat(index) * 22, y: (height - button.frame.height) / 2)
                if button.frame.origin != origin { button.setFrameOrigin(origin) }
            }
        }

        override func updateTrackingAreas() {
            super.updateTrackingAreas()
            if let hoverTracking { removeTrackingArea(hoverTracking) }
            let tracking = NSTrackingArea(rect: .zero, options: [.mouseEnteredAndExited, .activeAlways, .inVisibleRect], owner: self)
            addTrackingArea(tracking)
            hoverTracking = tracking
        }
        override func mouseEntered(with event: NSEvent) {
            guard usesFullscreenControls else { return }
            fullscreenButtons.forEach { ($0.cell as? NSButtonCell)?.mouseEntered(with: event) }
        }
        override func mouseExited(with event: NSEvent) {
            guard usesFullscreenControls else { return }
            fullscreenButtons.forEach { ($0.cell as? NSButtonCell)?.mouseExited(with: event) }
        }
        @objc private func closeWindow() { configuredWindow?.performClose(nil) }
        @objc private func miniaturizeWindow() { configuredWindow?.miniaturize(nil) }
        @objc private func toggleFullscreen() {
            guard let window = configuredWindow else { return }
            if NSApp.currentEvent?.modifierFlags.contains(.option) == true { window.performZoom(nil) }
            else { window.toggleFullScreen(nil) }
        }
        override func hitTest(_ point: NSPoint) -> NSView? {
            guard usesFullscreenControls else { return nil }
            let local = convert(point, from: superview)
            return fullscreenButtons.first { $0.isEnabled && $0.frame.insetBy(dx: -4, dy: -7).contains(local) }
        }
        func detach() {
            NotificationCenter.default.removeObserver(self)
            fullscreenTitlebar.suppressionEnabled = false
            fullscreenTitlebar.ownerWindow = nil
            if let window = configuredWindow {
                if window.delegate === presentationDelegate { window.delegate = presentationDelegate.original }
                presentationDelegate.original = nil
                if let index = window.titlebarAccessoryViewControllers.firstIndex(of: emptyTitlebar) {
                    window.removeTitlebarAccessoryViewController(at: index)
                }
                for type in Self.types { window.standardWindowButton(type)?.isHidden = false }
            }
            fullscreenButtons.forEach { $0.removeFromSuperview() }
            fullscreenButtons = []
            configuredWindow = nil
            fullscreenTransition = false
        }
        deinit { NotificationCenter.default.removeObserver(self) }
    }

    /// AppKit moves this accessory into a separate window during fullscreen.
    /// Suppress that window, leaving the Dock and system menu bar untouched.
    private final class FullscreenTitlebarView: NSView {
        weak var ownerWindow: NSWindow?
        var suppressionEnabled = false { didSet { updateSuppression() } }
        private weak var floatingWindow: NSWindow?
        private var previousAlpha: CGFloat = 1
        private var previousIgnoresMouseEvents = false
        private var alphaObservation: NSKeyValueObservation?
        private var enforcingHidden = false

        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            updateSuppression()
        }

        private func updateSuppression() {
            guard suppressionEnabled, let ownerWindow, let window, window !== ownerWindow else {
                restoreFloatingWindow()
                return
            }
            if floatingWindow !== window {
                restoreFloatingWindow()
                floatingWindow = window
                previousAlpha = window.alphaValue
                previousIgnoresMouseEvents = window.ignoresMouseEvents
                alphaObservation = window.observe(\.alphaValue, options: [.new]) { [weak self] _, _ in
                    self?.hideFloatingWindow()
                }
            }
            hideFloatingWindow()
        }

        private func hideFloatingWindow() {
            guard !enforcingHidden, let floatingWindow else { return }
            enforcingHidden = true
            defer { enforcingHidden = false }
            if floatingWindow.alphaValue != 0 { floatingWindow.alphaValue = 0 }
            if !floatingWindow.ignoresMouseEvents { floatingWindow.ignoresMouseEvents = true }
        }

        private func restoreFloatingWindow() {
            alphaObservation?.invalidate()
            alphaObservation = nil
            guard let window = floatingWindow else { return }
            floatingWindow = nil
            window.alphaValue = previousAlpha
            window.ignoresMouseEvents = previousIgnoresMouseEvents
        }
    }
}
