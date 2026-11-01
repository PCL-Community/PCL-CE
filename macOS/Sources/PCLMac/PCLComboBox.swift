import AppKit
import SwiftUI

struct PCLComboBoxOption<Selection: Hashable>: Identifiable, Equatable {
    let title: String
    let value: Selection
    var isEnabled = true
    var id: Selection { value }
}

/// MyComboBox's field and popup are drawn together; no system menu styling is used.
struct PCLComboBox<Selection: Hashable>: View {
    let title: String
    @Binding var selection: Selection
    let options: [PCLComboBoxOption<Selection>]

    var body: some View {
        PCLComboBoxRepresentable(title: title, selection: $selection, options: options)
            .frame(maxWidth: .infinity)
            .frame(height: 28)
    }
}

private struct PCLComboBoxRepresentable<Selection: Hashable>: NSViewRepresentable {
    let title: String
    @Binding var selection: Selection
    let options: [PCLComboBoxOption<Selection>]
    @Environment(\.isEnabled) private var isEnabled

    func makeCoordinator() -> Coordinator { Coordinator(selection: $selection) }

    func makeNSView(context: Context) -> PCLComboBoxControl {
        let control = PCLComboBoxControl(frame: .zero)
        let coordinator = context.coordinator
        control.chooseIndex = { [weak coordinator, weak control] index in
            guard let coordinator, let control else { return }
            coordinator.choose(index, in: control)
        }
        control.setContentHuggingPriority(.defaultLow, for: .horizontal)
        control.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        return control
    }

    func updateNSView(_ control: PCLComboBoxControl, context: Context) {
        let coordinator = context.coordinator
        coordinator.selection = $selection
        if coordinator.options != options {
            control.dismissPopup()
            coordinator.options = options
            control.items = options.map { PCLComboBoxItem(title: $0.title, isEnabled: $0.isEnabled) }
        }
        control.placeholder = title
        control.selectedIndex = options.firstIndex(where: { $0.value == selection })
        control.isEnabled = isEnabled && !options.isEmpty
        control.setAccessibilityLabel(title)
        control.setAccessibilityValue(control.displayedTitle)
        control.needsDisplay = true
    }

    static func dismantleNSView(_ control: PCLComboBoxControl, coordinator: Coordinator) {
        control.dismissPopup()
        control.chooseIndex = nil
    }

    func sizeThatFits(_ proposal: ProposedViewSize, nsView: PCLComboBoxControl, context: Context) -> CGSize? {
        CGSize(width: max(0, proposal.width ?? 240), height: 28)
    }

    final class Coordinator: NSObject {
        var selection: Binding<Selection>
        var options: [PCLComboBoxOption<Selection>] = []
        init(selection: Binding<Selection>) { self.selection = selection }

        func choose(_ index: Int, in control: PCLComboBoxControl) {
            guard options.indices.contains(index), options[index].isEnabled else { return }
            selection.wrappedValue = options[index].value
            // Custom-icon selection can synchronously open a file chooser or reject
            // a value. Read the saved binding again after that operation finishes.
            control.selectedIndex = options.firstIndex(where: { $0.value == selection.wrappedValue })
            control.setAccessibilityValue(control.displayedTitle)
            control.needsDisplay = true
        }
    }
}

private struct PCLComboBoxItem: Equatable {
    let title: String
    let isEnabled: Bool
}

private enum PCLComboBoxStyle {
    static let font = NSFont(name: "PCL English", size: 13) ?? .systemFont(ofSize: 13)
    // Default runtime theme: ThemeRefresh, hue 210 / saturation 85.
    static let text = NSColor(srgbRed: 0x33 / 255, green: 0x3e / 255, blue: 0x48 / 255, alpha: 1)
    static let border = NSColor(srgbRed: 0x12 / 255, green: 0x7a / 255, blue: 0xe1 / 255, alpha: 1)
    static let selected = NSColor(srgbRed: 0xcd / 255, green: 0xe4 / 255, blue: 0xfb / 255, alpha: 1)
    static let openBackground = NSColor(srgbRed: 0xe3 / 255, green: 0xf0 / 255, blue: 0xfd / 255, alpha: 1)
    static let hover = NSColor(srgbRed: 0xee / 255, green: 0xf6 / 255, blue: 0xfe / 255, alpha: 1)
    static let lineHeight = ceil(NSLayoutManager().defaultLineHeight(for: font))
    static let rowHeight = lineHeight + 8 // MyComboBoxItem: Padding="6,4".
}

