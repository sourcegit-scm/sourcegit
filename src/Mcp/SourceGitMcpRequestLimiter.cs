using System;
using System.Threading;

namespace SourceGit.Mcp
{
    public sealed class SourceGitMcpRequestLimiter
    {
        public SourceGitMcpRequestLimiter(int limit)
        {
            if (limit <= 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            _limit = limit;
        }

        public bool TryEnter(out IDisposable lease)
        {
            if (Interlocked.Increment(ref _active) > _limit)
            {
                Interlocked.Decrement(ref _active);
                lease = null;
                return false;
            }

            lease = new Lease(this);
            return true;
        }

        private void Release()
        {
            Interlocked.Decrement(ref _active);
        }

        private sealed class Lease : IDisposable
        {
            public Lease(SourceGitMcpRequestLimiter owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _owner, null)?.Release();
            }

            private SourceGitMcpRequestLimiter _owner;
        }

        private readonly int _limit;
        private int _active;
    }
}
