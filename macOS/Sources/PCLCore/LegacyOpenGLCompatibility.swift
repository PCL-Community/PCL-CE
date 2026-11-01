import CryptoKit
import Foundation

/// macOS 27 can clear LWJGL 2's current OpenGL context while draining Cocoa events.
/// This Java 8 agent restores the context before returning to Minecraft's renderer.
enum LegacyOpenGLCompatibility {
    static let filename = "pcl-legacy-opengl-context-1.jar"

    static func javaAgentArgument(metadata: [String: Any], javaMajorVersion: Int,
                                  gameDirectory: URL,
                                  macOSMajorVersion: Int = ProcessInfo.processInfo.operatingSystemVersion.majorVersion) throws -> String? {
        let names = (metadata["libraries"] as? [[String: Any]] ?? []).compactMap { $0["name"] as? String }
        guard macOSMajorVersion >= 27, javaMajorVersion == 8,
              names.contains(where: { $0.hasPrefix("org.lwjgl.lwjgl:lwjgl:2.") }),
              !names.contains(where: { $0.hasPrefix("org.lwjgl:lwjgl:3.") }) else { return nil }
        return "-javaagent:" + (try verifiedAgent(gameDirectory: gameDirectory)).path
    }

    private static func verifiedAgent(gameDirectory: URL) throws -> URL {
        // Resolve legitimate root aliases (for example /var -> /private/var) once,
        // then reject links in every cache component, even links inside the root.
        let root = try MinecraftVersionMetadata.safeFile(relativePath: ".pcl-opengl-path-probe", under: gameDirectory).deletingLastPathComponent()
        let relativePath = "PCL/tools/" + filename
        let destination = try safeCacheFile(relativePath: relativePath, root: root)
        if FileManager.default.fileExists(atPath: destination.path) {
            let data = try regularData(at: destination)
            if sha256(data) == agentSHA256 { return destination }
        }
        guard let embedded = Data(base64Encoded: agentBase64, options: .ignoreUnknownCharacters),
              sha256(embedded) == agentSHA256 else {
            throw LauncherError.invalidArguments("旧版 OpenGL 兼容组件校验失败")
        }
        try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
        _ = try safeCacheFile(relativePath: relativePath, root: root)
        try embedded.write(to: destination, options: .atomic)
        guard sha256(try regularData(at: destination)) == agentSHA256 else {
            throw LauncherError.invalidArguments("旧版 OpenGL 兼容组件写入校验失败")
        }
        return destination
    }

    private static func safeCacheFile(relativePath: String, root: URL) throws -> URL {
        var component = root
        for part in relativePath.split(separator: "/") {
            component.appendPathComponent(String(part))
            if (try? FileManager.default.destinationOfSymbolicLink(atPath: component.path)) != nil {
                throw LauncherError.invalidArguments("旧版 OpenGL 兼容组件目录不能使用符号链接")
            }
        }
        let file = try MinecraftVersionMetadata.safeFile(relativePath: relativePath, under: root)
        if FileManager.default.fileExists(atPath: file.path) { _ = try regularData(at: file) }
        return file
    }

