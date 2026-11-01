import Foundation

/// Mojang's rules use Java's platform names rather than Swift's architecture names.
public enum InstallPlatform {
    public static var architecture: String {
        #if arch(arm64)
        return "arm64"
        #else
        return "x86_64"
        #endif
    }

    public static func rulesAllowed(_ rules: [[String: Any]], features: [String: Bool] = [:]) -> Bool {
        guard !rules.isEmpty else { return true }
        var allowed = false
        for rule in rules {
            if let os = rule["os"] as? [String: Any] {
                if let name = os["name"] as? String, name != "osx" { continue }
                if let arch = os["arch"] as? String {
                    let aliases = architecture == "arm64" ? ["arm64", "aarch64"] : ["x86_64", "amd64"]
                    if !aliases.contains(arch) { continue }
                }
                if let pattern = os["version"] as? String {
                    let current = ProcessInfo.processInfo.operatingSystemVersion
                    let version = "\(current.majorVersion).\(current.minorVersion).\(current.patchVersion)"
                    guard let regex = try? NSRegularExpression(pattern: pattern),
                          regex.firstMatch(in: version, range: NSRange(version.startIndex..., in: version)) != nil else { continue }
                }
            }
            if let required = rule["features"] as? [String: Bool],
               required.contains(where: { features[$0.key, default: false] != $0.value }) { continue }
            allowed = rule["action"] as? String == "allow"
        }
        return allowed
    }

    public static func libraryAllowed(_ library: [String: Any]) -> Bool {
        if let rules = library["rules"] as? [[String: Any]], !rulesAllowed(rules) { return false }
        // Recent manifests list Intel and Apple Silicon native artifacts under identical OS rules.
        // Only put the selected architecture's dylibs in the classpath/extraction directory.
        if let name = library["name"] as? String {
            let components = name.split(separator: ":")
            if components.count >= 4 {
                let classifier = String(components[3])
                if classifier.hasPrefix("natives-macos") || classifier.hasPrefix("natives-osx") {
                    return architecture == "arm64" ? classifier.contains("arm64") : !classifier.contains("arm64")
                }
            }
        }
        return true
    }

    public static func nativeClassifier(for library: [String: Any]) -> String? {
        guard let natives = library["natives"] as? [String: String], var name = natives["osx"] else { return nil }
        name = name.replacingOccurrences(of: "${arch}", with: "64")
        if architecture == "arm64", let downloads = library["downloads"] as? [String: Any],
           let classifiers = downloads["classifiers"] as? [String: Any] {
            for candidate in [name + "-arm64", name.replacingOccurrences(of: "osx", with: "macos") + "-arm64"] {
                if classifiers[candidate] != nil { return candidate }
            }
        }
        // Legacy versions can still be installed on Apple Silicon; the launcher verifies the
        // extracted binaries against Java and explains Rosetta/Intel requirements at launch.
        return name
    }
}