private final class PCLComboBoxControl: NSControl {
    var items: [PCLComboBoxItem] = []
    var placeholder = ""
    var selectedIndex: Int? {
        didSet {
            popup?.list.selectedIndex = selectedIndex
            setAccessibilityValue(displayedTitle)
            needsDisplay = true
        }
    }
    var chooseIndex: ((Int) -> Void)?
    var displayedTitle: String {
        guard let selectedIndex, items.indices.contains(selectedIndex) else { return placeholder }
        return items[selectedIndex].title
    }
    private var hoverTracking: NSTrackingArea?
    private var isPointerInside = false
    private var popup: PCLComboBoxPanel?
    private var localMonitor: Any?
    private var globalMonitor: Any?
    private var notifications: [NSObjectProtocol] = []
    private let arrowLayer = CAShapeLayer()
    private var typeBuffer = ""
    private var lastTypeTime: TimeInterval = 0

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        focusRingType = .none
        wantsLayer = true
        arrowLayer.fillColor = NSColor.clear.cgColor
        arrowLayer.lineWidth = 1.5
        // Application.xaml ToggleButton.path: original flat 1.5-point stroke.
        arrowLayer.lineCap = .butt
        arrowLayer.lineJoin = .miter
        layer?.addSublayer(arrowLayer)
        setAccessibilityElement(true)
        setAccessibilityRole(.popUpButton)
        setAccessibilityHelp("按空格键展开，使用方向键选择，回车确认，Escape 取消")
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    override var isFlipped: Bool { true }
    override var acceptsFirstResponder: Bool { isEnabled }
    override var intrinsicContentSize: NSSize { NSSize(width: NSView.noIntrinsicMetric, height: 28) }
    override var alignmentRectInsets: NSEdgeInsets { NSEdgeInsetsZero }
    override var isEnabled: Bool {
        didSet {
            if !isEnabled { dismissPopup() }
            setAccessibilityEnabled(isEnabled)
            needsDisplay = true
        }
    }

    override func draw(_ dirtyRect: NSRect) {
        let focused = window?.firstResponder === self
        let active = isEnabled && (popup != nil || focused || isPointerInside)
        let foreground = isEnabled ? NSColor(calibratedRed: 0.35, green: 0.37, blue: 0.39, alpha: 1) : NSColor(calibratedWhite: 0.65, alpha: 1)
        let idleBorder = foreground.withAlphaComponent(0.65)
        let blue = NSColor(calibratedRed: 0.08, green: 0.46, blue: 0.81, alpha: 1)
        let borderColor = isEnabled ? (popup != nil ? PCLComboBoxStyle.border : (active ? blue : idleBorder)) : NSColor(calibratedWhite: 0.75, alpha: 1)
        let background = isEnabled ? (popup != nil ? PCLComboBoxStyle.openBackground : NSColor.white.withAlphaComponent(active ? 1 : 0.75)) : NSColor(calibratedWhite: 0.95, alpha: 1)
        let box = NSBezierPath(roundedRect: bounds.insetBy(dx: 0.5, dy: 0.5), xRadius: 3, yRadius: 3)
        background.setFill(); box.fill()
        borderColor.setStroke(); box.lineWidth = 1; box.stroke()

        let paragraph = NSMutableParagraphStyle()
        paragraph.lineBreakMode = .byTruncatingTail
        let label = NSAttributedString(string: displayedTitle, attributes: [.font: PCLComboBoxStyle.font, .foregroundColor: foreground, .paragraphStyle: paragraph])
        let textHeight = ceil(label.size().height)
        label.draw(with: NSRect(x: 8.5, y: bounds.midY - textHeight / 2, width: max(0, bounds.width - 30), height: textHeight), options: [.usesLineFragmentOrigin, .truncatesLastVisibleLine])

        CATransaction.begin()
        CATransaction.setDisableActions(true)
        arrowLayer.frame = NSRect(x: bounds.maxX - 17.5, y: bounds.midY - 2.75, width: 9, height: 5.5)
        let arrow = CGMutablePath()
        // Exact WPF Data="M1,1L4.5,4.5L8,1"; rotation remains 180 degrees.
        arrow.move(to: CGPoint(x: 1, y: 1))
        arrow.addLine(to: CGPoint(x: 4.5, y: 4.5))
        arrow.addLine(to: CGPoint(x: 8, y: 1))
        arrowLayer.path = arrow
        arrowLayer.strokeColor = (active ? borderColor : foreground).cgColor
        CATransaction.commit()
    }

