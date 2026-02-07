using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.Java.Scanner;
public interface IJavaScanner
{
    void Scan(ICollection<string> results);
}

