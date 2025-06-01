using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Core.Utils
{
    public static class Programe
    {

        /// <summary>
        /// 通过 PE 头结构判断文件是否为 64 位可执行文件
        /// </summary>
        public static bool IsExecutableFile64Bit(string filePath)
        {
            var peStruct = PE.PEHeaderReader.ReadPEHeader(filePath);
            return peStruct.IsValid && peStruct.Is64Bit;
        }
    }
}