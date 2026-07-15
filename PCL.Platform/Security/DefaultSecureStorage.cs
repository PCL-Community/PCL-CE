// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PCL.Platform.Abstractions.Security;

namespace PCL.Platform.Security;

public sealed class DefaultSecureStorage : ISecureStorage
{
    private readonly ISecureStorage _backend;

    public DefaultSecureStorage(string applicationDataDirectory, string serviceName = "PCL-N")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        _backend = OperatingSystem.IsWindows()
            ? new WindowsDpapiSecureStorage(Path.Combine(applicationDataDirectory, serviceName, "secure-storage"))
            : OperatingSystem.IsMacOS()
                ? new MacKeychainSecureStorage(serviceName)
                : new LinuxSecretServiceSecureStorage(serviceName);
    }

    public ValueTask<SecureStorageReadResult> ReadAsync(string key, CancellationToken cancellationToken = default) =>
        _backend.ReadAsync(ValidateKey(key), cancellationToken);

    public ValueTask<SecureStorageOperationResult> WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) =>
        _backend.WriteAsync(ValidateKey(key), value, cancellationToken);

    public ValueTask<SecureStorageOperationResult> DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        _backend.DeleteAsync(ValidateKey(key), cancellationToken);

    private static string ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Length > 512 || key.Any(static c => char.IsControl(c)))
            throw new ArgumentException("Secure storage key is invalid.", nameof(key));
        return key;
    }
}

internal sealed class WindowsDpapiSecureStorage(string root) : ISecureStorage
{
    public ValueTask<SecureStorageReadResult> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetPath(key);
        if (!File.Exists(path)) return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.NotFound));
        try
        {
            byte[] encrypted = File.ReadAllBytes(path);
            byte[] plain = LegacyWindowsDataProtection.Unprotect(encrypted, Entropy(key));
            return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.Success, plain));
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or Win32Exception)
        {
            return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.Failed, Message: exception.Message));
        }
    }

    public ValueTask<SecureStorageOperationResult> WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            Directory.CreateDirectory(root);
            byte[] encrypted = LegacyWindowsDataProtection.Protect(value.ToArray(), Entropy(key));
            WriteAtomic(GetPath(key), encrypted);
            return ValueTask.FromResult(new SecureStorageOperationResult(SecureStorageStatus.Success));
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or Win32Exception)
        {
            return ValueTask.FromResult(new SecureStorageOperationResult(SecureStorageStatus.Failed, exception.Message));
        }
    }

    public ValueTask<SecureStorageOperationResult> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { File.Delete(GetPath(key)); return ValueTask.FromResult(new SecureStorageOperationResult(SecureStorageStatus.Success)); }
        catch (IOException exception) { return ValueTask.FromResult(new SecureStorageOperationResult(SecureStorageStatus.Failed, exception.Message)); }
    }

    private string GetPath(string key) => Path.Combine(root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".bin");
    private static byte[] Entropy(string key) => SHA256.HashData(Encoding.UTF8.GetBytes("PCL.N.SecureStorage.v1\0" + key));
    private static void WriteAtomic(string path, byte[] value) { string temp = path + ".tmp"; File.WriteAllBytes(temp, value); File.Move(temp, path, true); }
}

public static class LegacyWindowsDataProtection
{
    private const uint UiForbidden = 1;
    public static byte[] Protect(byte[] value, byte[] entropy) => Transform(value, entropy, true);
    public static byte[] Unprotect(byte[] value, byte[]? entropy) => Transform(value, entropy, false);

