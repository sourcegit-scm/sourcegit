using System.Collections.Generic;

using Avalonia.Controls;

namespace SourceGit.DevSpaces
{
    public static class DevSpaceRegistry
    {
        public static ViewModels.DevSpaces GetOrCreate(ViewModels.Repository repository)
        {
            return GetOrCreateEntry(repository)?.Model;
        }

        public static ViewModels.DevSpaces Attach(ViewModels.Repository repository, Border host)
        {
            if (repository == null || host == null)
                return null;

            var entry = GetOrCreateEntry(repository);
            if (entry.Host != null &&
                !ReferenceEquals(entry.Host, host) &&
                ReferenceEquals(entry.Host.Child, entry.View))
            {
                entry.Host.Child = null;
            }

            if (!ReferenceEquals(host.Child, entry.View))
                host.Child = entry.View;

            entry.Host = host;
            return entry.Model;
        }

        public static void Close(ViewModels.Repository repository)
        {
            if (repository == null || !_spaces.Remove(repository, out var entry))
                return;

            if (entry.Host != null && ReferenceEquals(entry.Host.Child, entry.View))
                entry.Host.Child = null;

            entry.Model.Dispose();
            entry.View.Dispose();
        }

        public static void DisableAll()
        {
            foreach (var pair in _spaces)
            {
                if (pair.Key.SelectedViewIndex == 3)
                    pair.Key.SelectedViewIndex = 0;

                if (pair.Value.Host != null && ReferenceEquals(pair.Value.Host.Child, pair.Value.View))
                    pair.Value.Host.Child = null;

                pair.Value.Model.Dispose();
                pair.Value.View.Dispose();
            }

            _spaces.Clear();
        }

        private static Entry GetOrCreateEntry(ViewModels.Repository repository)
        {
            if (repository == null)
                return null;

            if (_spaces.TryGetValue(repository, out var existing))
                return existing;

            var model = new ViewModels.DevSpaces(repository.FullPath);
            var view = new Views.DevSpaces
            {
                DataContext = model,
            };
            var created = new Entry(model, view);
            _spaces.Add(repository, created);
            return created;
        }

        private sealed class Entry
        {
            public ViewModels.DevSpaces Model { get; }
            public Views.DevSpaces View { get; }
            public Border Host { get; set; }

            public Entry(ViewModels.DevSpaces model, Views.DevSpaces view)
            {
                Model = model;
                View = view;
            }
        }

        private static readonly Dictionary<ViewModels.Repository, Entry> _spaces = [];
    }
}
