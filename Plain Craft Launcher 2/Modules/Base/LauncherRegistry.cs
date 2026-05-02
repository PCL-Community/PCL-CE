using System.Text;
using Microsoft.Win32;

namespace PCL;

/// <summary>
/// Owns Windows registry read, write, delete, rename, and existence helpers.
/// </summary>
public static class LauncherRegistry
{
    public static void RenameKey(RegistryKey parentKey, string subKeyName, string newSubKeyName)
    {
        if (parentKey.GetSubKeyNames().Contains(newSubKeyName))
            parentKey.DeleteSubKeyTree(newSubKeyName, false);
        var sourceKey = parentKey.OpenSubKey(subKeyName);
        if (sourceKey == null)
            return;
        var newKey = parentKey.CreateSubKey(newSubKeyName);
        if (sourceKey.GetSubKeyNames().Length > 0)
            throw new NotSupportedException("不支持对包含子键的子键进行重命名：" + sourceKey.GetSubKeyNames()[0] + "。");
        foreach (var valueName in sourceKey.GetValueNames())
        {
            var objValue = sourceKey.GetValue(valueName);
            var valKind = sourceKey.GetValueKind(valueName);
            newKey.SetValue(valueName, objValue, valKind);
        }

        parentKey.DeleteSubKeyTree(subKeyName, false);
    }

    public static bool HasValue(string key)
    {
        return ReadValue(key, null) is not null;
    }

    public static void DeleteValue(string key, bool throwException = false)
    {
        try
        {
            var subKey = Registry.CurrentUser.OpenSubKey(@"Software\" + ModSecret.RegFolder, true);
            subKey?.DeleteValue(key);
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "删除注册表出错：" + key,
                throwException ? LauncherLogger.LogLevel.Hint : LauncherLogger.LogLevel.Developer);
            if (throwException)
                throw;
        }
    }

    public static string ReadValue(string key, string defaultValue = "", string path = "")
    {
        try
        {
            var softKey = Registry.CurrentUser.OpenSubKey(@"Software\" + (string.IsNullOrEmpty(path) ? ModSecret.RegFolder : path), true);
            if (softKey is null)
                return defaultValue;
            var rawValue = softKey.GetValue(key);
            if (rawValue is null)
                return defaultValue;
            var readValue = new StringBuilder();
            readValue.AppendLine(rawValue.ToString());
            var value = readValue.ToString().Replace("\r\n", "");
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "读取注册表出错：" + key, LauncherLogger.LogLevel.Hint);
            return defaultValue;
        }
    }

    public static void WriteValue(string key, string value, bool showException = false, string path = "", bool throwException = false)
    {
        try
        {
            var subKeyPath = @"Software\" + (string.IsNullOrEmpty(path) ? ModSecret.RegFolder : path);
            var softKey = Registry.CurrentUser.OpenSubKey(subKeyPath, true) ?? Registry.CurrentUser.CreateSubKey(subKeyPath);
            softKey.SetValue(key, value);
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "写入注册表出错：" + key,
                throwException ? LauncherLogger.LogLevel.Hint : LauncherLogger.LogLevel.Developer);
            if (throwException)
                throw;
        }
    }
}
