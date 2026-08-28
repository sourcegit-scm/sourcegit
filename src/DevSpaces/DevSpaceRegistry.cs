using System.Collections.Generic;

namespace SourceGit.DevSpaces
{
    public static class DevSpaceRegistry
    {
        public static ViewModels.DevSpaces GetOrCreate(ViewModels.Repository repository)
        {
            if (repository == null)
                return null;

            if (_spaces.TryGetValue(repository, out var existing))
                return existing;

            var created = new ViewModels.DevSpaces(repository.FullPath);
            _spaces.Add(repository, created);
            return created;
        }

        public static void Close(ViewModels.Repository repository)
        {
            if (repository == null || !_spaces.Remove(repository, out var spaces))
                return;

            spaces.Dispose();
        }

        public static void DisableAll()
        {
            foreach (var pair in _spaces)
            {
                if (pair.Key.SelectedViewIndex == 3)
                    pair.Key.SelectedViewIndex = 0;

                pair.Value.Dispose();
            }

            _spaces.Clear();
        }

        private static readonly Dictionary<ViewModels.Repository, ViewModels.DevSpaces> _spaces = [];
    }
}
