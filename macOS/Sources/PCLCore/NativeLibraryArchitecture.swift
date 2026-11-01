import Foundation

/// Reads Mach-O headers directly. Modern lipo rejects some valid legacy
/// PowerPC universal files even when an intact Intel slice is also present.
enum NativeLibraryArchitecture {
    static func architectures(in file: URL) throws -> Set<String> {
        let values = try file.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey])
        guard values.isRegularFile == true, values.isSymbolicLink != true else {
            throw LauncherError.invalidVersion("原生库文件无效：\(file.lastPathComponent)")
        }
        do { return try parse(Data(contentsOf: file, options: .mappedIfSafe)) }
        catch {
            throw LauncherError.invalidVersion("无法读取原生库 \(file.lastPathComponent) 的 Mach-O 架构：\(error.localizedDescription)")
        }
    }

    private enum ByteOrder { case big, little }
    private enum Invalid: LocalizedError {
        case header, bounds, cpu, commands
        var errorDescription: String? {
            switch self {
            case .header: return "文件头无效"
            case .bounds: return "架构切片超出文件范围或相互重叠"
            case .cpu: return "架构表与切片的 CPU 类型不一致"
            case .commands: return "Mach-O 加载命令范围无效"
            }
        }
    }
    private static func parse(_ data: Data) throws -> Set<String> {
        let magic = try word(data, at: 0, order: .big)
        let order: ByteOrder, wide: Bool
        switch magic {
        case 0xcafebabe: order = .big; wide = false
        case 0xbebafeca: order = .little; wide = false
        case 0xcafebabf: order = .big; wide = true
        case 0xbfbafeca: order = .little; wide = true
        default:
            let cpu = try thin(data, start: 0, count: data.count, expectedCPU: nil)
            return name(cpu).map { [$0] } ?? []
        }
        let count = try word(data, at: 4, order: order)
        let recordSize = wide ? 32 : 20
        guard count > 0, UInt64(count) <= UInt64(max(0, data.count - 8) / recordSize) else { throw Invalid.bounds }
        let tableEnd = 8 + Int(count) * recordSize
        var ranges: [(start: UInt64, end: UInt64)] = [], result = Set<String>()
        for index in 0..<Int(count) {
            let record = 8 + index * recordSize
            let cpu = try word(data, at: record, order: order)
            let offset = wide ? try doubleWord(data, at: record + 8, order: order) : UInt64(try word(data, at: record + 8, order: order))
            let size = wide ? try doubleWord(data, at: record + 16, order: order) : UInt64(try word(data, at: record + 12, order: order))
            let alignment = try word(data, at: record + (wide ? 24 : 16), order: order)
            guard offset >= UInt64(tableEnd), offset <= UInt64(data.count), size >= 28,
                  size <= UInt64(data.count) - offset, alignment < 64,
                  offset % (UInt64(1) << UInt64(alignment)) == 0 else { throw Invalid.bounds }
            if wide, try word(data, at: record + 28, order: order) != 0 { throw Invalid.header }
            let end = offset + size // The preceding subtraction proves this cannot overflow.
            guard !ranges.contains(where: { offset < $0.end && end > $0.start }) else { throw Invalid.bounds }
            ranges.append((offset, end))
            let actual = try thin(data, start: Int(offset), count: Int(size), expectedCPU: cpu)
            if let name = name(actual) { result.insert(name) }
        }
        return result
    }

    private static func thin(_ data: Data, start: Int, count: Int, expectedCPU: UInt32?) throws -> UInt32 {
        guard start >= 0, count >= 28, start <= data.count - count else { throw Invalid.bounds }
        let magic = try word(data, at: start, order: .big)
        let order: ByteOrder, wide: Bool
        switch magic {
        case 0xfeedface: order = .big; wide = false
        case 0xcefaedfe: order = .little; wide = false
        case 0xfeedfacf: order = .big; wide = true
        case 0xcffaedfe: order = .little; wide = true
        default: throw Invalid.header
        }
        let headerSize = wide ? 32 : 28
        guard count >= headerSize else { throw Invalid.bounds }
        let cpu = try word(data, at: start + 4, order: order)
        if let expectedCPU, expectedCPU != cpu { throw Invalid.cpu }
        let cpuIs64Bit = cpu & 0x0300_0000 != 0
        guard cpuIs64Bit == wide else { throw Invalid.cpu }
        let commands = try word(data, at: start + 16, order: order)
        let commandsSize = try word(data, at: start + 20, order: order)
        guard UInt64(commandsSize) <= UInt64(count - headerSize), UInt64(commands) <= UInt64(commandsSize) / 8 else { throw Invalid.commands }
        var cursor = start + headerSize
        let commandEnd = cursor + Int(commandsSize)
        for _ in 0..<commands {
            guard cursor <= commandEnd - 8 else { throw Invalid.commands }
            let size = try word(data, at: cursor + 4, order: order)
            guard size >= 8, size % 4 == 0, UInt64(size) <= UInt64(commandEnd - cursor) else { throw Invalid.commands }
            cursor += Int(size)
        }
        guard cursor == commandEnd else { throw Invalid.commands }
        return cpu
    }

    private static func name(_ cpu: UInt32) -> String? {
        switch cpu {
        case 7: return "i386"
        case 0x0100_0007: return "x86_64"
        case 12: return "arm"
        case 0x0100_000c: return "arm64"
        case 0x0200_000c: return "arm64_32"
        case 18: return "ppc"
        case 0x0100_0012: return "ppc64"
        default: return nil
        }
    }
    private static func word(_ data: Data, at offset: Int, order: ByteOrder) throws -> UInt32 {
        guard offset >= 0, offset <= data.count - 4 else { throw Invalid.bounds }
        let indices = order == .big ? [0, 1, 2, 3] : [3, 2, 1, 0]
        return indices.reduce(UInt32(0)) { ($0 << 8) | UInt32(data[offset + $1]) }
    }
    private static func doubleWord(_ data: Data, at offset: Int, order: ByteOrder) throws -> UInt64 {
        let first = UInt64(try word(data, at: offset, order: order)), second = UInt64(try word(data, at: offset + 4, order: order))
        return order == .big ? (first << 32) | second : (second << 32) | first
    }
}
