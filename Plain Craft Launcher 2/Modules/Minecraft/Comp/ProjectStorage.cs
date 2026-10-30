namespace PCL;

public static partial class Comp
{
    public class CompProjectStorage
    {
        public int curseForgeOffset;
        public int curseForgeTotal = -1;

        /// <summary>
        ///     当前的错误信息。如果没有则为 Nothing。
        /// </summary>
        public string? errorMessage = null;

        public int modrinthOffset;
        public int modrinthTotal = -1;

        /// <summary>
        ///     可供展示的所有工程的列表。
        /// </summary>
        public List<CompProject> results = [];
    }
}
