using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Utilities
{
    /// <summary>
    /// Shared async decoding context for OFPA filenames.
    /// Handles scheduling, stale-guard, PropertyChanged notification,
    /// and reactive enable/disable in response to Repository.EnableOFPADecoding changes.
    /// </summary>
    internal sealed class OFPADecodingContext : ObservableObject, IDisposable
    {
        public IReadOnlyDictionary<string, string> DecodedPaths => _decodedPaths;

        public OFPADecodingContext(ViewModels.Repository repo, Action onReEnabled)
        {
            _repo = repo;
            _onReEnabled = onReEnabled;
            _repo.PropertyChanged += OnRepositoryPropertyChanged;
        }

        public void Dispose()
        {
            if (_repo != null)
                _repo.PropertyChanged -= OnRepositoryPropertyChanged;
            _repo = null;
            _onReEnabled = null;
        }

        /// <summary>
        /// Schedule an async OFPA decode. The caller provides a factory
        /// that returns the decoded path map. Stale requests are discarded.
        /// </summary>
        public void ScheduleRefresh(Func<Task<Dictionary<string, string>>> lookupFactory)
        {
            var requestId = Interlocked.Increment(ref _requestId);
            _ = RunAsync(lookupFactory, requestId);
        }

        public void Clear()
        {
            Interlocked.Increment(ref _requestId);
            _decodedPaths = null;
            OnPropertyChanged(nameof(DecodedPaths));
        }

        private async Task RunAsync(Func<Task<Dictionary<string, string>>> lookupFactory, long requestId)
        {
            Dictionary<string, string> results = null;
            try
            {
                results = await lookupFactory().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Decode failures are non-fatal; raw paths remain visible.
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_repo == null || !_repo.EnableOFPADecoding || requestId != Interlocked.Read(ref _requestId))
                    return;

                _decodedPaths = results;
                OnPropertyChanged(nameof(DecodedPaths));
            });
        }

        private void OnRepositoryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ViewModels.Repository.EnableOFPADecoding))
                return;

            if (_repo?.EnableOFPADecoding == true)
                _onReEnabled?.Invoke();
            else
                Clear();
        }

        private ViewModels.Repository _repo;
        private Action _onReEnabled;
        private Dictionary<string, string> _decodedPaths;
        private long _requestId;
    }
}