    override func mouseDown(with event: NSEvent) {
        guard isEnabled else { return }
        window?.makeFirstResponder(self)
        togglePopup()
    }
    override func performClick(_ sender: Any?) {
        guard isEnabled else { return }
        window?.makeFirstResponder(self)
        togglePopup()
    }
    override func accessibilityPerformPress() -> Bool {
        guard isEnabled else { return false }
        performClick(nil)
        return true
    }
    override func isAccessibilityExpanded() -> Bool { popup != nil }
    // A borderless non-key child panel is intentionally absent from the normal
    // window focus order. Expose its list through the expanded field instead.
    override func accessibilityChildren() -> [Any]? { popup.map { [$0.list] } ?? [] }
    override func accessibilityLinkedUIElements() -> [Any]? { accessibilityChildren() }

    override func keyDown(with event: NSEvent) {
        if handleKey(event) { return }
        super.keyDown(with: event)
    }

    private func handleKey(_ event: NSEvent) -> Bool {
        if event.modifierFlags.contains(.command) || event.modifierFlags.contains(.control) {
            dismissPopup()
            return false
        }
        switch event.keyCode {
        case 53: // Escape
            if popup != nil { dismissPopup(); return true }
            return false
        case 48: // Keep traversal in the host window, never in the floating list.
            dismissPopup()
            window?.recalculateKeyViewLoop()
            if event.modifierFlags.contains(.shift) { window?.selectPreviousKeyView(self) }
            else { window?.selectNextKeyView(self) }
            return true
        case 36, 76, 49: // Return / keypad Enter / Space
            if let popup {
                if let index = popup.list.activeIndex ?? selectedIndex { commit(index) }
                else { dismissPopup() }
            } else { showPopup() }
            return true
        case 125, 126, 115, 119, 116, 121: // Arrows / Home / End / Page Up / Page Down
            if popup == nil { showPopup() }
            guard let list = popup?.list else { return true }
            switch event.keyCode {
            case 125: list.moveActive(by: 1)
            case 126: list.moveActive(by: -1)
            case 115: list.activateBoundary(last: false)
            case 119: list.activateBoundary(last: true)
            case 116: list.moveActive(by: -max(1, Int(list.viewportHeight / PCLComboBoxStyle.rowHeight)))
            default: list.moveActive(by: max(1, Int(list.viewportHeight / PCLComboBoxStyle.rowHeight)))
            }
            return true
        default:
            guard let characters = event.characters, !characters.isEmpty,
                  characters.unicodeScalars.allSatisfy({ !CharacterSet.controlCharacters.contains($0) }) else { return false }
            if popup == nil { showPopup() }
            guard let list = popup?.list else { return false }
            let now = ProcessInfo.processInfo.systemUptime
            if now - lastTypeTime > 0.8 { typeBuffer = "" }
            lastTypeTime = now
            let cycle = typeBuffer.count == 1 && characters.count == 1
                && typeBuffer.folding(options: [.caseInsensitive, .diacriticInsensitive, .widthInsensitive], locale: .current)
                    == characters.folding(options: [.caseInsensitive, .diacriticInsensitive, .widthInsensitive], locale: .current)
            if cycle { typeBuffer = "" }
            typeBuffer += characters
            list.activatePrefix(typeBuffer, cycle: cycle)
            return true
        }
    }

    private func togglePopup() {
        if popup == nil { showPopup() } else { dismissPopup() }
    }

