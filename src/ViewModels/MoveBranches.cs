using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class MoveBranches : Popup
    {
        public const string LocalPrefix = "refs/heads/";

        public string Target
        {
            get;
        }

        public List<string> Preview
        {
            get;
        } = [];

        public string ErrorMessage
        {
            get;
        } = string.Empty;

        public bool HasError
        {
            get => !string.IsNullOrEmpty(ErrorMessage);
        }

        public MoveBranches(Repository repo, IReadOnlyList<string> paths, string targetPrefix)
        {
            _repo = repo;
            Target = string.IsNullOrEmpty(targetPrefix) ? App.Text("MoveBranches.Root") : $"{targetPrefix}/";

            foreach (var path in paths)
            {
                if (!path.StartsWith(LocalPrefix, StringComparison.Ordinal))
                    continue;

                var parent = GetParent(path.Substring(LocalPrefix.Length));
                if (parent.Equals(targetPrefix, StringComparison.Ordinal))
                    continue;

                var folderPrefix = $"{path}/";
                foreach (var branch in repo.Branches)
                {
                    if (!branch.IsLocal)
                        continue;

                    if (branch.FullName.Equals(path, StringComparison.Ordinal) ||
                        branch.FullName.StartsWith(folderPrefix, StringComparison.Ordinal))
                    {
                        var leaf = parent.Length == 0 ? branch.Name : branch.Name.Substring(parent.Length + 1);
                        var newName = targetPrefix.Length == 0 ? leaf : $"{targetPrefix}/{leaf}";
                        _moves.Add(new KeyValuePair<Models.Branch, string>(branch, newName));
                        Preview.Add($"{branch.Name}  →  {newName}");
                    }
                }
            }

            ErrorMessage = Validate();
        }

        // Whether dropping the given tree node paths on the folder `targetPrefix` ("" for the top level) would
        // move anything. Folders cannot be dropped into themselves, and items already there are no-ops.
        public static bool CanDropOnto(IReadOnlyList<string> paths, string targetPrefix)
        {
            if (paths.Count == 0)
                return false;

            var anyMoves = false;
            foreach (var path in paths)
            {
                if (!path.StartsWith(LocalPrefix, StringComparison.Ordinal))
                    return false;

                var relative = path.Substring(LocalPrefix.Length);
                if (targetPrefix.Equals(relative, StringComparison.Ordinal) ||
                    targetPrefix.StartsWith($"{relative}/", StringComparison.Ordinal))
                    return false;

                if (!GetParent(relative).Equals(targetPrefix, StringComparison.Ordinal))
                    anyMoves = true;
            }

            return anyMoves;
        }

        public override async Task<bool> Sure()
        {
            if (HasError)
            {
                _repo.SendNotification(ErrorMessage, true);
                return false;
            }

            using var lockWatcher = _repo.LockWatcher();

            var log = _repo.CreateLog("Move Branches");
            Use(log);

            var succ = true;
            foreach (var (branch, newName) in _moves)
            {
                ProgressDescription = $"Rename '{branch.Name}'";

                succ = await new Commands.Branch(_repo.FullPath, branch.Name)
                    .Use(log)
                    .RenameAsync(newName);

                if (!succ)
                    break;

                _repo.RefreshAfterRenameBranch(branch, newName);
            }

            log.Complete();
            return succ;
        }

        private string Validate()
        {
            if (_moves.Count == 0)
                return "Nothing to move!";

            var moving = new HashSet<Models.Branch>();
            foreach (var (branch, _) in _moves)
                moving.Add(branch);

            for (var i = 0; i < _moves.Count; i++)
            {
                var name = _moves[i].Value;
                if (!Models.RefName.IsValidBranchName(name))
                    return $"Bad branch name format: {name}";

                foreach (var other in _repo.Branches)
                {
                    if (!other.IsLocal || moving.Contains(other))
                        continue;

                    if (Conflicts(name, other.Name))
                        return $"A branch named '{other.Name}' already exists!!!";
                }

                for (var j = i + 1; j < _moves.Count; j++)
                {
                    if (Conflicts(name, _moves[j].Value))
                        return $"Branches would end up with the same name: {name}";
                }
            }

            return string.Empty;
        }

        // Two branch names cannot coexist when they are equal or one is a "directory" of the other.
        private static bool Conflicts(string a, string b)
        {
            return a.Equals(b, StringComparison.Ordinal) ||
                a.StartsWith($"{b}/", StringComparison.Ordinal) ||
                b.StartsWith($"{a}/", StringComparison.Ordinal);
        }

        private static string GetParent(string relativePath)
        {
            var idx = relativePath.LastIndexOf('/');
            return idx < 0 ? string.Empty : relativePath.Substring(0, idx);
        }

        private readonly Repository _repo;
        private readonly List<KeyValuePair<Models.Branch, string>> _moves = [];
    }
}
