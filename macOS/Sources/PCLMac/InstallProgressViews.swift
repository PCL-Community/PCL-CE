import AppKit
import PCLCore
import SwiftUI

@MainActor extension LauncherStore {
    var showsClassicInstallProgress: Bool {
        installingID != nil || installProgress?.snapshot?.stages.contains(where: {
            $0.state == .waiting || $0.state == .running || $0.state == .failed
        }) == true
    }
}

/// FormMain.BtnExtraDownload / MyExtraButton: reopen the task manager after
/// returning to another page while a download is still active.
struct PCLTaskManagerButton: View {
    @EnvironmentObject private var store: LauncherStore
    @State private var hovering = false

    private var fraction: Double {
        min(1, max(0, store.installProgress?.snapshot?.totalFraction ?? store.installProgress?.fraction ?? 0))
    }

    var body: some View {
        Button { store.page = .activity } label: {
            ZStack(alignment: .bottom) {
                Circle().fill(Color.pclBlue)
                Color.white.opacity(47.0 / 255.0)
                    .frame(height: 40 * fraction)
                PCLIcon("TaskManager").foregroundStyle(Color.white)
                    .frame(width: 16, height: 16).scaleEffect(1.1)
                    .frame(width: 40, height: 40)
            }
            .frame(width: 40, height: 40).clipShape(Circle())
            .shadow(color: .black.opacity(0.2), radius: 5)
            .scaleEffect(hovering ? 1.08 : 1)
            .padding(.top, 10)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain).help("任务管理")
        .accessibilityLabel("任务管理")
        .accessibilityValue("总进度 \(Int(floor(fraction * 100)))%")
        .onHover { hovering = $0 }
        .animation(.easeOut(duration: 0.12), value: hovering)
    }
}

/// PageSpeedLeft: the original task manager uses a fixed 200-point sidebar.
struct PCLInstallProgressSidebar: View {
    @EnvironmentObject private var store: LauncherStore

    var body: some View {
        GeometryReader { geometry in
            let flexible = max(0, (geometry.size.height - 3 * 63) / 3.2)
            VStack(spacing: 0) {
                Color.clear.frame(height: flexible)
                metric("总进度", value: percent)
                Color.clear.frame(height: flexible * 0.6)
                metric("下载速度", value: speed)
                Color.clear.frame(height: flexible * 0.6)
                metric("剩余文件", value: remaining)
                Color.clear.frame(height: flexible)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        }
        .background(ClassicTheme.panel)
    }

    private var snapshot: InstallTaskSnapshot? { store.installProgress?.snapshot }

    private var percent: String {
        let fraction = min(1, max(0, snapshot?.totalFraction ?? store.installProgress?.fraction ?? 0))
        if fraction > 0.999999 { return "100 %" }
        return String(format: "%.2f %%", floor(fraction * 10_000) / 100)
    }

    private var speed: String {
        guard let snapshot else { return "—" }
        return PCLTransferSpeed.string(snapshot.bytesPerSecond)
    }

    private var remaining: String {
        guard let snapshot else { return "—" }
        return String(max(0, snapshot.remainingFiles))
    }

    private func metric(_ title: String, value: String) -> some View {
        VStack(spacing: 0) {
            Text(title)
                .font(.custom("PCL English", size: 14))
                .foregroundStyle(Color.pclBlue)
                .frame(height: 20)
            LinearGradient(stops: [
                .init(color: ClassicTheme.panel, location: 0),
                .init(color: .pclBlue, location: 0.02),
                .init(color: .pclBlue, location: 0.98),
                .init(color: ClassicTheme.panel, location: 1)
            ], startPoint: .leading, endPoint: .trailing)
            .frame(height: 2).padding(.vertical, 7).padding(.horizontal, 25)
            Text(value)
                .font(.custom("PCL English", size: 20))
                .foregroundStyle(ClassicTheme.text)
                .monospacedDigit()
                .lineLimit(1).minimumScaleFactor(0.7)
                .frame(height: 27)
        }
        .frame(maxWidth: .infinity)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("\(title)：\(value)")
    }
}

/// PageSpeedRight / PageSpeedLeft.TaskRefresh: one white task card with a
/// percentage, blue tick, or waiting dots for every genuinely tracked stage.
struct PCLDownloadProgressCard: View {
    @EnvironmentObject private var store: LauncherStore
    @State private var hovering = false
    @State private var copiedError = false
    @FocusState private var cancelFocused: Bool

