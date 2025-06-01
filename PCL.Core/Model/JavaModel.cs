using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PCL.Core.Helper.Java
{
    public enum JavaBrandType
    {
        Oracle,
        Microsoft,
        Bellsoft,
        AzulZulu,
        AmazonCorretto,
        OpenJDK,
        EclipseTemurin,
        Dragonwell,
        Kona,
        Unknown
    }
    public class JavaModel
    {
        /// <summary>
        /// 就像这样：
        /// D:\Program Files\Java24\bin
        /// </summary>
        public string Path { get; set; }
        public Version Version { get; set; }
        public JavaBrandType Brand { get; set; }
        /// <summary>
        /// 用户是否启动此 Java
        /// </summary>
        public bool IsEnabled { get; set; }
        public bool Is64Bit { get; set; }
        public bool IsJre { get; set; }
        public string JavaExePath => $@"{Path}\java.exe";
        public string JavawExePath => $@"{Path}\javaw.exe";
    }
}