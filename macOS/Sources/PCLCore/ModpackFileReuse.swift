import Foundation
import CryptoKit
import Darwin

/// Identity of the complete distributable file. Mod IDs, names and versions are
/// deliberately not accepted as proof that a manifest's exact file is present.
struct ModpackFileIdentity: Sendable {
    let size: Int64?
    let sha1: String?
    let sha512: String?
    let md5: String?
    let fingerprint: UInt32?

    init(size: Int64? = nil, sha1: String? = nil, sha512: String? = nil,
         md5: String? = nil, fingerprint: UInt32? = nil) {
        self.size = size; self.sha1 = sha1?.lowercased(); self.sha512 = sha512?.lowercased()
        self.md5 = md5?.lowercased(); self.fingerprint = fingerprint
    }

    fileprivate var hasFullFileHash: Bool { sha1 != nil || sha512 != nil || md5 != nil }

    fileprivate func validate() throws {
        if let size, size < 0 { throw ModpackImportError.invalidManifest("复用文件大小无效") }
        for (value, length) in [(sha1, 40), (sha512, 128), (md5, 32)] {
            if let value, value.utf8.count != length || !value.utf8.allSatisfy({ (48...57).contains($0) || (97...102).contains($0) }) {
                throw ModpackImportError.invalidManifest("复用文件的哈希无效")
            }
        }
    }
}

