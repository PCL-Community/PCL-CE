import CryptoKit
import Darwin
import Foundation

enum ExtraModKind: String, Hashable, Sendable { case optiFine, optiFabric }
struct ExtraManagedMod: Hashable, Sendable {
    let kind: ExtraModKind
    let url: URL
    let local: LocalModFile?
    let identity: ExtraModIdentity
}
struct ExtraModSnapshot: Hashable, Sendable {
    let folder: URL
    let files: [ExtraManagedMod]
    let fingerprint: String
    static func capture(folder: URL) throws -> Self {
        let scanned = try LocalModManager.scan(directory: folder)
        let canonicalFolder = try MinecraftVersionMetadata.safeFile(relativePath: ".pcl-extras-probe", under: folder).deletingLastPathComponent()
        var files: [ExtraManagedMod] = []
        for mod in scanned where mod.enabled {
            let kind: ExtraModKind?
            if mod.modID == "optifabric" { kind = .optiFabric }
            else if (mod.modID.isEmpty || mod.modID == "optifine"), mod.url.pathExtension.lowercased() == "jar", try isOptiFine(mod.url) { kind = .optiFine }
            else { kind = nil }
            if let kind { files.append(.init(kind: kind, url: mod.url, local: mod, identity: try ExtraModIdentity.read(mod.url))) }
        }
        files.sort { $0.url.lastPathComponent < $1.url.lastPathComponent }
        let signature = canonicalFolder.path + files.map { "\n" + $0.kind.rawValue + ":" + $0.url.lastPathComponent + ":" + $0.identity.sha256 }.joined()
        return .init(folder: canonicalFolder, files: files, fingerprint: SHA256.hash(data: Data(signature.utf8)).map { String(format: "%02x", $0) }.joined())
    }
    func matches(file: URL, resource: CommunityResourceFile) -> Bool { files.first { $0.url == file }?.identity.matches(resource) == true }
    private static func isOptiFine(_ file: URL) throws -> Bool {
        // A .jar suffix alone is never enough to claim ownership of a user's Mod.
        let listing = try NativeProcess.capture(URL(fileURLWithPath: "/usr/bin/unzip"), ["-Z1", file.path], timeout: 5)
        guard listing.status == 0 else { return false }
        let names = Set(listing.text.split(separator: "\n").map(String.init))
        return names.contains("optifine/OptiFineTweaker.class") || names.contains("optifine/OptiFineForgeTweaker.class") || names.contains("optifine/OptiFineTransformationService.class")
    }
}

struct ExtraModIdentity: Hashable, Sendable {
    let size: Int64
    let sha1: String, sha256: String, sha512: String, md5: String
    static func read(_ file: URL) throws -> Self {
        let descriptor = open(file.path, O_RDONLY | O_NOFOLLOW)
        guard descriptor >= 0 else { throw LocalModError.unsafePath(file.path) }
        let handle = FileHandle(fileDescriptor: descriptor, closeOnDealloc: true)
        defer { try? handle.close() }
        var status = stat()
        guard fstat(descriptor, &status) == 0, status.st_mode & S_IFMT == S_IFREG, status.st_nlink == 1,
              status.st_size >= 0, status.st_size <= 2 * 1024 * 1024 * 1024 else { throw LocalModError.unsafePath(file.path) }
        var one = Insecure.SHA1(), two = SHA256(), five = SHA512(), md5 = Insecure.MD5()
        while let bytes = try handle.read(upToCount: 1_048_576), !bytes.isEmpty {
            one.update(data: bytes); two.update(data: bytes); five.update(data: bytes); md5.update(data: bytes)
        }
        func hex<T: Sequence>(_ value: T) -> String where T.Element == UInt8 { value.map { String(format: "%02x", $0) }.joined() }
        return .init(size: status.st_size, sha1: hex(one.finalize()), sha256: hex(two.finalize()), sha512: hex(five.finalize()), md5: hex(md5.finalize()))
    }
    func matches(_ file: CommunityResourceFile) -> Bool {
        size == file.size && (file.sha1 == nil || file.sha1?.lowercased() == sha1)
            && (file.sha512 == nil || file.sha512?.lowercased() == sha512) && (file.md5 == nil || file.md5?.lowercased() == md5)
            && (file.sha1 != nil || file.sha512 != nil || file.md5 != nil)
    }
}

