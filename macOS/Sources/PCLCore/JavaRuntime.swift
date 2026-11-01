import Foundation
import Darwin

enum NativeProcess {
    struct Result {
        let status: Int32
        let output: Data
        var text: String { String(decoding: output, as: UTF8.self) }
    }

    static func capture(_ executable: URL, _ arguments: [String], timeout: TimeInterval = 8) throws -> Result {
        let process = Process()
        let pipe = Pipe()
        process.executableURL = executable
        process.arguments = arguments
        process.standardOutput = pipe
        process.standardError = pipe
        process.standardInput = FileHandle.nullDevice
        try process.run()
        let deadline = DispatchWorkItem {
            if process.isRunning { kill(process.processIdentifier, SIGKILL) }
        }
        DispatchQueue.global(qos: .utility).asyncAfter(deadline: .now() + timeout, execute: deadline)
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit()
        deadline.cancel()
        return Result(status: process.terminationStatus, output: data)
    }
}

public enum JavaDiscovery {
    public static func discover() -> [JavaRuntime] {
        let fm = FileManager.default
        var candidates: [URL] = []
        let user = fm.homeDirectoryForCurrentUser
        let roots = [URL(fileURLWithPath: "/Library/Java/JavaVirtualMachines"), user.appendingPathComponent("Library/Java/JavaVirtualMachines")]
        for root in roots {
            for bundle in (try? fm.contentsOfDirectory(at: root, includingPropertiesForKeys: nil, options: [.skipsHiddenFiles])) ?? [] {
                candidates.append(bundle.appendingPathComponent("Contents/Home/bin/java"))
            }
        }
        for prefix in ["/opt/homebrew/opt", "/usr/local/opt"] {
            for folder in (try? fm.contentsOfDirectory(at: URL(fileURLWithPath: prefix), includingPropertiesForKeys: nil, options: [.skipsHiddenFiles])) ?? [] where folder.lastPathComponent.hasPrefix("openjdk") {
                candidates.append(folder.appendingPathComponent("libexec/openjdk.jdk/Contents/Home/bin/java"))
                candidates.append(folder.appendingPathComponent("bin/java"))
            }
        }
        candidates.append(URL(fileURLWithPath: "/Applications/Android Studio.app/Contents/jbr/Contents/Home/bin/java"))
        if let javaHome = ProcessInfo.processInfo.environment["JAVA_HOME"], !javaHome.isEmpty {
            candidates.append(URL(fileURLWithPath: javaHome).appendingPathComponent("bin/java"))
        }
        if let result = try? NativeProcess.capture(URL(fileURLWithPath: "/usr/libexec/java_home"), ["-V"], timeout: 4),
           let regex = try? NSRegularExpression(pattern: #"(/[^\n]+/Contents/Home)"#) {
            let text = result.text as NSString
            for match in regex.matches(in: result.text, range: NSRange(location: 0, length: text.length)) {
                candidates.append(URL(fileURLWithPath: text.substring(with: match.range(at: 1))).appendingPathComponent("bin/java"))
            }
        }
        var seen = Set<String>()
        return candidates.compactMap { candidate -> JavaRuntime? in
            let resolved = candidate.resolvingSymlinksInPath()
            guard seen.insert(resolved.path).inserted else { return nil }
            return inspect(executable: resolved)
        }.sorted {
            if $0.majorVersion != $1.majorVersion { return $0.majorVersion > $1.majorVersion }
            return $0.id < $1.id
        }
    }

