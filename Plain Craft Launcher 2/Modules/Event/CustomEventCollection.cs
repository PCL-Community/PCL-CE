using System.Collections;
using System.Windows.Markup;

namespace PCL
{
    [ContentProperty("Events")]
    public class CustomEventCollection : IEnumerable<CustomEvent>
    {
        private readonly List<CustomEvent> _events = new();

        public List<CustomEvent> Events => _events;

        public IEnumerator<CustomEvent> GetEnumerator() => Events.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
