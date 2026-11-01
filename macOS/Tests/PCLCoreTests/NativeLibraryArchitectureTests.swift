import Foundation
import XCTest
@testable import PCLCore

final class NativeLibraryArchitectureTests: XCTestCase {
    private func inspect(_ bytes: Data) throws -> Set<String> {
        let file = FileManager.default.temporaryDirectory.appendingPathComponent("PCL-MachO-" + UUID().uuidString + ".jnilib")
        try bytes.write(to: file)
        defer { try? FileManager.default.removeItem(at: file) }
        return try NativeLibraryArchitecture.architectures(in: file)
    }
    private func w32(_ value: UInt32, little: Bool = false) -> Data {
        let value = little ? value.littleEndian : value.bigEndian
        return withUnsafeBytes(of: value) { Data($0) }
    }
    private func w64(_ value: UInt64, little: Bool = false) -> Data {
        let value = little ? value.littleEndian : value.bigEndian
        return withUnsafeBytes(of: value) { Data($0) }
    }
    private func thin(_ cpu: UInt32, wide: Bool, little: Bool) -> Data {
        var data = w32(wide ? 0xfeedfacf : 0xfeedface, little: little)
        for word: UInt32 in [cpu, 3, 6, 0, 0, 0] { data += w32(word, little: little) }
        if wide { data += w32(0, little: little) }
        return data
    }
    private func fat(_ slices: [(UInt32, Data)], wide: Bool = false, little: Bool = false) -> Data {
        let tableEnd = 8 + slices.count * (wide ? 32 : 20)
        var offset = (tableEnd + 7) & ~7
        var header = w32(wide ? 0xcafebabf : 0xcafebabe, little: little) + w32(UInt32(slices.count), little: little)
        var content = Data(repeating: 0, count: offset - tableEnd)
        for (cpu, slice) in slices {
            header += w32(cpu, little: little) + w32(3, little: little)
            if wide { header += w64(UInt64(offset), little: little) + w64(UInt64(slice.count), little: little) }
            else { header += w32(UInt32(offset), little: little) + w32(UInt32(slice.count), little: little) }
            header += w32(3, little: little)
            if wide { header += w32(0, little: little) }
            content += slice
            let padding = (8 - slice.count % 8) % 8
            content += Data(repeating: 0, count: padding)
            offset += slice.count + padding
        }
        return header + content
    }
    private func replace(_ data: inout Data, offset: Int, value: UInt32, little: Bool = false) {
        data.replaceSubrange(offset..<offset + 4, with: w32(value, little: little))
    }

    func testThinIntelAndARM64BothByteOrders() throws {
        XCTAssertEqual(try inspect(thin(0x0100_0007, wide: true, little: true)), ["x86_64"])
        XCTAssertEqual(try inspect(thin(0x0100_0007, wide: true, little: false)), ["x86_64"])
        XCTAssertEqual(try inspect(thin(0x0100_000c, wide: true, little: true)), ["arm64"])
        XCTAssertEqual(try inspect(thin(7, wide: false, little: true)), ["i386"])
        XCTAssertEqual(try inspect(thin(18, wide: false, little: false)), ["ppc"])
    }

    func testLegacyPowerPCSliceDoesNotHideValidIntelSlices() throws {
        let bytes = fat([(18, thin(18, wide: false, little: false)),
                         (7, thin(7, wide: false, little: true)),
                         (0x0100_0007, thin(0x0100_0007, wide: true, little: true))])
        XCTAssertEqual(try inspect(bytes), ["ppc", "i386", "x86_64"])
    }

    func testFat32AndFat64LittleAndBigEndian() throws {
        let slices: [(UInt32, Data)] = [(0x0100_0007, thin(0x0100_0007, wide: true, little: true)),
                                       (0x0100_000c, thin(0x0100_000c, wide: true, little: true))]
        for wide in [false, true] {
            for little in [false, true] { XCTAssertEqual(try inspect(fat(slices, wide: wide, little: little)), ["x86_64", "arm64"]) }
        }
    }

    func testFatCPUDeclarationMustMatchActualThinHeaderAndHeaderWidth() throws {
        XCTAssertThrowsError(try inspect(fat([(0x0100_0007, thin(0x0100_000c, wide: true, little: true))])))
        XCTAssertThrowsError(try inspect(thin(0x0100_0007, wide: false, little: true)))
        XCTAssertThrowsError(try inspect(thin(7, wide: true, little: true)))
    }

    func testTruncatedMalformedAndNonMachOBinariesAreRejected() throws {
        for data in [Data(), Data([0xca, 0xfe]), Data("not Mach-O".utf8), Data([0xca, 0xfe, 0xba, 0xbe, 0, 0, 0, 2])] {
            XCTAssertThrowsError(try inspect(data))
        }
        let valid = thin(0x0100_0007, wide: true, little: true)
        XCTAssertThrowsError(try inspect(valid.dropLast()))
        var invalidCount = fat([(0x0100_0007, valid)])
        replace(&invalidCount, offset: 4, value: UInt32.max)
        XCTAssertThrowsError(try inspect(invalidCount))
        var offsetIntoTable = fat([(0x0100_0007, valid)])
        replace(&offsetIntoTable, offset: 16, value: 8)
        XCTAssertThrowsError(try inspect(offsetIntoTable))
        var beyondEnd = fat([(0x0100_0007, valid)])
        replace(&beyondEnd, offset: 20, value: UInt32.max)
        XCTAssertThrowsError(try inspect(beyondEnd))
        var overflow = fat([(0x0100_0007, valid)], wide: true)
        overflow.replaceSubrange(24..<32, with: w64(UInt64.max))
        XCTAssertThrowsError(try inspect(overflow))
    }

    func testOverlappingSlicesAlignmentAndLoadCommandBoundsAreRejected() throws {
        let one = thin(0x0100_0007, wide: true, little: true)
        var overlap = fat([(0x0100_0007, one), (0x0100_0007, one)])
        let firstOffset = overlap.subdata(in: 16..<20)
        overlap.replaceSubrange(36..<40, with: firstOffset)
        XCTAssertThrowsError(try inspect(overlap))
        var alignment = fat([(0x0100_0007, one)])
        replace(&alignment, offset: 24, value: 64)
        XCTAssertThrowsError(try inspect(alignment))
        var commands = one
        replace(&commands, offset: 16, value: 1, little: true)
        replace(&commands, offset: 20, value: 8, little: true)
        XCTAssertThrowsError(try inspect(commands))
        commands += w32(1, little: true) + w32(0, little: true)
        XCTAssertThrowsError(try inspect(commands))
        replace(&commands, offset: 36, value: 8, little: true)
        XCTAssertEqual(try inspect(commands), ["x86_64"])
    }

    func testActualLegacyJInputLibraryWhenProvided() throws {
        guard let path = ProcessInfo.processInfo.environment["PCL_NATIVE_ARCH_LIVE_FILE"] else {
            throw XCTSkip("Provide the downloaded legacy JInput native for a read-only header check")
        }
        XCTAssertEqual(try NativeLibraryArchitecture.architectures(in: URL(fileURLWithPath: path)), ["ppc", "i386", "x86_64"])
    }
}