    private func showPopup() {
        guard popup == nil, isEnabled, !items.isEmpty, let host = window,
              !visibleRect.isEmpty else { return }
        // Only one selector may be open, including selectors in another window.
        NotificationCenter.default.post(name: .pclComboBoxWillOpen, object: self)
        let anchor = host.convertToScreen(convert(bounds, to: nil))
        let screen = host.screen ?? NSScreen.main
        let screenFrame = screen?.visibleFrame ?? host.frame
        let desiredHeight = min(320, CGFloat(items.count) * PCLComboBoxStyle.rowHeight + 2)
        let belowSpace = max(0, anchor.minY + 1.5 - screenFrame.minY)
        let aboveSpace = max(0, screenFrame.maxY - anchor.maxY + 1.5)
        let below = belowSpace >= desiredHeight || belowSpace >= aboveSpace
        let height = min(desiredHeight, below ? belowSpace : aboveSpace)
        guard height > 2 else { return }
        let width = bounds.width
        let originX = min(max(anchor.minX, screenFrame.minX), max(screenFrame.minX, screenFrame.maxX - width))
        let originY = below ? anchor.minY + 1.5 - height : anchor.maxY - 1.5
        let panel = PCLComboBoxPanel(contentRect: NSRect(x: originX, y: originY, width: width, height: height), items: items, selectedIndex: selectedIndex)
        panel.title = (accessibilityLabel() ?? placeholder) + "选项"
        panel.setAccessibilityLabel(panel.title)
        panel.list.accessibilityOwner = self
        panel.list.onChoose = { [weak self] index in self?.commit(index) }
        panel.list.onCancel = { [weak self] in self?.dismissPopup() }
        popup = panel
        animateArrow(expanded: true)
        typeBuffer = ""
        host.addChildWindow(panel, ordered: .above)
        panel.alphaValue = 0
        panel.orderFront(nil)
        panel.list.revealActive()
        NSAnimationContext.runAnimationGroup { context in
            context.duration = 0.1
            panel.animator().alphaValue = 1
        }
        installDismissalObservers(host: host, panel: panel)
        needsDisplay = true
        NSAccessibility.post(element: self, notification: .layoutChanged)
    }

    private func commit(_ index: Int?) {
        guard let index, items.indices.contains(index), items[index].isEnabled else { return }
        // Tear down the child window and tracking before a binding can open a
        // modal custom-icon chooser or replace the page that owns this control.
        dismissPopup()
        chooseIndex?(index)
    }

    func dismissPopup() {
        if let localMonitor { NSEvent.removeMonitor(localMonitor); self.localMonitor = nil }
        if let globalMonitor { NSEvent.removeMonitor(globalMonitor); self.globalMonitor = nil }
        notifications.forEach { NotificationCenter.default.removeObserver($0) }
        notifications.removeAll()
        guard let panel = popup else { return }
        panel.list.accessibilityOwner = nil
        panel.list.onChoose = nil
        panel.list.onCancel = nil
        panel.parent?.removeChildWindow(panel)
        panel.orderOut(nil)
        popup = nil
        animateArrow(expanded: false)
        typeBuffer = ""
        needsDisplay = true
        NSAccessibility.post(element: self, notification: .layoutChanged)
    }

    private func animateArrow(expanded: Bool) {
        let target: CGFloat = expanded ? .pi : 0
        let current = arrowLayer.presentation()?.value(forKeyPath: "transform.rotation.z") ?? arrowLayer.value(forKeyPath: "transform.rotation.z")
        let animation = CABasicAnimation(keyPath: "transform.rotation.z")
        animation.fromValue = current
        animation.toValue = target
        animation.duration = 0.2
        animation.timingFunction = CAMediaTimingFunction(name: .easeOut)
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        arrowLayer.setAffineTransform(CGAffineTransform(rotationAngle: target))
        CATransaction.commit()
        arrowLayer.add(animation, forKey: "rotation")
    }