final class ExtraModTransaction {
    private let folder: URL, backup: URL, root: URL
    private var originals: [(URL, URL, ExtraModIdentity)] = []
    private var published: [(URL, ExtraModIdentity)] = []
    private var completed = false
    private init(folder: URL, backup: URL, root: URL) { self.folder = folder; self.backup = backup; self.root = root }
    static func apply(expected: ExtraModSnapshot, desired: ExtraModSnapshot, gameDirectory: URL) throws -> ExtraModTransaction {
        try Task.checkCancellation()
        let current = try ExtraModSnapshot.capture(folder: expected.folder)
        guard current.fingerprint == expected.fingerprint else { throw MinecraftInstallError.invalidMetadata("OptiFine 或 OptiFabric 文件已改变，请刷新后重试") }
        let root = try MinecraftVersionMetadata.safeFile(relativePath: ".pcl-extra-root-probe", under: gameDirectory).deletingLastPathComponent()
        let backup = try MinecraftVersionMetadata.safeFile(relativePath: ".pcl-extras-\(UUID().uuidString)", under: root)
        let txn = ExtraModTransaction(folder: current.folder, backup: backup, root: root)
        try FileManager.default.createDirectory(at: backup, withIntermediateDirectories: false)
        if current.files.count == desired.files.count, zip(current.files, desired.files).allSatisfy({ $0.kind == $1.kind && $0.url.lastPathComponent == $1.url.lastPathComponent && $0.identity == $1.identity }) { txn.finish(); return txn }
        do {
            var names = Set<String>()
            var incoming: [(URL, URL, ExtraModIdentity)] = []
            let owned = Set(current.files.map { key($0.url.lastPathComponent) })
            for (index, file) in desired.files.enumerated() {
                let name = try ModpackZIPArchive.validatedPath(file.url.lastPathComponent)
                guard !name.contains("/"), names.insert(key(name)).inserted else { throw LocalModError.collision(name) }
                if FileManager.default.fileExists(atPath: current.folder.path) {
                    for existing in try FileManager.default.contentsOfDirectory(atPath: current.folder.path) where key(existing) == key(name) && !owned.contains(key(existing)) {
                        throw LocalModError.collision(existing)
                    }
                }
                let staged = backup.appendingPathComponent("incoming-\(index).jar")
                try ModpackImporter.cloneOrCopy(file.url, to: staged)
                guard try ExtraModIdentity.read(staged) == file.identity else { throw LocalModError.invalidFile(name) }
                incoming.append((staged, current.folder.appendingPathComponent(name), file.identity))
            }
            try Task.checkCancellation()
            try txn.checkFolder()
            guard try ExtraModSnapshot.capture(folder: current.folder).fingerprint == expected.fingerprint else { throw LocalModError.invalidFile("组件文件已改变") }
            if !incoming.isEmpty { try FileManager.default.createDirectory(at: current.folder, withIntermediateDirectories: true) }
            for (index, file) in current.files.enumerated() {
                let saved = backup.appendingPathComponent("original-\(index).jar")
                try FileManager.default.moveItem(at: file.url, to: saved)
                txn.originals.append((file.url, saved, file.identity))
            }
            for (staged, destination, identity) in incoming {
                try FileManager.default.moveItem(at: staged, to: destination)
                txn.published.append((destination, identity))
            }
            return txn
        } catch { let original = error; try txn.rollback(); throw original }
    }
    func rollback() throws {
        guard !completed else { return }
        if !published.isEmpty || !originals.isEmpty { try checkFolder() }
        for (file, identity) in published.reversed() {
            guard try ExtraModIdentity.read(file) == identity else { throw LocalModError.rollback(backup.path) }
            try FileManager.default.removeItem(at: file)
        }
        published.removeAll()
        for (file, saved, identity) in originals.reversed() {
            guard !FileManager.default.fileExists(atPath: file.path), try ExtraModIdentity.read(saved) == identity else { throw LocalModError.rollback(backup.path) }
            try FileManager.default.moveItem(at: saved, to: file)
        }
        originals.removeAll(); completed = true; try? FileManager.default.removeItem(at: backup)
    }
    func finish() { guard !completed else { return }; completed = true; try? FileManager.default.removeItem(at: backup) }
    private func checkFolder() throws {
        let safe = try MinecraftVersionMetadata.safeFile(relativePath: String(folder.path.dropFirst(root.path.count + 1)), under: root)
        guard folder.path.hasPrefix(root.path + "/"), safe == folder, folder.resolvingSymlinksInPath().standardizedFileURL == folder.standardizedFileURL else { throw LocalModError.unsafePath(folder.path) }
    }
    private static func key(_ value: String) -> String { value.precomposedStringWithCanonicalMapping.lowercased() }
}