    var body: some View {
        ZStack(alignment: .topLeading) {
            VStack(alignment: .leading, spacing: 0) {
                if let errorDetails {
                    HStack(alignment: .top, spacing: 0) {
                        PCLIcon("TaskFailure").foregroundStyle(Color.pclBlue)
                            .frame(width: 15, height: 15).padding(.top, 1)
                            .frame(width: 50, alignment: .top)
                        Button { copyError(errorDetails) } label: {
                            Text(errorDetails)
                                .font(.custom("PCL English", size: 13))
                                .foregroundStyle(ClassicTheme.text)
                                .multilineTextAlignment(.leading)
                                .fixedSize(horizontal: false, vertical: true)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }
                        .buttonStyle(.plain)
                        .help(copiedError ? "已复制错误详情" : "单击复制错误详情")
                        .accessibilityLabel("复制错误详情：\(errorDetails)")
                    }
                    .padding(.bottom, 5)
                } else if let snapshot = store.installProgress?.snapshot {
                    ForEach(snapshot.stages.filter(\.isVisible)) { stage in
                        PCLInstallStageRow(title: stage.title, state: stage.state, fraction: stage.fraction)
                    }
                } else {
                    // Older installer callers expose only one real progress item.
                    // Do not fabricate completed or simultaneous stages for them.
                    PCLInstallStageRow(title: store.installProgress?.message ?? "获取版本信息",
                        state: .running, fraction: store.installProgress?.fraction ?? 0)
                }
            }
            .padding(.top, 40).padding(.leading, 14).padding(.trailing, 15).padding(.bottom, 10)
            Text(title)
                .font(.custom("PCL English", size: 13).weight(.bold))
                .foregroundStyle(Color.pclBlue)
                .lineLimit(1).truncationMode(.tail)
                .padding(.leading, 15).padding(.trailing, 38)
                .padding(.top, 12)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Color.white.opacity(245.0 / 255.0), in: RoundedRectangle(cornerRadius: 5))
        .shadow(color: Color.pclBlue.opacity(hovering ? 0.4 : 0.07), radius: 3, y: 1)
        .overlay(alignment: .topTrailing) {
            Button { store.cancelInstall() } label: {
                PCLIcon("TaskCancel").foregroundStyle(Color.pclBlue)
                    .frame(width: 12, height: 12)
                    .frame(width: 20, height: 20)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .focused($cancelFocused)
            .keyboardShortcut(".", modifiers: .command)
            .help(errorDetails == nil ? "取消" : "关闭")
            .accessibilityLabel(errorDetails == nil ? "取消安装" : "关闭错误详情")
            .opacity(hovering || cancelFocused ? 1 : 0)
            .padding(.top, 10).padding(.trailing, 10)
        }
        .onHover { hovering = $0 }
        .onChange(of: errorDetails) { _, _ in copiedError = false }
        .animation(.easeOut(duration: 0.09), value: hovering)
    }

    private var title: String {
        store.installProgress?.snapshot?.title ?? "Minecraft 安装：\(store.installingID ?? "")"
    }

    private var errorDetails: String? {
        guard let progress = store.installProgress,
              progress.snapshot?.stages.contains(where: { $0.state == .failed }) == true else { return nil }
        return progress.message
    }

    private func copyError(_ details: String) {
        NSPasteboard.general.clearContents()
        copiedError = NSPasteboard.general.setString(details, forType: .string)
    }
}

private struct PCLInstallStageRow: View {
    let title: String
    let state: InstallStageState
    let fraction: Double

    var body: some View {
        HStack(alignment: .top, spacing: 0) {
            status.frame(width: 50, height: 26, alignment: .top)
            Text(title)
                .font(.custom("PCL English", size: 13))
                .foregroundStyle(ClassicTheme.text)
                .lineLimit(1).truncationMode(.tail)
                .frame(maxWidth: .infinity, minHeight: 26, alignment: .topLeading)
        }
        .frame(height: 26)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("\(title)，\(statusDescription)")
    }

    @ViewBuilder private var status: some View {
        switch state {
        case .waiting:
            PCLIcon("TaskWaiting")
                .foregroundStyle(Color.pclBlue).frame(width: 18, height: 6).padding(.top, 7)
        case .running:
            Text("\(Int(floor(min(1, max(0, fraction)) * 100)))%")
                .font(.custom("PCL English", size: 13))
                .foregroundStyle(Color.pclBlue).monospacedDigit()
        case .completed:
            PCLIcon("TaskSuccess").foregroundStyle(Color.pclBlue)
                .aspectRatio(38 / 31.6665, contentMode: .fit)
                .frame(width: 15, height: 16).padding(.top, 3)
        case .failed, .cancelled:
            PCLIcon("TaskFailure").foregroundStyle(Color.pclBlue)
                .frame(width: 15, height: 15).padding(.top, 1)
        }
    }

    private var statusDescription: String {
        switch state {
        case .waiting: return "等待中"
        case .running: return "\(Int(floor(min(1, max(0, fraction)) * 100)))%"
        case .completed: return "已完成"
        case .failed: return "失败"
        case .cancelled: return "已取消"
        }
    }
}

private enum PCLTransferSpeed {
    static func string(_ raw: Double) -> String {
        let bytes = raw.isFinite ? max(0, raw) : 0
        if bytes < 1024 { return "\(Int(bytes.rounded(.down))) B/s" }
        for (unit, divisor) in [("K", 1024.0), ("M", 1_048_576.0), ("G", 1_073_741_824.0)] {
            if bytes < divisor * 1024 || unit == "G" {
                return String(format: "%.1f %@/s", bytes / divisor, unit)
            }
        }
        return "0 B/s"
    }
}
