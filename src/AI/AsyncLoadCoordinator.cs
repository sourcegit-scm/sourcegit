using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevBoard.AI
{
    internal sealed class AsyncLoadCoordinator<T> : IDisposable where T : class, IDisposable
    {
        public T Current
        {
            get
            {
                lock (_gate)
                    return _current;
            }
        }

        public bool IsLoading
        {
            get
            {
                lock (_gate)
                    return _loadTask != null;
            }
        }

        public Task<T> GetOrLoadAsync(Func<T> factory, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(factory);

            Task<T> task;
            lock (_gate)
            {
                if (_current != null)
                    return Task.FromResult(_current);

                task = _loadTask;
                if (task == null)
                {
                    var generation = _generation;
                    task = LoadAsync(factory, generation);
                    _loadTask = task;
                }
            }

            return cancellationToken.CanBeCanceled ? task.WaitAsync(cancellationToken) : task;
        }

        public void Reset()
        {
            T current;
            lock (_gate)
            {
                _generation++;
                current = _current;
                _current = null;
                _loadTask = null;
            }

            current?.Dispose();
        }

        public void Dispose()
        {
            Reset();
        }

        private async Task<T> LoadAsync(Func<T> factory, long generation)
        {
            await Task.Yield();

            T loaded;
            try
            {
                loaded = await Task.Run(factory).ConfigureAwait(false);
            }
            catch
            {
                lock (_gate)
                {
                    if (generation == _generation)
                        _loadTask = null;
                }

                throw;
            }

            lock (_gate)
            {
                if (generation != _generation)
                {
                    loaded.Dispose();
                    throw new OperationCanceledException("The model load was invalidated by a configuration change.");
                }

                _current = loaded;
                _loadTask = null;
                return loaded;
            }
        }

        private readonly object _gate = new();
        private long _generation;
        private T _current;
        private Task<T> _loadTask;
    }
}
