using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QuerySelectedCommitChanges : Command
    {
        public QuerySelectedCommitChanges(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Editor = EditorType.None;
        }

        /// <param name="commits">Selected commits in history order, newest first.</param>
        public async Task<List<Models.CommitChange>> ReadAsync(IReadOnlyList<Models.Commit> commits, CancellationToken cancellationToken)
        {
            var changes = new List<Models.CommitChange>();
            for (var i = commits.Count - 1; i >= 0; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var commit = commits[i];
                var parentChanges = new List<List<Models.CommitChange>>();
                if (commit.Parents.Count == 0)
                {
                    // --root works without writing an empty tree into the object database.
                    Args = $"diff-tree --root --no-commit-id --raw -z -r -M --no-ext-diff --ignore-submodules=none --no-abbrev {commit.SHA}";
                    var output = await ReadRawAsync(cancellationToken).ConfigureAwait(false);
                    parentChanges.Add(Parse(output, commit.FirstParentToCompare, commit.SHA));
                }
                else
                {
                    foreach (var parent in commit.Parents)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Args = $"diff-tree --no-commit-id --raw -z -r -M --no-ext-diff --ignore-submodules=none --no-abbrev {parent} {commit.SHA}";
                        var output = await ReadRawAsync(cancellationToken).ConfigureAwait(false);
                        parentChanges.Add(Parse(output, parent, commit.SHA));
                    }
                }

                changes.AddRange(MergeParents(parentChanges));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Models.CommitChange.Zip(changes);
        }

        /// <summary>
        /// Combined merge changes, as in the Rebased log: a path must differ from
        /// every parent. Renames are matched by destination, not source path.
        /// Retain the first parent's real endpoints for content loading.
        /// </summary>
        public static List<Models.CommitChange> MergeParents(IReadOnlyList<List<Models.CommitChange>> parents)
        {
            if (parents.Count == 0)
                return [];
            if (parents.Count == 1)
                return parents[0];

            var maps = new List<Dictionary<string, Models.CommitChange>>();
            for (var i = 1; i < parents.Count; i++)
            {
                var map = new Dictionary<string, Models.CommitChange>(StringComparer.Ordinal);
                foreach (var change in parents[i])
                    map[change.Path] = change;
                maps.Add(map);
            }

            var result = new List<Models.CommitChange>();
            foreach (var first in parents[0])
            {
                var presentInAll = true;
                var status = first.Index;
                foreach (var map in maps)
                {
                    if (!map.TryGetValue(first.Path, out var other))
                    {
                        presentInAll = false;
                        break;
                    }

                    if (status != other.Index)
                        status = status == Models.ChangeState.Deleted || other.Index == Models.ChangeState.Deleted ?
                            Models.ChangeState.Deleted : Models.ChangeState.Modified;
                    else if (status == Models.ChangeState.Renamed && !first.BeforePath.Equals(other.BeforePath, StringComparison.Ordinal))
                        status = Models.ChangeState.Modified;
                }

                if (!presentInAll)
                    continue;

                first.Index = status;
                if (status != Models.ChangeState.Renamed)
                    first.OriginalPath = string.Empty;
                result.Add(first);
            }

            return result;
        }

        public static List<Models.CommitChange> Parse(string output, string beforeRevision, string afterRevision)
        {
            var result = new List<Models.CommitChange>();
            var fields = output.Split('\0');
            for (var i = 0; i < fields.Length - 1;)
            {
                var header = fields[i++].Split(' ');
                if (header.Length != 5 || !header[0].StartsWith(':') || i >= fields.Length - 1)
                    throw new InvalidDataException("Invalid raw diff from Git.");

                var path = fields[i++];
                var status = header[4][0];
                var target = path;
                if (status is 'R' or 'C')
                {
                    if (i >= fields.Length - 1)
                        throw new InvalidDataException("Missing rename destination from Git.");
                    target = fields[i++];
                }

                var change = new Models.CommitChange
                {
                    BeforeRevision = status == 'A' ? string.Empty : beforeRevision,
                    BeforePath = status == 'A' ? string.Empty : path,
                    BeforeMode = header[0][1..],
                    BeforeObject = status == 'A' ? string.Empty : header[2],
                    AfterRevision = status == 'D' ? string.Empty : afterRevision,
                    AfterPath = status == 'D' ? string.Empty : target,
                    AfterMode = header[1],
                    AfterObject = status == 'D' ? string.Empty : header[3],
                };
                change.UpdateStatus();
                result.Add(change);
            }

            return result;
        }

        private async Task<string> ReadRawAsync(CancellationToken cancellationToken)
        {
            using var process = new Process { StartInfo = CreateGitStartInfo(true) };
            process.Start();
            // Drain both pipes concurrently, and own the process lifetime when a
            // newer selection cancels this read-only query.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch (InvalidOperationException)
                {
                    // The process may exit between HasExited and Kill.
                }
                await process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                throw;
            }

            var output = await stdout.ConfigureAwait(false);
            var error = await stderr.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(error.Trim());
            return output;
        }
    }
}