    private static func regularData(at url: URL) throws -> Data {
        let values = try url.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey, .linkCountKey, .fileSizeKey])
        guard values.isRegularFile == true, values.isSymbolicLink != true, values.linkCount == 1,
              (values.fileSize ?? Int.max) <= 1_048_576 else {
            throw LauncherError.invalidArguments("旧版 OpenGL 兼容组件缓存不是安全的普通文件")
        }
        return try Data(contentsOf: url)
    }

    private static func sha256(_ data: Data) -> String {
        SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
    }

    // BEGIN GENERATED LEGACY OPENGL AGENT
    private static let agentSHA256 = "94062619ec3eef36f803f1b982768a16d0b8633aa495422224761d7f35eaa29d"
    private static let agentBase64 = """
        UEsDBBQAAAAAAAAAIQCEcjAZPwAAAD8AAAAUAAAATUVUQS1JTkYvTUFOSUZFU1QuTUZNYW5pZmVzdC1WZXJzaW9uOiAxLjANClBy
        ZW1haW4tQ2xhc3M6IFBDTExlZ2FjeUNvbnRleHRBZ2VudA0KDQpQSwMEFAAAAAAAAAAhADlqudHJBAAAyQQAACEAAABQQ0xMZWdh
        Y3lDb250ZXh0QWdlbnQkMSQxJDEuY2xhc3PK/rq+AAAANAA7CQAQAB8KABEAIAcAIQcAIgoABAAjCAAkCAAlCAAmCgARACcKABEA
        KAgAKQgAKggAKwoAEQAsCgARAC0HAC4HAC8BAAZ0aGlzJDEHADABAAxJbm5lckNsYXNzZXMHADEBABtMUENMTGVnYWN5Q29udGV4
        dEFnZW50JDEkMTsBAAY8aW5pdD4BAE0oTFBDTExlZ2FjeUNvbnRleHRBZ2VudCQxJDE7SUxqZGsvaW50ZXJuYWwvb3JnL29iamVj
        dHdlYi9hc20vTWV0aG9kVmlzaXRvcjspVgEABENvZGUBAAl2aXNpdEluc24BAAQoSSlWAQANU3RhY2tNYXBUYWJsZQEAD0VuY2xv
        c2luZ01ldGhvZAwAMgAzDAASABYMABcANAEAJmpkay9pbnRlcm5hbC9vcmcvb2JqZWN0d2ViL2FzbS9PcGNvZGVzAQAkamRrL2lu
        dGVybmFsL29yZy9vYmplY3R3ZWIvYXNtL0xhYmVsDAAXACsBABhvcmcvbHdqZ2wvb3BlbmdsL0Rpc3BsYXkBAAlpc0NyZWF0ZWQB
        AAMoKVoMADUANgwANwA4AQAJaXNDdXJyZW50AQALbWFrZUN1cnJlbnQBAAMoKVYMADkAOgwAGgAbAQAbUENMTGVnYWN5Q29udGV4
        dEFnZW50JDEkMSQxAQAsamRrL2ludGVybmFsL29yZy9vYmplY3R3ZWIvYXNtL01ldGhvZFZpc2l0b3IBABdQQ0xMZWdhY3lDb250
        ZXh0QWdlbnQkMQEAGVBDTExlZ2FjeUNvbnRleHRBZ2VudCQxJDEBAAt2aXNpdE1ldGhvZAEAeihJTGphdmEvbGFuZy9TdHJpbmc7
        TGphdmEvbGFuZy9TdHJpbmc7TGphdmEvbGFuZy9TdHJpbmc7W0xqYXZhL2xhbmcvU3RyaW5nOylMamRrL2ludGVybmFsL29yZy9v
        YmplY3R3ZWIvYXNtL01ldGhvZFZpc2l0b3I7AQAyKElMamRrL2ludGVybmFsL29yZy9vYmplY3R3ZWIvYXNtL01ldGhvZFZpc2l0
        b3I7KVYBAA92aXNpdE1ldGhvZEluc24BADsoSUxqYXZhL2xhbmcvU3RyaW5nO0xqYXZhL2xhbmcvU3RyaW5nO0xqYXZhL2xhbmcv
        U3RyaW5nO1opVgEADXZpc2l0SnVtcEluc24BACooSUxqZGsvaW50ZXJuYWwvb3JnL29iamVjdHdlYi9hc20vTGFiZWw7KVYBAAp2
        aXNpdExhYmVsAQApKExqZGsvaW50ZXJuYWwvb3JnL29iamVjdHdlYi9hc20vTGFiZWw7KVYAIAAQABEAAAABEBAAEgAWAAAAAgAA
        ABcAGAABABkAAAAYAAMABAAAAAwqK7UAASocLbcAArEAAAAAAAEAGgAbAAEAGQAAAGsABgADAAAAVBsRALGgAEq7AARZtwAFTSoR
        ALgSBhIHEggDtwAJKhEAmSy3AAoqEQC4EgYSCxIIA7cACSoRAJostwAKKhEAuBIGEgwSDQO3AAkqLLcADiobtwAPsQAAAAEAHAAA
        AAUAAfsATgACAB0AAAAEABUAHgAUAAAAGgADABMAAAAAAAgAFQAAAAAAAAAQAAAAAAAAUEsDBBQAAAAAAAAAIQD80M2l+QQAAPkE
        AAAfAAAAUENMTGVnYWN5Q29udGV4dEFnZW50JDEkMS5jbGFzc8r+ur4AAAA0ADcJAA4AHwkADgAgCgAPACEKAA8AIggAIwoAJAAl
        CAAmCAAnCAAoBwApBwAqAwAFAAAKAAsAKwcALAcALQcALgEADElubmVyQ2xhc3NlcwEAC3ZhbCRwYXRjaGVkAQACW0kBAAZ0aGlz
        JDABABlMUENMTGVnYWN5Q29udGV4dEFnZW50JDE7AQAGPGluaXQ+AQBMKExQQ0xMZWdhY3lDb250ZXh0QWdlbnQkMTtJTGpkay9p
        bnRlcm5hbC9vcmcvb2JqZWN0d2ViL2FzbS9DbGFzc1Zpc2l0b3I7W0kpVgEABENvZGUBAAt2aXNpdE1ldGhvZAEAeihJTGphdmEv
        bGFuZy9TdHJpbmc7TGphdmEvbGFuZy9TdHJpbmc7TGphdmEvbGFuZy9TdHJpbmc7W0xqYXZhL2xhbmcvU3RyaW5nOylMamRrL2lu
        dGVybmFsL29yZy9vYmplY3R3ZWIvYXNtL01ldGhvZFZpc2l0b3I7AQANU3RhY2tNYXBUYWJsZQcALwEAD0VuY2xvc2luZ01ldGhv
        ZAwAMAAxDAAUABUMABIAEwwAFgAyDAAZABoBAA9wcm9jZXNzTWVzc2FnZXMHADMMADQANQEAAygpVgEABnVwZGF0ZQEABChaKVYB
        ACZqZGsvaW50ZXJuYWwvb3JnL29iamVjdHdlYi9hc20vT3Bjb2RlcwEAG1BDTExlZ2FjeUNvbnRleHRBZ2VudCQxJDEkMQwAFgA2
        AQAZUENMTGVnYWN5Q29udGV4dEFnZW50JDEkMQEAK2pkay9pbnRlcm5hbC9vcmcvb2JqZWN0d2ViL2FzbS9DbGFzc1Zpc2l0b3IB
        ABdQQ0xMZWdhY3lDb250ZXh0QWdlbnQkMQEALGpkay9pbnRlcm5hbC9vcmcvb2JqZWN0d2ViL2FzbS9NZXRob2RWaXNpdG9yAQAJ
        dHJhbnNmb3JtAQBgKExqYXZhL2xhbmcvQ2xhc3NMb2FkZXI7TGphdmEvbGFuZy9TdHJpbmc7TGphdmEvbGFuZy9DbGFzcztMamF2
        YS9zZWN1cml0eS9Qcm90ZWN0aW9uRG9tYWluO1tCKVtCAQAxKElMamRrL2ludGVybmFsL29yZy9vYmplY3R3ZWIvYXNtL0NsYXNz
        VmlzaXRvcjspVgEAEGphdmEvbGFuZy9TdHJpbmcBAAZlcXVhbHMBABUoTGphdmEvbGFuZy9PYmplY3Q7KVoBAE0oTFBDTExlZ2Fj
        eUNvbnRleHRBZ2VudCQxJDE7SUxqZGsvaW50ZXJuYWwvb3JnL29iamVjdHdlYi9hc20vTWV0aG9kVmlzaXRvcjspVgAgAA4ADwAA
        AAIQEAASABMAABAQABQAFQAAAAIAAAAWABcAAQAYAAAAHgADAAUAAAASKiu1AAEqGQS1AAIqHC23AAOxAAAAAAABABkAGgABABgA
        AACKAAYACAAAAGcqGywtGQQZBbcABDoGEgUstgAGmQAMEgcttgAGmgAeEggstgAGmQAZEgcttgAGmgAMEgkttgAGmQAHBKcABAM2
        BxUHmQAKGxAIfpoABhkGsCq0AAIDXC4EYE+7AAtZKhIMGQa3AA2wAAAAAQAbAAAAEQAG/AAfBwAcGgNAAfwADQECAAIAHQAAAAQA
        EAAeABEAAAAaAAMAEAAAAAAACAAOAAAAAAAAAAsAAAAAAABQSwMEFAAAAAAAAAAhAIkUhBM/CAAAPwgAAB0AAABQQ0xMZWdhY3lD
        b250ZXh0QWdlbnQkMS5jbGFzc8r+ur4AAAA0AHUKAB4ANggANwoAOAA5BwA6CgAEADsHADwKAAYAPQcAPgcAPwMABQAACgAIAEAK
        AAQAQQcAQggAQwoADQBECQBFAEYHAEcKABEANggASAoAEQBJCgARAEoKABEASwoATABNCgAGAE4HAE8KABkAUAgAUQoADQBSBwBT
        BwBUBwBVAQAMSW5uZXJDbGFzc2VzAQAGPGluaXQ+AQADKClWAQAEQ29kZQEACXRyYW5zZm9ybQEAYChMamF2YS9sYW5nL0NsYXNz
        TG9hZGVyO0xqYXZhL2xhbmcvU3RyaW5nO0xqYXZhL2xhbmcvQ2xhc3M7TGphdmEvc2VjdXJpdHkvUHJvdGVjdGlvbkRvbWFpbjtb
        QilbQgEADVN0YWNrTWFwVGFibGUHADoHAFYHADwHAFMHAFcHAFgHAFkHAFoHAFsHAE8BAAlTaWduYXR1cmUBAGMoTGphdmEvbGFu
        Zy9DbGFzc0xvYWRlcjtMamF2YS9sYW5nL1N0cmluZztMamF2YS9sYW5nL0NsYXNzPCo+O0xqYXZhL3NlY3VyaXR5L1Byb3RlY3Rp
        b25Eb21haW47W0IpW0IBAA9FbmNsb3NpbmdNZXRob2QHAFwMAF0AXgwAIQAiAQAYb3JnL2x3amdsL29wZW5nbC9EaXNwbGF5BwBY
        DABfAGABACpqZGsvaW50ZXJuYWwvb3JnL29iamVjdHdlYi9hc20vQ2xhc3NSZWFkZXIMACEAYQEAKmpkay9pbnRlcm5hbC9vcmcv
        b2JqZWN0d2ViL2FzbS9DbGFzc1dyaXRlcgwAIQBiAQAZUENMTGVnYWN5Q29udGV4dEFnZW50JDEkMQEAJmpkay9pbnRlcm5hbC9v
        cmcvb2JqZWN0d2ViL2FzbS9PcGNvZGVzDAAhAGMMAGQAZQEAH2phdmEvbGFuZy9JbGxlZ2FsU3RhdGVFeGNlcHRpb24BAB5VbmV4
        cGVjdGVkIExXSkdMIDIgRGlzcGxheSBBUEkMACEAZgcAZwwAaABpAQAXamF2YS9sYW5nL1N0cmluZ0J1aWxkZXIBAExbUENMIExl
        Z2FjeSBHTF0gbWFjT1MgT3BlbkdMIGNvbnRleHQgY29tcGF0aWJpbGl0eSBlbmFibGVkOyBwYXRjaGVkIG1ldGhvZHM9DABqAGsM
        AGoAbAwAbQBuBwBvDABwAGYMAHEAcgEAE2phdmEvbGFuZy9UaHJvd2FibGUMAHMAIgEAIkxXSkdMIDIgY29udGV4dCB0cmFuc2Zv
        cm1lciBmYWlsZWQMACEAdAEAF1BDTExlZ2FjeUNvbnRleHRBZ2VudCQxAQAQamF2YS9sYW5nL09iamVjdAEAKWphdmEvbGFuZy9p
        bnN0cnVtZW50L0NsYXNzRmlsZVRyYW5zZm9ybWVyAQACW0kBABVqYXZhL2xhbmcvQ2xhc3NMb2FkZXIBABBqYXZhL2xhbmcvU3Ry
        aW5nAQAPamF2YS9sYW5nL0NsYXNzAQAeamF2YS9zZWN1cml0eS9Qcm90ZWN0aW9uRG9tYWluAQACW0IBABVQQ0xMZWdhY3lDb250
        ZXh0QWdlbnQBAAdwcmVtYWluAQA7KExqYXZhL2xhbmcvU3RyaW5nO0xqYXZhL2xhbmcvaW5zdHJ1bWVudC9JbnN0cnVtZW50YXRp
        b247KVYBAAZlcXVhbHMBABUoTGphdmEvbGFuZy9PYmplY3Q7KVoBAAUoW0IpVgEAMChMamRrL2ludGVybmFsL29yZy9vYmplY3R3
        ZWIvYXNtL0NsYXNzUmVhZGVyO0kpVgEATChMUENMTGVnYWN5Q29udGV4dEFnZW50JDE7SUxqZGsvaW50ZXJuYWwvb3JnL29iamVj
        dHdlYi9hc20vQ2xhc3NWaXNpdG9yO1tJKVYBAAZhY2NlcHQBADEoTGpkay9pbnRlcm5hbC9vcmcvb2JqZWN0d2ViL2FzbS9DbGFz
        c1Zpc2l0b3I7SSlWAQAVKExqYXZhL2xhbmcvU3RyaW5nOylWAQAQamF2YS9sYW5nL1N5c3RlbQEAA2VycgEAFUxqYXZhL2lvL1By
        aW50U3RyZWFtOwEABmFwcGVuZAEALShMamF2YS9sYW5nL1N0cmluZzspTGphdmEvbGFuZy9TdHJpbmdCdWlsZGVyOwEAHChJKUxq
        YXZhL2xhbmcvU3RyaW5nQnVpbGRlcjsBAAh0b1N0cmluZwEAFCgpTGphdmEvbGFuZy9TdHJpbmc7AQATamF2YS9pby9QcmludFN0
        cmVhbQEAB3ByaW50bG4BAAt0b0J5dGVBcnJheQEABCgpW0IBAA9wcmludFN0YWNrVHJhY2UBACooTGphdmEvbGFuZy9TdHJpbmc7
        TGphdmEvbGFuZy9UaHJvd2FibGU7KVYAMAAdAB4AAQAfAAAAAgAAACEAIgABACMAAAARAAEAAQAAAAUqtwABsQAAAAAAAQAkACUA
        AgAjAAAAzAAHAAkAAACHEgIstgADmgAFAbC7AARZGQW3AAU6BgS8ClkDA086B7sABlkZBga3AAc6CBkGuwAIWSoSChkIGQe3AAsQ
        CLYADBkHAy4GnwANuwANWRIOtwAPv7IAELsAEVm3ABISE7YAFBkHAy62ABW2ABa2ABcZCLYAGLA6BhkGtgAauwANWRIbGQa3ABy/
        AAEACwBzAHQAGQABACYAAAArAAML/gBGBwAnBwAoBwAp/wAhAAYHACoHACsHACwHAC0HAC4HAC8AAQcAMAAxAAAAAgAyAAIAMwAA
        AAQANAA1ACAAAAASAAIAHQAAAAAACAAIAAAAAAAAUEsDBBQAAAAAAAAAIQCYYbydrwEAAK8BAAAbAAAAUENMTGVnYWN5Q29udGV4
        dEFnZW50LmNsYXNzyv66vgAAADQAFgoABgANBwAOCgACAA0LAA8AEAcAEQcAEgEADElubmVyQ2xhc3NlcwEABjxpbml0PgEAAygp
        VgEABENvZGUBAAdwcmVtYWluAQA7KExqYXZhL2xhbmcvU3RyaW5nO0xqYXZhL2xhbmcvaW5zdHJ1bWVudC9JbnN0cnVtZW50YXRp
        b247KVYMAAgACQEAF1BDTExlZ2FjeUNvbnRleHRBZ2VudCQxBwATDAAUABUBABVQQ0xMZWdhY3lDb250ZXh0QWdlbnQBABBqYXZh
        L2xhbmcvT2JqZWN0AQAkamF2YS9sYW5nL2luc3RydW1lbnQvSW5zdHJ1bWVudGF0aW9uAQAOYWRkVHJhbnNmb3JtZXIBAC4oTGph
        dmEvbGFuZy9pbnN0cnVtZW50L0NsYXNzRmlsZVRyYW5zZm9ybWVyOylWADEABQAGAAAAAAACAAEACAAJAAEACgAAABEAAQABAAAA
        BSq3AAGxAAAAAAAJAAsADAABAAoAAAAaAAMAAgAAAA4ruwACWbcAA7kABAIAsQAAAAAAAQAHAAAACgABAAIAAAAAAAhQSwECFAMU
        AAAAAAAAACEAhHIwGT8AAAA/AAAAFAAAAAAAAAAAAAAApIEAAAAATUVUQS1JTkYvTUFOSUZFU1QuTUZQSwECFAMUAAAAAAAAACEA
        OWq50ckEAADJBAAAIQAAAAAAAAAAAAAApIFxAAAAUENMTGVnYWN5Q29udGV4dEFnZW50JDEkMSQxLmNsYXNzUEsBAhQDFAAAAAAA
        AAAhAPzQzaX5BAAA+QQAAB8AAAAAAAAAAAAAAKSBeQUAAFBDTExlZ2FjeUNvbnRleHRBZ2VudCQxJDEuY2xhc3NQSwECFAMUAAAA
        AAAAACEAiRSEEz8IAAA/CAAAHQAAAAAAAAAAAAAApIGvCgAAUENMTGVnYWN5Q29udGV4dEFnZW50JDEuY2xhc3NQSwECFAMUAAAA
        AAAAACEAmGG8na8BAACvAQAAGwAAAAAAAAAAAAAApIEpEwAAUENMTGVnYWN5Q29udGV4dEFnZW50LmNsYXNzUEsFBgAAAAAFAAUA
        cgEAABEVAAAAAA==
        """
    // END GENERATED LEGACY OPENGL AGENT
}
