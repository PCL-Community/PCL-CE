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
        OpenJDK,
        AdoptOpenJDK,
        AmazonCorretto,
        AzulZulu,
        Other
    }
    public class JavaModel
    {
        public string Path { get; set; }
        public Version Version { get; set; }
        public JavaBrandType Brand { get; set; }
    }
}