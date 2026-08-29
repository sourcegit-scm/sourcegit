using System;
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

            entry.Repository = repository;
            entry.Host = host;
            return entry.Model;
        }

        public static void Close(ViewModels.Repository repository)
        {
            if (repository == null || !_spaces.Remove(repository.FullPath, out var entry))
                return;

            if (entry.Host != null && ReferenceEquals(entry.Host.Child, entry.View))
                entry.Host.Child = null;

            entry.Model.Dispose();
            entry.View.Dispose();
        }

        public static void DisableAll()
        {
            foreach (var entry in _spaces.Values)
            {
                if (entry.Repository?.SelectedViewIndex == 3)
                    entry.Repository.SelectedViewIndex = 0;

                if (entry.Host != null && ReferenceEquals(entry.Host.Child, entry.View))
                    entry.Host.Child = null;

                entry.Model.Dispose();
                entry.View.Dispose();
            }

            _spaces.Clear();
        }

        private static Entry GetOrCreateEntry(ViewModels.Repository repository)
        {
            if (repository == null || string.IsNullOrEmpty(repository.FullPath))
                return null;

            if (_spaces.TryGetValue(repository.FullPath, out var existing))
            {
                existing.Repository = repository;
                return existing;
            }

            var model = new ViewModels.DevSpaces(repository, repository.FullPath);
            var view = new Views.DevSpaces
            {
                DataContext = model,
            };
            var created = new Entry(repository, model, view);
            _spaces.Add(repository.FullPath, created);
            return created;
        }

        private sealed class Entry
        {
            public ViewModels.Repository Repository { get; set; }
            public ViewModels.DevSpaces Model { get; }
            public Views.DevSpaces View { get; }
            public Border Host { get; set; }

            public Entry(ViewModels.Repository repository, ViewModels.DevSpaces model, Views.DevSpaces view)
            {
                Repository = repository;
                Model = model;
                View = view;
            }
        }

        private static readonly StringComparer _pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        private static readonly Dictionary<string, Entry> _spaces = new(_pathComparer);
    }
}
