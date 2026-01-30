using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PCL.Core.Utils.Encryption;

public class ChaCha20SoftwareProvider : IEncryptionProvider
{
    public static ChaCha20SoftwareProvider Instance { get; } = new();

    // 常量：expand 32-byte k
    private static ReadOnlySpan<uint> Sigma => new[] { 0x61707865u, 0x3320646eu, 0x79622d32u, 0x6b206574u };

    public static bool IsSupported => true;

    public byte[] Encrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
    {
        // 预留 12 字节 Nonce 空间
        var result = new byte[data.Length + 12];
        var nonce = result.AsSpan(0, 12);

        // 生成随机 Nonce
        RandomNumberGenerator.Fill(nonce);

        _Process(data, result.AsSpan(12), key, nonce);
        return result;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
    {
        if (data.Length < 12) throw new ArgumentException("数据长度不足以包含 Nonce");

        var nonce = data[..12];
        var ciphertext = data[12..];
        var plaintext = new byte[ciphertext.Length];

        _Process(ciphertext, plaintext, key, nonce);
        return plaintext;
    }

    private static void _Process(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce)
    {
        if (key.Length != 32) throw new ArgumentException("Key 必须为 32 字节");
        if (nonce.Length != 12) throw new ArgumentException("Nonce 必须为 12 字节");

        // 在栈上分配状态矩阵和工作块，避免 GC
        Span<uint> state = stackalloc uint[16];

        // 1. 初始化状态
        Sigma.CopyTo(state[..4]);
        for (var i = 0; i < 8; i++)
        {
            state[4 + i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4, 4));
        }
        state[12] = 0; // 计数器初始值
        state[13] = BinaryPrimitives.ReadUInt32LittleEndian(nonce[..4]);
        state[14] = BinaryPrimitives.ReadUInt32LittleEndian(nonce[4..8]);
        state[15] = BinaryPrimitives.ReadUInt32LittleEndian(nonce[8..12]);

        Span<uint> workingBlock = stackalloc uint[16];
        var offset = 0;
        var length = input.Length;

        while (offset < length)
        {
            _GenerateBlock(workingBlock, state);
            state[12]++; // 递增计数器

            var remaining = Math.Min(64, length - offset);
            var inputPart = input.Slice(offset, remaining);
            var outputPart = output.Slice(offset, remaining);

            // 将 uint 块视为字节流进行异或
            var blockBytes = MemoryMarshal.AsBytes(workingBlock);
            for (var i = 0; i < remaining; i++)
            {
                outputPart[i] = (byte)(inputPart[i] ^ blockBytes[i]);
            }

            offset += 64;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void _GenerateBlock(Span<uint> x, ReadOnlySpan<uint> input)
    {
        input.CopyTo(x);

        for (var i = 0; i < 10; i++) // 20 轮计算
        {
            // 列变换
            _QuarterRound(x, 0, 4, 8, 12);
            _QuarterRound(x, 1, 5, 9, 13);
            _QuarterRound(x, 2, 6, 10, 14);
            _QuarterRound(x, 3, 7, 11, 15);
            // 对角线变换
            _QuarterRound(x, 0, 5, 10, 15);
            _QuarterRound(x, 1, 6, 11, 12);
            _QuarterRound(x, 2, 7, 8, 13);
            _QuarterRound(x, 3, 4, 9, 14);
        }

        for (var i = 0; i < 16; i++)
        {
            x[i] += input[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void _QuarterRound(Span<uint> x, int a, int b, int c, int d)
    {
        x[a] += x[b]; x[d] ^= x[a]; x[d] = BitOperations.RotateLeft(x[d], 16);
        x[c] += x[d]; x[b] ^= x[c]; x[b] = BitOperations.RotateLeft(x[b], 12);
        x[a] += x[b]; x[d] ^= x[a]; x[d] = BitOperations.RotateLeft(x[d], 8);
        x[c] += x[d]; x[b] ^= x[c]; x[b] = BitOperations.RotateLeft(x[b], 7);
    }
}