    private func installDismissalObservers(host: NSWindow, panel: PCLComboBoxPanel) {
        let mask: NSEvent.EventTypeMask = [.leftMouseDown, .rightMouseDown, .otherMouseDown, .scrollWheel, .keyDown]
        localMonitor = NSEvent.addLocalMonitorForEvents(matching: mask) { [weak self, weak panel, weak host] event in
            guard let self, let panel, self.popup === panel else { return event }
            if event.type == .keyDown { return self.handleKey(event) ? nil : event }
            if event.window === panel { return event }
            if event.type == .leftMouseDown, event.window === host,
               self.bounds.contains(self.convert(event.locationInWindow, from: nil)) { return event }
            self.dismissPopup()
            return event
        }
        globalMonitor = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown, .otherMouseDown]) { [weak self] _ in
            self?.dismissPopup()
        }
        let center = NotificationCenter.default
        for name in [NSWindow.willMoveNotification, NSWindow.didResizeNotification, NSWindow.willMiniaturizeNotification,
                     NSWindow.willCloseNotification, NSWindow.didResignKeyNotification] {
            notifications.append(center.addObserver(forName: name, object: host, queue: .main) { [weak self] _ in self?.dismissPopup() })
        }
        notifications.append(center.addObserver(forName: NSApplication.didResignActiveNotification, object: nil, queue: .main) { [weak self] _ in self?.dismissPopup() })
        notifications.append(center.addObserver(forName: .pclComboBoxWillOpen, object: nil, queue: .main) { [weak self] notification in
            guard let self, notification.object as AnyObject? !== self else { return }
            self.dismissPopup()
        })
    }

    override func viewWillMove(toWindow newWindow: NSWindow?) {
        if newWindow !== window { dismissPopup() }
        super.viewWillMove(toWindow: newWindow)
    }
    override func viewDidHide() { dismissPopup(); super.viewDidHide() }
    override func setFrameSize(_ newSize: NSSize) {
        if newSize != frame.size { dismissPopup() }
        super.setFrameSize(newSize)
    }
    override func setFrameOrigin(_ newOrigin: NSPoint) {
        if newOrigin != frame.origin { dismissPopup() }
        super.setFrameOrigin(newOrigin)
    }
    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let hoverTracking { removeTrackingArea(hoverTracking) }
        let area = NSTrackingArea(rect: .zero, options: [.activeInKeyWindow, .mouseEnteredAndExited, .inVisibleRect], owner: self)
        addTrackingArea(area)
        hoverTracking = area
    }
    override func mouseEntered(with event: NSEvent) { isPointerInside = true; needsDisplay = true }
    override func mouseExited(with event: NSEvent) { isPointerInside = false; needsDisplay = true }
    override func becomeFirstResponder() -> Bool { needsDisplay = true; return true }
    override func resignFirstResponder() -> Bool { dismissPopup(); needsDisplay = true; return true }
    deinit {
        if let localMonitor { NSEvent.removeMonitor(localMonitor) }
        if let globalMonitor { NSEvent.removeMonitor(globalMonitor) }
        notifications.forEach { NotificationCenter.default.removeObserver($0) }
    }
}

private extension Notification.Name {
    static let pclComboBoxWillOpen = Notification.Name("PCLComboBoxWillOpen")
}

private final class PCLComboBoxPanel: NSPanel {
    let list: PCLComboBoxList
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }

    init(contentRect: NSRect, items: [PCLComboBoxItem], selectedIndex: Int?) {
        list = PCLComboBoxList(frame: NSRect(origin: .zero, size: contentRect.size), items: items, selectedIndex: selectedIndex)
        super.init(contentRect: contentRect, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        isReleasedWhenClosed = false
        hidesOnDeactivate = false
        acceptsMouseMovedEvents = true
        animationBehavior = .none
        collectionBehavior = [.fullScreenAuxiliary, .transient]
        contentView = list
    }
}

private final class PCLComboBoxList: NSView {
    let scrollView = NSScrollView()
    weak var accessibilityOwner: NSView?
    private let document = PCLComboBoxDocument()
    private let scrollbar = PCLComboBoxScrollbar()
    private var rows: [PCLComboBoxRow] = []
    private let items: [PCLComboBoxItem]
    var selectedIndex: Int? { didSet { refreshRows() } }
    private(set) var activeIndex: Int?
    var onChoose: ((Int) -> Void)?
    var onCancel: (() -> Void)?
    private var boundsObserver: NSObjectProtocol?
    var viewportHeight: CGFloat { scrollView.contentView.bounds.height }
    override var isFlipped: Bool { true }

