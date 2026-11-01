namespace PCL;

public static partial class Loading
{
    private struct LoaderFolderDictionaryEntry : IEquatable<LoaderFolderDictionaryEntry>
    {
        public DateTime? lastCheckTime;
        public string folderPath;

        public override bool Equals(object obj)
        {
            return obj is LoaderFolderDictionaryEntry entry
                && EqualityComparer<DateTime?>.Default.Equals(lastCheckTime, entry.lastCheckTime)
                && (folderPath ?? "") == (entry.folderPath ?? "");
        }

        public bool Equals(LoaderFolderDictionaryEntry other)
        {
            return Nullable.Equals(lastCheckTime, other.lastCheckTime)
                && folderPath == other.folderPath;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(lastCheckTime, folderPath);
        }
    }
}
