import Foundation

public enum OfflineSkinModel: String, Codable, Sendable {
    case automatic
    case steve
    case alex
}

public enum OfflineLoginError: LocalizedError, Equatable {
    case emptyName
    case doubleQuote
    case nameTooLong

    public var errorDescription: String? {
        switch self {
        case .emptyName: return "玩家名不能为空！"
        case .doubleQuote: return "玩家名不能包含英文引号！"
        case .nameTooLong: return "自 1.20.3 起，玩家名至多只能包含 16 个字符！"
        }
    }
}

/// Mirrors PageLoginLegacy.GetLoginData / IsVaild in the original PCL launcher.
public enum OfflineLogin {
    public static func normalizedName(_ input: String) -> String {
        input.replacingOccurrences(of: "¨", with: "").trimmingCharacters(in: .whitespacesAndNewlines)
    }

    @discardableResult
    public static func validateName(_ input: String, versionID: String? = nil) throws -> String {
        let name = normalizedName(input)
        guard !name.isEmpty else { throw OfflineLoginError.emptyName }
        guard !name.contains("\"") else { throw OfflineLoginError.doubleQuote }
        if appliesModernNameLimit(versionID), name.utf16.count > 16 { throw OfflineLoginError.nameTooLong }
        return name
    }

    /// PCL intentionally uses its own deterministic UUID, rather than Java's OfflinePlayer MD5.
    /// GetStableHashCode is the UInt64 / UTF-16 implementation from the pinned MeloongCore:
    /// https://github.com/Meloong-Git/MeloongCore/blob/4c60c2962c3b50bc3fe96ed5dd9259b980173580/Shared/Extensions/StringExtensions.cs#L392-L396
    public static func uuid(for name: String, skinModel: OfflineSkinModel = .automatic) -> String {
        var hash: UInt64 = 5381
        for unit in name.utf16 { hash = (hash &<< 5) ^ hash ^ UInt64(unit) }
        hash ^= 0xA98F501BC684032F
        let lengthHex = paddedHex(UInt64(name.utf16.count))
        let hashHex = paddedHex(hash)
        var bytes = Array((lengthHex + hashHex).utf8)
        bytes[12] = 51 // UUID version 3
        bytes[16] = 57 // PCL's fixed variant nibble 9
        var uuid = String(decoding: bytes, as: UTF8.self)
        guard skinModel != .automatic else { return uuid }
        let requiredSkin = skinModel == .steve ? "Steve" : "Alex"
        // Mirrors McLoginLegacyUuidWithCustomSkin cases 1/2: alter only the final five
        // hex digits until McSkinSex matches. The original wraps FFFFF to 00000,
        // then increments, so the next candidate is deliberately 00001.
        while skinName(forUUID: uuid) != requiredSkin {
            let prefix = String(uuid.prefix(27))
            var suffix = UInt32(uuid.suffix(5), radix: 16)!
            if suffix == 0xFFFFF { suffix = 0 }
            suffix += 1
            let hex = String(suffix, radix: 16, uppercase: true)
            uuid = prefix + String(repeating: "0", count: 5 - hex.count) + hex
        }
        return uuid
    }

    /// The original McSkinSex XORs four UUID nibbles; odd is Alex, even is Steve.
    public static func skinName(forUUID uuid: String) -> String {
        let bytes = Array(uuid.utf8)
        guard bytes.count == 32 else { return "Steve" }
        var parity = 0
        for index in [7, 15, 23, 31] {
            guard let digit = Int(String(UnicodeScalar(bytes[index])), radix: 16) else { return "Steve" }
            parity ^= digit
        }
        return parity % 2 == 0 ? "Steve" : "Alex"
    }

    private static func paddedHex(_ value: UInt64) -> String {
        let hex = String(value, radix: 16, uppercase: true)
        return String(repeating: "0", count: max(0, 16 - hex.count)) + hex
    }

    private static func appliesModernNameLimit(_ versionID: String?) -> Bool {
        guard let versionID,
              versionID.range(of: #"^[0-9]+\.[0-9]+(?:\.[0-9]+)?$"#, options: .regularExpression) != nil else { return false }
        let components = versionID.split(separator: ".").compactMap { Int($0) }
        guard components.count >= 2 else { return false }
        // Both classic 1.x.y releases and year-based releases (26.x onward) compare correctly.
        if components[0] != 1 { return components[0] > 1 }
        if components[1] != 20 { return components[1] > 20 }
        return components.count > 2 && components[2] >= 3
    }
}
