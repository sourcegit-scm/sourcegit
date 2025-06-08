using System.Collections.Generic;

namespace SourceGit.Models
{
    public class InlineElementCollector
    {
        public int Count => _implementation.Count;
        public InlineElement this[int index] => _implementation[index];

        public InlineElement Intersect(int start, int length)
        {
            foreach (var elem in _implementation)
            {
                if (elem.IsIntersecting(start, length))
                    return elem;
            }

            return null;
        }

        public void Add(InlineElement element)
        {
            _implementation.Add(element);
        }

        public void Sort()
        {
            _implementation.Sort((l, r) => l.Start.CompareTo(r.Start));
        }

        public void Clear()
        {
            _implementation.Clear();
        }

        private readonly List<InlineElement> _implementation = [];
    }
}