    private static byte[] Transform(byte[] value, byte[]? entropy, bool protect)
    {
        Blob input = Blob.From(value); Blob optional = entropy is { Length: > 0 } ? Blob.From(entropy) : default; Blob output = default; IntPtr description = IntPtr.Zero;
        try
        {
            bool success = entropy is { Length: > 0 }
                ? protect
                    ? CryptProtectDataWithEntropy(ref input, null, ref optional, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output)
                    : CryptUnprotectDataWithEntropy(ref input, out description, ref optional, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output)
                : protect
                    ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output)
                    : CryptUnprotectData(ref input, out description, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output);
            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());
            byte[] result = new byte[output.Size]; Marshal.Copy(output.Data, result, 0, output.Size); return result;
        }
        finally { input.Free(); optional.Free(); output.LocalFree(); if (description != IntPtr.Zero) _ = LocalFree(description); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct Blob
    {
        public int Size; public IntPtr Data;
        public static Blob From(byte[] value) { Blob blob = new() { Size = value.Length, Data = Marshal.AllocHGlobal(value.Length) }; Marshal.Copy(value, 0, blob.Data, value.Length); return blob; }
        public void Free() { if (Data != IntPtr.Zero) { Marshal.FreeHGlobal(Data); Data = IntPtr.Zero; } }
        public void LocalFree() { if (Data != IntPtr.Zero) { LegacyWindowsDataProtection.LocalFree(Data); Data = IntPtr.Zero; } }
    }
    [DllImport("Crypt32.dll", EntryPoint = "CryptProtectData", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectDataWithEntropy(ref Blob input, string? description, ref Blob entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("Crypt32.dll", EntryPoint = "CryptUnprotectData", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectDataWithEntropy(ref Blob input, out IntPtr description, ref Blob entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("Crypt32.dll", EntryPoint = "CryptProtectData", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("Crypt32.dll", EntryPoint = "CryptUnprotectData", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input, out IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("Kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}

internal sealed class MacKeychainSecureStorage(string service) : ISecureStorage
{
    private const int Success = 0, NotFound = -25300;
    public ValueTask<SecureStorageReadResult> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); IntPtr data = IntPtr.Zero;
        int status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)Encoding.UTF8.GetByteCount(service), service, (uint)Encoding.UTF8.GetByteCount(key), key, out uint length, out data, IntPtr.Zero);
        if (status == NotFound) return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.NotFound));
        if (status != Success) return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.Failed, Message: $"Keychain status {status}."));
        try { byte[] value = new byte[length]; Marshal.Copy(data, value, 0, checked((int)length)); return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.Success, value)); }
        finally { _ = SecKeychainItemFreeContent(IntPtr.Zero, data); }
    }
    public async ValueTask<SecureStorageOperationResult> WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        await DeleteAsync(key, cancellationToken).ConfigureAwait(false); byte[] bytes = value.ToArray();
        try { int status = SecKeychainAddGenericPassword(IntPtr.Zero, (uint)Encoding.UTF8.GetByteCount(service), service, (uint)Encoding.UTF8.GetByteCount(key), key, (uint)bytes.Length, bytes, IntPtr.Zero); return new(status == Success ? SecureStorageStatus.Success : SecureStorageStatus.Failed, status == Success ? null : $"Keychain status {status}."); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public ValueTask<SecureStorageOperationResult> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); int status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)Encoding.UTF8.GetByteCount(service), service, (uint)Encoding.UTF8.GetByteCount(key), key, out _, out IntPtr data, out IntPtr item);
        if (data != IntPtr.Zero) _ = SecKeychainItemFreeContent(IntPtr.Zero, data);
        if (status == NotFound) return ValueTask.FromResult(new SecureStorageOperationResult(SecureStorageStatus.NotFound));
        if (status != Success) return ValueTask.FromResult(new SecureStorageOperationResult(SecureStorageStatus.Failed, $"Keychain status {status}."));
        status = SecKeychainItemDelete(item); return ValueTask.FromResult(new SecureStorageOperationResult(status == Success ? SecureStorageStatus.Success : SecureStorageStatus.Failed, status == Success ? null : $"Keychain status {status}."));
    }
    [DllImport("/System/Library/Frameworks/Security.framework/Security", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)] private static extern int SecKeychainFindGenericPassword(IntPtr keychain, uint serviceLength, string serviceName, uint accountLength, string accountName, out uint passwordLength, out IntPtr passwordData, IntPtr itemRef);
    [DllImport("/System/Library/Frameworks/Security.framework/Security", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)] private static extern int SecKeychainFindGenericPassword(IntPtr keychain, uint serviceLength, string serviceName, uint accountLength, string accountName, out uint passwordLength, out IntPtr passwordData, out IntPtr itemRef);
    [DllImport("/System/Library/Frameworks/Security.framework/Security", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)] private static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceLength, string serviceName, uint accountLength, string accountName, uint passwordLength, byte[] passwordData, IntPtr itemRef);
    [DllImport("/System/Library/Frameworks/Security.framework/Security")] private static extern int SecKeychainItemDelete(IntPtr itemRef);
    [DllImport("/System/Library/Frameworks/Security.framework/Security")] private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);
}

