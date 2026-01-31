using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.Java;

public class JavaStorageItem
{
    public required string Path { get; init; }
    public bool IsEnable { get; init; }
    public JavaSource? Source { get; init; }
}
