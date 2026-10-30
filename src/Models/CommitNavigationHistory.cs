using System.Collections.Generic;

namespace SourceGit.Models
{
    public class CommitNavigationHistory
    {
        public bool CanGoBack => _position > 0;
        public bool CanGoForward => _position + 1 < _commits.Count;
        public string Current => _position >= 0 ? _commits[_position] : null;
        public string Previous => CanGoBack ? _commits[_position - 1] : null;
        public string Next => CanGoForward ? _commits[_position + 1] : null;

        public void Visit(string sha)
        {
            if (_position >= 0 && _commits[_position] == sha)
                return;

            _commits.RemoveRange(_position + 1, _commits.Count - _position - 1);
            _commits.Add(sha);
            _position++;
        }

        public string Back()
        {
            return CanGoBack ? _commits[--_position] : null;
        }

        public string Forward()
        {
            return CanGoForward ? _commits[++_position] : null;
        }

        public void Forget(string sha)
        {
            for (var i = _commits.Count - 1; i >= 0; i--)
            {
                if (_commits[i] != sha)
                    continue;

                _commits.RemoveAt(i);
                if (i <= _position)
                    _position--;
            }
        }

        private readonly List<string> _commits = [];
        private int _position = -1;
    }
}
