using System.IO;

namespace SourceGit.Models
{
    /// <summary>
    ///     The "safetynet" — a single, global handler for every destructive deletion of a
    ///     user's working-copy files (discarding changes, cleaning untracked/ignored files,
    ///     resolving conflicts, ...).
    ///
    ///     All such deletions should go through <see cref="Delete"/> rather than calling
    ///     <c>File.Delete</c> / <c>Directory.Delete</c> (or <c>git clean</c>) directly. Whether a
    ///     deletion is recoverable is decided per-operation by the caller (driven by the user's
    ///     preferences):
    ///       * <c>useTrash == true</c>  → move to the OS trash/recycle bin (recoverable).
    ///       * <c>useTrash == false</c> → permanent delete (legacy behavior).
    ///
    ///     The actual per-platform trash behavior is provided by <see cref="Native.OS.MoveToTrash"/>:
    ///       * Windows: the Recycle Bin (implemented).
    ///       * macOS / Linux: permanent delete for now — a native trash is a future extension that
    ///         only needs a new backend implementation, no changes here or at the call sites.
    ///
    ///     NOTE: this must only ever be used for user files inside a repository working copy.
    ///     Internal temporary files (pathspec files, patch temps, cached avatars, ...) should
    ///     still use <c>File.Delete</c> so they don't pollute the trash bin.
    /// </summary>
    public static class TrashBin
    {
        /// <summary>
        ///     Delete a single file or directory, optionally routing it to the OS trash bin.
        /// </summary>
        /// <param name="fullPath">Absolute path to the file or directory.</param>
        /// <param name="useTrash">
        ///     When true the entry is moved to the trash bin; when false it is permanently deleted.
        /// </param>
        /// <param name="log">Optional command log to record the operation in.</param>
        /// <returns>
        ///     True if the entry was removed (or did not exist); false if the operation failed.
        /// </returns>
        public static bool Delete(string fullPath, bool useTrash, ICommandLog log = null)
        {
            if (string.IsNullOrEmpty(fullPath))
                return false;

            // Nothing to do if it is already gone; treat as success so callers stay simple.
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                return true;

            if (useTrash)
            {
                var moved = Native.OS.MoveToTrash(fullPath);
                log?.AppendLine(moved
                    ? $"$ [safetynet] moved to trash: {fullPath}"
                    : $"$ [safetynet] FAILED to move to trash: {fullPath}");
                return moved;
            }

            try
            {
                if (Directory.Exists(fullPath))
                    Directory.Delete(fullPath, true);
                else
                    File.Delete(fullPath);

                log?.AppendLine($"$ deleted: {fullPath}");
                return true;
            }
            catch
            {
                log?.AppendLine($"$ FAILED to delete: {fullPath}");
                return false;
            }
        }
    }
}