    init(frame: NSRect, items: [PCLComboBoxItem], selectedIndex: Int?) {
        self.items = items
        self.selectedIndex = selectedIndex
        activeIndex = selectedIndex.flatMap { items.indices.contains($0) && items[$0].isEnabled ? $0 : nil } ?? items.firstIndex(where: \.isEnabled)
        super.init(frame: frame)
        setAccessibilityElement(true)
        setAccessibilityRole(.list)
        setAccessibilityLabel("下拉选项")
        wantsLayer = true
        layer?.cornerRadius = 3
        layer?.masksToBounds = true
        scrollView.drawsBackground = false
        scrollView.automaticallyAdjustsContentInsets = false
        scrollView.contentInsets = NSEdgeInsetsZero
        scrollView.scrollerInsets = NSEdgeInsetsZero
        scrollView.contentView.automaticallyAdjustsContentInsets = false
        scrollView.contentView.contentInsets = NSEdgeInsetsZero
        scrollView.borderType = .noBorder
        scrollView.hasVerticalScroller = false
        scrollView.hasHorizontalScroller = false
        scrollView.verticalScrollElasticity = .none
        scrollView.horizontalScrollElasticity = .none
        scrollView.contentView.postsBoundsChangedNotifications = true
        scrollView.documentView = document
        addSubview(scrollView)
        addSubview(scrollbar)
        scrollbar.setAccessibilityParent(self)
        scrollbar.onScroll = { [weak self] fraction in
            guard let self else { return }
            let range = max(0, self.document.bounds.height - self.viewportHeight)
            self.scrollView.contentView.scroll(to: NSPoint(x: 0, y: range * fraction))
            self.scrollView.reflectScrolledClipView(self.scrollView.contentView)
        }
        for (index, item) in items.enumerated() {
            let row = PCLComboBoxRow(item: item)
            row.accessibilityList = self
            row.onChoose = { [weak self] in self?.onChoose?(index) }
            row.onHover = { [weak self] inside in
                guard let self else { return }
                if inside { self.activeIndex = index }
                else if self.activeIndex == index { self.activeIndex = nil }
                self.refreshRows()
            }
            rows.append(row)
            document.addSubview(row)
        }
        boundsObserver = NotificationCenter.default.addObserver(forName: NSView.boundsDidChangeNotification, object: scrollView.contentView, queue: .main) { [weak self] _ in self?.updateScrollPosition() }
        layoutList()
        refreshRows(animated: false)
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    override func draw(_ dirtyRect: NSRect) {
        let box = NSBezierPath(roundedRect: bounds.insetBy(dx: 0.5, dy: 0.5), xRadius: 3, yRadius: 3)
        NSColor.white.setFill(); box.fill()
        PCLComboBoxStyle.border.setStroke(); box.lineWidth = 1; box.stroke()
    }
    override func layout() { super.layout(); layoutList() }
    private func layoutList() {
        let documentHeight = CGFloat(rows.count) * PCLComboBoxStyle.rowHeight
        let needsScrollbar = documentHeight > bounds.height - 2
        let scrollbarSpace: CGFloat = needsScrollbar ? 11 : 0 // Original margin 2,1,1,1 + width 8.
        scrollView.frame = NSRect(x: 1, y: 1, width: max(0, bounds.width - 2 - scrollbarSpace), height: max(0, bounds.height - 2))
        document.frame = NSRect(x: 0, y: 0, width: scrollView.contentSize.width, height: documentHeight)
        for (index, row) in rows.enumerated() {
            row.frame = NSRect(x: 0, y: CGFloat(index) * PCLComboBoxStyle.rowHeight, width: document.bounds.width, height: PCLComboBoxStyle.rowHeight)
        }
        scrollbar.isHidden = !needsScrollbar
        scrollbar.frame = NSRect(x: bounds.width - 10, y: 2, width: 8, height: max(0, bounds.height - 4))
        updateScrollPosition()
    }
    private func updateScrollPosition() {
        let range = max(0, document.bounds.height - viewportHeight)
        scrollbar.fraction = range > 0 ? min(1, max(0, scrollView.contentView.bounds.minY / range)) : 0
        scrollbar.proportion = document.bounds.height > 0 ? min(1, viewportHeight / document.bounds.height) : 1
    }
    private func refreshRows(animated: Bool = true) {
        for (index, row) in rows.enumerated() {
            row.setState(selected: index == selectedIndex, hovered: index == activeIndex, animated: animated)
        }
    }
    func revealActive() {
        guard let activeIndex, rows.indices.contains(activeIndex) else { return }
        document.scrollToVisible(rows[activeIndex].frame)
        updateScrollPosition()
    }
    func moveActive(by delta: Int) {
        let enabled = items.indices.filter { items[$0].isEnabled }
        guard !enabled.isEmpty else { return }
        let position = (activeIndex ?? selectedIndex).flatMap { enabled.firstIndex(of: $0) } ?? (delta > 0 ? -1 : enabled.count)
        activeIndex = enabled[min(enabled.count - 1, max(0, position + delta))]
        refreshRows()
        revealActive()
    }
    func activateBoundary(last: Bool) {
        activeIndex = last ? items.lastIndex(where: \.isEnabled) : items.firstIndex(where: \.isEnabled)
        refreshRows()
        revealActive()
    }
    func activatePrefix(_ prefix: String, cycle: Bool = false) {
        func normalized(_ text: String) -> String {
            text.folding(options: [.caseInsensitive, .diacriticInsensitive, .widthInsensitive], locale: .current)
        }
        let query = normalized(prefix)
        guard !query.isEmpty else { return }
        if !cycle {
            // An exact version wins over a longer prerelease sharing its prefix.
            if let exact = items.indices.first(where: { items[$0].isEnabled && normalized(items[$0].title) == query }) {
                activeIndex = exact
                refreshRows()
                revealActive()
                return
            }
            if let activeIndex, items.indices.contains(activeIndex), items[activeIndex].isEnabled,
               normalized(items[activeIndex].title).hasPrefix(query) {
                revealActive()
                return
            }
        }
        let start = cycle ? (activeIndex ?? selectedIndex ?? -1) + 1 : 0
        for step in 0..<items.count {
            let index = (start + step) % items.count
            if items[index].isEnabled && normalized(items[index].title).hasPrefix(query) {
                activeIndex = index
                refreshRows()
                revealActive()
                return
            }
        }
    }
    override func accessibilityParent() -> Any? { accessibilityOwner ?? super.accessibilityParent() }
    override func accessibilityChildren() -> [Any]? {
        var children: [Any] = rows
        if !scrollbar.isHidden { children.append(scrollbar) }
        return children
    }
    override func accessibilitySelectedChildren() -> [Any]? {
        guard let selectedIndex, rows.indices.contains(selectedIndex) else { return [] }
        return [rows[selectedIndex]]
    }
    override func scrollWheel(with event: NSEvent) { scrollView.scrollWheel(with: event) }
    override func accessibilityPerformCancel() -> Bool { onCancel?(); return true }
    deinit { if let boundsObserver { NotificationCenter.default.removeObserver(boundsObserver) } }
}

private final class PCLComboBoxDocument: NSView {
    override var isFlipped: Bool { true }
}

private final class PCLComboBoxRow: NSView {
    let item: PCLComboBoxItem
    weak var accessibilityList: PCLComboBoxList?
    var onChoose: (() -> Void)?
    var onHover: ((Bool) -> Void)?
    private var tracking: NSTrackingArea?
    private var isSelected = false
    private var isHovered = false
    override var isFlipped: Bool { true }