    public static func inspect(executable: URL) -> JavaRuntime? {
        guard FileManager.default.isExecutableFile(atPath: executable.path),
              let result = try? NativeProcess.capture(executable, ["-XshowSettings:properties", "-version"], timeout: 5), result.status == 0 else { return nil }
        var properties: [String: String] = [:]
        for line in result.text.split(separator: "\n") {
            let parts = line.split(separator: "=", maxSplits: 1).map { $0.trimmingCharacters(in: .whitespaces) }
            if parts.count == 2 { properties[parts[0]] = parts[1] }
        }
        let version: String
        if let property = properties["java.version"] { version = property }
        else if let regex = try? NSRegularExpression(pattern: #"version "([^"]+)""#), let match = regex.firstMatch(in: result.text, range: NSRange(result.text.startIndex..., in: result.text)), let range = Range(match.range(at: 1), in: result.text) { version = String(result.text[range]) }
        else { return nil }
        let pieces = version.split(whereSeparator: { $0 == "." || $0 == "-" || $0 == "_" })
        let major = pieces.first == "1" && pieces.count > 1 ? Int(pieces[1]) : pieces.first.flatMap { Int($0) }
        guard let major, major >= 8 else { return nil }
        let rawArchitecture = properties["os.arch"] ?? "unknown"
        let architecture = ["aarch64", "arm64"].contains(rawArchitecture) ? "arm64" : (["amd64", "x86_64"].contains(rawArchitecture) ? "x86_64" : rawArchitecture)
        let vendor = properties["java.vendor"] ?? "Java"
        return JavaRuntime(executable: executable, majorVersion: major, architecture: architecture, displayName: "Java \(version) · \(architecture) · \(vendor)")
    }
}

/// An immutable selection snapshot shared by launch preparation and its UI preview.
public struct VersionJavaSelection: Hashable, Sendable {
    private let mode: String
    private let path: String
    private let versionRange: String
    private let directory: URL
    private let requiredMajorVersion: Int
    private let requiredArchitecture: String?
    private let runtimes: [JavaRuntime]

    public init(version: InstalledVersion, profile: VersionProfileSettings, globalJavaPath: String, runtimes: [JavaRuntime]) {
        mode = profile.javaConfigurationMode
        path = mode == "global" ? globalJavaPath : profile.javaPath
        versionRange = profile.javaVersionRange
        directory = version.directory
        requiredMajorVersion = version.requiredJava
        requiredArchitecture = version.requiredJavaArchitecture
        self.runtimes = runtimes
    }

    public func resolve(fallback: JavaRuntime? = nil) throws -> JavaRuntime {
        let java: JavaRuntime?
        switch mode {
        case "global", "custom":
            if !path.isEmpty {
                java = runtimes.first { $0.executable.path == path }
                    ?? JavaDiscovery.inspect(executable: URL(fileURLWithPath: path))
            } else { java = best(runtimes) ?? fallback }
        case "folder":
            let roots = (try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil, options: [.skipsHiddenFiles])) ?? []
            java = ([directory] + roots).lazy.flatMap { root in
                [root.appendingPathComponent("bin/java"), root.appendingPathComponent("Contents/Home/bin/java"), root.appendingPathComponent("jre/bin/java")]
            }.compactMap { JavaDiscovery.inspect(executable: $0) }.first { $0.majorVersion >= requiredMajorVersion && (requiredArchitecture == nil || $0.architecture == requiredArchitecture) }
        case "range":
            let range = try VersionJavaRange(versionRange)
            java = best(runtimes.filter { range.contains($0) })
        default: java = best(runtimes) ?? fallback
        }
        guard let java else {
            if requiredArchitecture == "x86_64" { throw LauncherError.invalidJava("此旧版游戏使用 Intel 原生库，请安装或导入 Intel（x86_64）版 Java \(requiredMajorVersion)。") }
            throw LauncherError.invalidJava("未找到符合此版本设置的 Java，请选择已安装的 Java。")
        }
        if let requiredArchitecture, java.architecture != requiredArchitecture {
            throw LauncherError.invalidJava("此旧版游戏使用 Intel 原生库，请选择 x86_64 版 Java \(requiredMajorVersion)，当前所选为 \(java.architecture)。")
        }
        return java
    }

    private func best(_ candidates: [JavaRuntime]) -> JavaRuntime? {
        let compatible = candidates.filter { requiredArchitecture == nil || $0.architecture == requiredArchitecture }
        return compatible.first { $0.majorVersion == requiredMajorVersion }
            ?? compatible.filter { $0.majorVersion >= requiredMajorVersion }.min { $0.majorVersion < $1.majorVersion }
    }
}
