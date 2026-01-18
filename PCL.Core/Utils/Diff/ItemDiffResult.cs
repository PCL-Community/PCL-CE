using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Core.Utils.Diff
{
    public class ItemDiffResult<T>
    {
        public IReadOnlyList<T> Added { get; }
        public IReadOnlyList<T> Removed { get; }
        public IReadOnlyList<T> Unchanged { get; }

        public ItemDiffResult(IReadOnlyList<T> added, IReadOnlyList<T> removed, IReadOnlyList<T> unchanged)
        {
            Added = added ?? throw new ArgumentNullException(nameof(added));
            Removed = removed ?? throw new ArgumentNullException(nameof(removed));
            Unchanged = unchanged ?? throw new ArgumentNullException(nameof(unchanged));
        }
    }
}
