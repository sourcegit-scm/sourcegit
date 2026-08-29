using System;
using System.Threading;

namespace DevBoard.Mcp
{
    public sealed class DevBoardMcpRequestLimiter
    {
        public DevBoardMcpRequestLimiter(int limit)
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
            public Lease(DevBoardMcpRequestLimiter owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _owner, null)?.Release();
            }

            private DevBoardMcpRequestLimiter _owner;
        }

        private readonly int _limit;
        private int _active;
    }
}
