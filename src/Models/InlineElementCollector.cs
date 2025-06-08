using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace SourceGit.Models
{
    public class InlineElementCollector : IEnumerable<InlineElement>
    {
        private readonly List<InlineElement> _implementation = [];

        public void Clear()
        {
            _implementation.Clear();

            AssertInvariant();
        }

        public int Count => _implementation.Count;

        public void Add(InlineElement element)
        {

            var index = FindIndex(element.Start);
            if (!IsIntersection(index, element.Start, element.Length))
                _implementation.Insert(index, element);

            AssertInvariant();
        }

        [Conditional("DEBUG")]
        private void AssertInvariant()
        {
            if (_implementation.Count == 0)
                return;

            for (var index = 1; index < _implementation.Count; index++)
            {
                var prev = _implementation[index - 1];
                var curr = _implementation[index];

                Debug.Assert(prev.Start + prev.Length <= curr.Start);
            }
        }

        public InlineElement Lookup(int position)
        {
            var index = FindIndex(position);
            return IsIntersection(index, position, 1)
                ? _implementation[index]
                : null;
        }

        private int FindIndex(int start)
        {
            var index = 0;
            while (index < _implementation.Count && _implementation[index].Start <= start)
                index++;

            return index;
        }

        private bool IsIntersection(int index, int start, int length)
        {
            if (index > 0)
            {
                var predecessor = _implementation[index - 1];
                if (predecessor.Start + predecessor.Length >= start)
                    return true;
            }

            if (index < _implementation.Count)
            {
                var successor = _implementation[index];
                if (start + length >= successor.Start)
                    return true;
            }

            return false;
        }

        public IEnumerator<InlineElement> GetEnumerator() => _implementation.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