/// Searches only the immediate regular files in caller-controlled directories.
/// Archive overrides must be extracted safely before adding their directories.
/// A nested dependency inside a JAR is never substituted for an entire download.
actor ModpackFileReuse {
    private let roots: [URL]
    private let maximumFileBytes: Int64
    private var indexed = false
    private var filesBySize: [Int64: [URL]] = [:]
    private var allFiles: [URL] = []
    private(set) var reusedFiles = 0

    init(roots: [URL], maximumFileBytes: Int64 = 512 * 1024 * 1024) {
        self.roots = roots; self.maximumFileBytes = max(0, maximumFileBytes)
    }

    /// Rebuild after publishing another verified file into a configured cache.
    func refresh() throws {
        try Task.checkCancellation()
        var bySize: [Int64: [URL]] = [:], files: [URL] = [], seen = Set<String>()
        for root in roots {
            try Task.checkCancellation()
            guard (try? FileManager.default.destinationOfSymbolicLink(atPath: root.path)) == nil,
                  let values = try? root.resourceValues(forKeys: [.isDirectoryKey]), values.isDirectory == true,
                  let resolved = realpath(root.path, nil) else { continue }
            let canonical = URL(fileURLWithPath: String(cString: resolved), isDirectory: true)
            free(resolved)
            guard let entries = try? FileManager.default.contentsOfDirectory(at: canonical,
                includingPropertiesForKeys: [.isRegularFileKey, .isSymbolicLinkKey, .fileSizeKey], options: [.skipsHiddenFiles]) else { continue }
            for entry in entries.sorted(by: { $0.lastPathComponent < $1.lastPathComponent }) {
                try Task.checkCancellation()
                guard seen.insert(entry.path).inserted,
                      let attributes = try? entry.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey, .fileSizeKey]),
                      attributes.isSymbolicLink != true, attributes.isRegularFile == true,
                      let size = attributes.fileSize, Int64(size) <= maximumFileBytes else { continue }
                bySize[Int64(size), default: []].append(entry)
                files.append(entry)
            }
        }
        filesBySize = bySize; allFiles = files; indexed = true
    }

    /// Returns a verified source at the instant of checking. Use reuse(_:to:) to
    /// also verify the copied bytes before an atomic destination commit.
    func matchingFile(for identity: ModpackFileIdentity) throws -> URL? {
        try identity.validate()
        // CurseForge fingerprints ignore whitespace. A matching fingerprint can
        // help find candidates, but alone cannot prove equality of complete bytes.
        guard identity.hasFullFileHash else { return nil }
        if !indexed { try refresh() }
        let candidates = identity.size.map { filesBySize[$0] ?? [] } ?? allFiles
        for candidate in candidates {
            try Task.checkCancellation()
            do {
                if try Self.matches(candidate, identity: identity, maximumFileBytes: maximumFileBytes) { return candidate }
            } catch is CancellationError { throw CancellationError() }
            catch { continue } // Unreadable/stale cache files are a cache miss.
        }
        return nil
    }

    /// Destination must already be path-validated by the caller. Existing files
    /// are retained; a differently hashed destination is a miss, never overwritten.
    @discardableResult func reuse(_ identity: ModpackFileIdentity, to destination: URL) throws -> Bool {
        try identity.validate()
        try Task.checkCancellation()
        guard identity.hasFullFileHash else { return false }
        if try Self.matches(destination, identity: identity, maximumFileBytes: maximumFileBytes) {
            reusedFiles += 1
            return true
        }
        guard !FileManager.default.fileExists(atPath: destination.path),
              (try? FileManager.default.destinationOfSymbolicLink(atPath: destination.path)) == nil,
              let source = try matchingFile(for: identity) else { return false }
        let parent = destination.deletingLastPathComponent()
        guard (try? FileManager.default.destinationOfSymbolicLink(atPath: parent.path)) == nil else {
            throw ModpackImportError.unsafePath(parent.lastPathComponent)
        }
        try FileManager.default.createDirectory(at: parent, withIntermediateDirectories: true)
        let staging = parent.appendingPathComponent(".pcl-reuse-\(UUID().uuidString).partial")
        defer { try? FileManager.default.removeItem(at: staging) }
        let result = source.path.withCString { from in staging.path.withCString { to in clonefile(from, to, 0) } }
        if result != 0 { try FileManager.default.copyItem(at: source, to: staging) }
        guard try Self.matches(staging, identity: identity, maximumFileBytes: maximumFileBytes) else { return false }
        try Task.checkCancellation()
        do { try FileManager.default.moveItem(at: staging, to: destination) }
        catch {
            if FileManager.default.fileExists(atPath: destination.path) {
                let matched = try Self.matches(destination, identity: identity, maximumFileBytes: maximumFileBytes)
                if matched { reusedFiles += 1 }
                return matched
            }
            throw error
        }
        reusedFiles += 1
        return true
    }

    static func matches(_ file: URL, identity: ModpackFileIdentity,
                        maximumFileBytes: Int64 = 512 * 1024 * 1024) throws -> Bool {
        try identity.validate()
        guard identity.hasFullFileHash else { return false }
        let descriptor = open(file.path, O_RDONLY | O_NOFOLLOW)
        guard descriptor >= 0 else { return false }
        let handle = FileHandle(fileDescriptor: descriptor, closeOnDealloc: true)
        defer { try? handle.close() }
        var status = stat()
        guard fstat(descriptor, &status) == 0, status.st_mode & S_IFMT == S_IFREG,
              status.st_size >= 0, status.st_size <= maximumFileBytes,
              identity.size == nil || identity.size == status.st_size else { return false }
        var sha1 = Insecure.SHA1(), sha512 = SHA512(), md5 = Insecure.MD5()
        var filteredCount: UInt64 = 0
        if identity.fingerprint != nil {
            while let chunk = try handle.read(upToCount: 1024 * 1024), !chunk.isEmpty {
                try Task.checkCancellation()
                filteredCount += UInt64(chunk.filter { !isFingerprintWhitespace($0) }.count)
            }
            guard filteredCount <= UInt32.max else { return false }
            try handle.seek(toOffset: 0)
        }
        var fingerprint = UInt32(1) ^ UInt32(filteredCount), pending: UInt32 = 0, pendingCount = 0
        while let chunk = try handle.read(upToCount: 1024 * 1024), !chunk.isEmpty {
            try Task.checkCancellation()
            if identity.sha1 != nil { sha1.update(data: chunk) }
            if identity.sha512 != nil { sha512.update(data: chunk) }
            if identity.md5 != nil { md5.update(data: chunk) }
            if identity.fingerprint != nil {
                for byte in chunk where !isFingerprintWhitespace(byte) {
                    pending |= UInt32(byte) << (pendingCount * 8); pendingCount += 1
                    if pendingCount == 4 {
                        var k = pending &* 0x5bd1e995
                        k ^= k >> 24; k = k &* 0x5bd1e995
                        fingerprint = (fingerprint &* 0x5bd1e995) ^ k
                        pending = 0; pendingCount = 0
                    }
                }
            }
        }
        if let expected = identity.sha1, hex(sha1.finalize()) != expected { return false }
        if let expected = identity.sha512, hex(sha512.finalize()) != expected { return false }
        if let expected = identity.md5, hex(md5.finalize()) != expected { return false }
        if let expected = identity.fingerprint {
            if pendingCount > 0 { fingerprint = (fingerprint ^ pending) &* 0x5bd1e995 }
            fingerprint ^= fingerprint >> 13; fingerprint = fingerprint &* 0x5bd1e995; fingerprint ^= fingerprint >> 15
            if fingerprint != expected { return false }
        }
        return true
    }

    private static func isFingerprintWhitespace(_ byte: UInt8) -> Bool { byte == 9 || byte == 10 || byte == 13 || byte == 32 }
    private static func hex<D: Sequence>(_ digest: D) -> String where D.Element == UInt8 {
        digest.map { String(format: "%02x", $0) }.joined()
    }
}
