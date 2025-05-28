using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Core.Java
{
    public class JavaModel
    {
        public string Path { get; set; }
        public Version Version { get; set; }
        public JavaBrandType Brand { get; set; }
    }

    public enum JavaBrandType
    {
        Oracle,
        OpenJDK,
        AdoptOpenJDK,
        AmazonCorretto,
        AzulZulu,
        Other
    }
}
