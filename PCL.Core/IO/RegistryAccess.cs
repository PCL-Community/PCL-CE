// This codes is disabled because of if access ModSecret.RegFolder, it will cause a circular reference.
// So we will move this class to ModSecret.cs in the future, but for now, we will just disable it.

//namespace PCL.Core.IO;

//public class RegistryAccess
//{

//    // =============================
//    // 注册表
//    // =============================

//    /// <summary>
//    /// 重命名一个注册表子键。不可用于包含子键的子键。
//    /// </summary>
//    /// <exception cref="NotSupportedException">在尝试对包含子键的子键进行重命名时抛出</exception>
//    public static void RenameReg(RegistryKey parentKey, string subKeyName, string newSubKeyName)
//    {
//        if (parentKey.GetSubKeyNames().Contains(newSubKeyName))
//        {
//            parentKey.DeleteSubKeyTree(newSubKeyName, false);
//        }

//        var sourceKey = parentKey.OpenSubKey(subKeyName);
//        if (sourceKey == null)
//        {
//            return; // 没有目标项
//        }

//        var newKey = parentKey.CreateSubKey(newSubKeyName);
//        if (sourceKey.GetSubKeyNames().Length > 0)
//        {
//            throw new NotSupportedException($"不支持对包含子键的子键进行重命名：{sourceKey.GetSubKeyNames()[0]}。");
//        }

//        foreach (var valueName in sourceKey.GetValueNames())
//        {
//            var objValue = sourceKey.GetValue(valueName);
//            var valKind = sourceKey.GetValueKind(valueName);
//            newKey.SetValue(valueName, objValue, valKind);
//        }

//        parentKey.DeleteSubKeyTree(subKeyName, false);
//    }

//    /// <summary>
//    /// 读取注册表，默认为程序所属。
//    /// </summary>
//    public static string ReadReg(string key, string defaultValue = "", string path = "")
//    {
//        string readRegRet;
//        try
//        {
//            var parentKey = Registry.CurrentUser;
//            var softKey = parentKey.OpenSubKey($"Software\\{(string.IsNullOrEmpty(path) ? ModSecret.RegFolder : path)}", true);
//            if (softKey is null)
//            {
//                readRegRet = defaultValue; // 不存在则返回默认值
//            }
//            else
//            {
//                var readValue = new StringBuilder();
//                readValue.AppendLine(softKey.GetValue(key).ToString());
//                var value = readValue.ToString().Replace("\r\n", ""); // 去除莫名的回车
//                return string.IsNullOrEmpty(value) ? defaultValue : value;
//            } // 错误则返回默认值
//        }
//        catch (Exception ex)
//        {
//            Log(ex, "读取注册表出错：" + key, LogType.Hint);
//            return defaultValue;
//        }

//        return readRegRet;
//    }

//    /// <summary>
//    /// 写入注册表，默认为程序所属。
//    /// </summary>
//    /// <exception cref="Exception">Throws if failed to write..</exception>
//    public static void WriteReg(string key,
//        string value,
//        bool showException = false,
//        string path = "",
//        bool throwException = false)
//    {
//        try
//        {
//            var parentKey = Registry.CurrentUser;
//            var softKey =
//                parentKey.OpenSubKey($"Software\\ {(string.IsNullOrEmpty(path) ? ModSecret.RegFolder : path)}", true) ??
//                parentKey.CreateSubKey($"Software\\{(string.IsNullOrEmpty(path)
//                    ? ModSecret.RegFolder
//                    : path)}"); // 如果不存在就创建

//            softKey.SetValue(key, value);
//        }
//        catch (Exception ex)
//        {
//            Log(ex, "写入注册表出错：" + key, throwException ? LogType.Hint : LogType.Developer);
//            if (throwException)
//                throw;
//        }
//    }

//    /// <summary>
//    /// 是否存在某个注册表键。
//    /// </summary>
//    public static bool HasReg(string key)
//    {
//        return !(ReadReg(key, "\0").Equals("\0", StringComparison.InvariantCulture));
//    }

//    /// <summary>
//    /// 删除注册表键。
//    /// </summary>
//    public static void DeleteReg(string key, bool throwException = false)
//    {
//        try
//        {
//            var subKey = Registry.CurrentUser.OpenSubKey(@"Software\" + ModSecret.RegFolder, true);
//            subKey?.DeleteValue(key);
//        }
//        catch (Exception ex)
//        {
//            Log(ex, "删除注册表出错：" + key, throwException ? LogType.Hint : LogType.Developer);
//            if (throwException)
//                throw;
//        }
//    }
//}