    init(item: PCLComboBoxItem) {
        self.item = item
        super.init(frame: .zero)
        wantsLayer = true
        setAccessibilityElement(true)
        setAccessibilityRole(.button)
        setAccessibilityLabel(item.title)
        setAccessibilityEnabled(item.isEnabled)
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    func setState(selected: Bool, hovered: Bool, animated: Bool) {
        guard selected != isSelected || hovered != isHovered || layer?.backgroundColor == nil else { return }
        isSelected = selected
        isHovered = hovered
        let color = selected ? PCLComboBoxStyle.selected : (hovered && item.isEnabled ? PCLComboBoxStyle.hover : NSColor.clear)
        if animated {
            let animation = CABasicAnimation(keyPath: "backgroundColor")
            animation.fromValue = layer?.presentation()?.backgroundColor ?? layer?.backgroundColor
            animation.toValue = color.cgColor
            animation.duration = selected || (hovered && item.isEnabled) ? 0.1 : 0.3
            layer?.add(animation, forKey: "backgroundColor")
        }
        layer?.backgroundColor = color.cgColor
        setAccessibilityValue(selected ? "已选中" : "")
        needsDisplay = true
    }
    override func draw(_ dirtyRect: NSRect) {
        let paragraph = NSMutableParagraphStyle()
        paragraph.lineBreakMode = .byTruncatingTail
        let opacity: CGFloat = isSelected || item.isEnabled ? 1 : 0.4
        let label = NSAttributedString(string: item.title, attributes: [.font: PCLComboBoxStyle.font, .foregroundColor: PCLComboBoxStyle.text.withAlphaComponent(opacity), .paragraphStyle: paragraph])
        label.draw(with: NSRect(x: 6, y: 4, width: max(0, bounds.width - 12), height: PCLComboBoxStyle.lineHeight), options: [.usesLineFragmentOrigin, .truncatesLastVisibleLine])
    }
    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let tracking { removeTrackingArea(tracking) }
        let area = NSTrackingArea(rect: .zero, options: [.activeAlways, .mouseEnteredAndExited, .inVisibleRect], owner: self)
        addTrackingArea(area)
        tracking = area
    }
    override func accessibilityParent() -> Any? { accessibilityList ?? super.accessibilityParent() }
    override func mouseEntered(with event: NSEvent) { if item.isEnabled { onHover?(true) } }
    override func mouseExited(with event: NSEvent) { onHover?(false) }
    override func mouseDown(with event: NSEvent) { /* The source commits on mouse-up. */ }
    override func mouseUp(with event: NSEvent) {
        if item.isEnabled && bounds.contains(convert(event.locationInWindow, from: nil)) { onChoose?() }
    }
    override func accessibilityPerformPress() -> Bool {
        guard item.isEnabled else { return false }
        onChoose?()
        return true
    }
}

/// A source-style 8-point scroll rail with a 4-point thumb, independent of macOS
/// scrollbar preferences and overlay-menu appearances.
private final class PCLComboBoxScrollbar: NSControl {
    var fraction: CGFloat = 0 { didSet { needsDisplay = true } }
    var proportion: CGFloat = 1 { didSet { needsDisplay = true } }
    var onScroll: ((CGFloat) -> Void)?
    private var dragOffset: CGFloat?
    private var isPointerInside = false
    private var tracking: NSTrackingArea?
    override var isFlipped: Bool { true }
    private var thumbRect: NSRect {
        let height = min(bounds.height, max(20, bounds.height * proportion))
        return NSRect(x: 2, y: max(0, bounds.height - height) * min(1, max(0, fraction)), width: max(0, bounds.width - 4), height: height)
    }
    override init(frame: NSRect) {
        super.init(frame: frame)
        setAccessibilityElement(true)
        setAccessibilityRole(.scrollBar)
        setAccessibilityLabel("滚动下拉选项")
        setAccessibilityOrientation(.vertical)
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    override func draw(_ dirtyRect: NSRect) {
        PCLComboBoxStyle.border.withAlphaComponent(dragOffset != nil ? 1 : (isPointerInside ? 0.9 : 0.5)).setFill()
        NSBezierPath(roundedRect: thumbRect, xRadius: 2, yRadius: 2).fill()
    }
    override func mouseDown(with event: NSEvent) {
        let point = convert(event.locationInWindow, from: nil)
        dragOffset = thumbRect.contains(point) ? point.y - thumbRect.minY : thumbRect.height / 2
        updateDrag(point.y)
    }
    override func mouseDragged(with event: NSEvent) { updateDrag(convert(event.locationInWindow, from: nil).y) }
    override func mouseUp(with event: NSEvent) { dragOffset = nil; needsDisplay = true }
    private func updateDrag(_ y: CGFloat) {
        let travel = bounds.height - thumbRect.height
        guard travel > 0 else { return }
        onScroll?(min(1, max(0, (y - (dragOffset ?? 0)) / travel)))
    }
    override func accessibilityValue() -> Any? { Double(fraction) }
    override func accessibilityMinValue() -> Any? { 0.0 }
    override func accessibilityMaxValue() -> Any? { 1.0 }
    override func accessibilityPerformIncrement() -> Bool { onScroll?(min(1, fraction + 0.1)); return true }
    override func accessibilityPerformDecrement() -> Bool { onScroll?(max(0, fraction - 0.1)); return true }
    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let tracking { removeTrackingArea(tracking) }
        let area = NSTrackingArea(rect: .zero, options: [.activeAlways, .mouseEnteredAndExited, .inVisibleRect], owner: self)
        addTrackingArea(area); tracking = area
    }
    override func mouseEntered(with event: NSEvent) { isPointerInside = true; needsDisplay = true }
    override func mouseExited(with event: NSEvent) { isPointerInside = false; needsDisplay = true }
}