internal sealed class LinuxSecretServiceSecureStorage(string service) : ISecureStorage
{
    private const string AttributeName = "pcl-key";
    private const string UnavailableMessage = "Secret Service is unavailable in this session; credentials will not be persisted.";
    private readonly string _service = service;
    private static SecretSchema Schema = SecretSchema.Create();

    public ValueTask<SecureStorageReadResult> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            IntPtr password = SecretPasswordLookupSync(ref Schema, IntPtr.Zero, out IntPtr error, AttributeName, Namespaced(key), IntPtr.Zero);
            if (error != IntPtr.Zero) return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.Unavailable, Message: TakeError(error)));
            if (password == IntPtr.Zero) return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.NotFound));
            try
            {
                string encoded = Marshal.PtrToStringUTF8(password) ?? string.Empty;
                return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.Success, Convert.FromBase64String(encoded)));
            }
            finally { SecretPasswordFree(password); }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or FormatException)
        {
            return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.Unavailable, Message: UnavailableMessage));
        }
    }

    public ValueTask<SecureStorageOperationResult> WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            bool success = SecretPasswordStoreSync(ref Schema, "default", _service + ": " + key, Convert.ToBase64String(value.Span), IntPtr.Zero, out IntPtr error, AttributeName, Namespaced(key), IntPtr.Zero);
            return ValueTask.FromResult(ToOperation(success, error));
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return ValueTask.FromResult(new SecureStorageOperationResult(SecureStorageStatus.Unavailable, UnavailableMessage));
        }
    }

    public ValueTask<SecureStorageOperationResult> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            bool success = SecretPasswordClearSync(ref Schema, IntPtr.Zero, out IntPtr error, AttributeName, Namespaced(key), IntPtr.Zero);
            return ValueTask.FromResult(ToOperation(success, error));
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return ValueTask.FromResult(new SecureStorageOperationResult(SecureStorageStatus.Unavailable, UnavailableMessage));
        }
    }

    private string Namespaced(string key) => _service + "/" + key;
    private static SecureStorageOperationResult ToOperation(bool success, IntPtr error) => error != IntPtr.Zero
        ? new SecureStorageOperationResult(SecureStorageStatus.Unavailable, TakeError(error))
        : new SecureStorageOperationResult(success ? SecureStorageStatus.Success : SecureStorageStatus.Failed);
    private static string TakeError(IntPtr error) { GError value = Marshal.PtrToStructure<GError>(error); string message = Marshal.PtrToStringUTF8(value.Message) ?? UnavailableMessage; GErrorFree(error); return message; }

    [StructLayout(LayoutKind.Sequential)] private struct SecretSchema
    {
        public IntPtr Name; public int Flags;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public SecretSchemaAttribute[] Attributes;
        public static SecretSchema Create() { SecretSchemaAttribute[] attributes = new SecretSchemaAttribute[32]; attributes[0] = new SecretSchemaAttribute { Name = Marshal.StringToHGlobalAnsi(AttributeName) }; return new SecretSchema { Name = Marshal.StringToHGlobalAnsi("top.pcln.credentials"), Attributes = attributes }; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct SecretSchemaAttribute { public IntPtr Name; public int Type; }
    [StructLayout(LayoutKind.Sequential)] private struct GError { public uint Domain; public int Code; public IntPtr Message; }
    [DllImport("libsecret-1.so.0", EntryPoint = "secret_password_lookup_sync", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)] private static extern IntPtr SecretPasswordLookupSync(ref SecretSchema schema, IntPtr cancellable, out IntPtr error, string attribute, string value, IntPtr terminator);
    [DllImport("libsecret-1.so.0", EntryPoint = "secret_password_store_sync", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SecretPasswordStoreSync(ref SecretSchema schema, string collection, string label, string password, IntPtr cancellable, out IntPtr error, string attribute, string value, IntPtr terminator);
    [DllImport("libsecret-1.so.0", EntryPoint = "secret_password_clear_sync", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SecretPasswordClearSync(ref SecretSchema schema, IntPtr cancellable, out IntPtr error, string attribute, string value, IntPtr terminator);
    [DllImport("libsecret-1.so.0", EntryPoint = "secret_password_free", CallingConvention = CallingConvention.Cdecl)] private static extern void SecretPasswordFree(IntPtr password);
    [DllImport("libglib-2.0.so.0", EntryPoint = "g_error_free", CallingConvention = CallingConvention.Cdecl)] private static extern void GErrorFree(IntPtr error);
}
