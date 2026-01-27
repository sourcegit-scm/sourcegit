using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class QueryConflictContent
    {
        /// <summary>
        /// Gets the base version (common ancestor) of the conflicted file from stage :1:
        /// </summary>
        public static Task<string> GetBaseContentAsync(string repo, string file)
        {
            return GetContentFromStageAsync(repo, ":1", file);
        }

        /// <summary>
        /// Gets the ours version (current branch) of the conflicted file from stage :2:
        /// </summary>
        public static Task<string> GetOursContentAsync(string repo, string file)
        {
            return GetContentFromStageAsync(repo, ":2", file);
        }

        /// <summary>
        /// Gets the theirs version (incoming branch) of the conflicted file from stage :3:
        /// </summary>
        public static Task<string> GetTheirsContentAsync(string repo, string file)
        {
            return GetContentFromStageAsync(repo, ":3", file);
        }

        private static async Task<string> GetContentFromStageAsync(string repo, string stage, string file)
        {
            try
            {
                using var stream = await QueryFileContent.RunAsync(repo, stage, file).ConfigureAwait(false);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Parses conflict markers from the working copy content.
        /// Supports both standard and diff3 format.
        /// </summary>
        public static Models.MergeConflictDocument ParseConflictMarkers(string workingCopyContent, string baseContent, string oursContent, string theirsContent)
        {
            var doc = new Models.MergeConflictDocument
            {
                BaseContent = baseContent,
                OursContent = oursContent,
                TheirsContent = theirsContent,
                ResultContent = workingCopyContent
            };

            if (string.IsNullOrEmpty(workingCopyContent))
                return doc;

            var lines = workingCopyContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var regions = new List<Models.MergeConflictRegion>();

            int conflictStart = -1;

            var commonRegion = new Models.MergeConflictRegion();
            var oursBuilder = new StringBuilder();
            var baseBuilder = new StringBuilder();
            var theirsBuilder = new StringBuilder();

            ParseState state = ParseState.Common;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    // End previous common region if exists
                    if (state == ParseState.Common && commonRegion.StartLine < i)
                    {
                        commonRegion.EndLine = i - 1;
                        if (commonRegion.EndLine >= commonRegion.StartLine)
                            regions.Add(commonRegion);
                    }

                    conflictStart = i;
                    state = ParseState.Ours;
                    oursBuilder.Clear();
                    baseBuilder.Clear();
                    theirsBuilder.Clear();
                }
                else if (line.StartsWith("|||||||", StringComparison.Ordinal) && state == ParseState.Ours)
                {
                    // diff3 format - base section
                    state = ParseState.Base;
                }
                else if (line.StartsWith("=======", StringComparison.Ordinal) && (state == ParseState.Ours || state == ParseState.Base))
                {
                    state = ParseState.Theirs;
                }
                else if (line.StartsWith(">>>>>>>", StringComparison.Ordinal) && state == ParseState.Theirs)
                {
                    // End of conflict region
                    var conflictRegion = new Models.MergeConflictRegion
                    {
                        StartLine = conflictStart,
                        EndLine = i,
                        IsConflict = true,
                        OursContent = oursBuilder.ToString(),
                        BaseContent = baseBuilder.ToString(),
                        TheirsContent = theirsBuilder.ToString()
                    };
                    regions.Add(conflictRegion);

                    // Start new common region
                    commonRegion = new Models.MergeConflictRegion
                    {
                        StartLine = i + 1,
                        IsConflict = false
                    };
                    state = ParseState.Common;
                }
                else
                {
                    switch (state)
                    {
                        case ParseState.Ours:
                            if (oursBuilder.Length > 0)
                                oursBuilder.AppendLine();
                            oursBuilder.Append(line);
                            break;
                        case ParseState.Base:
                            if (baseBuilder.Length > 0)
                                baseBuilder.AppendLine();
                            baseBuilder.Append(line);
                            break;
                        case ParseState.Theirs:
                            if (theirsBuilder.Length > 0)
                                theirsBuilder.AppendLine();
                            theirsBuilder.Append(line);
                            break;
                    }
                }
            }

            // Handle trailing common region
            if (state == ParseState.Common && commonRegion.StartLine < lines.Length)
            {
                commonRegion.EndLine = lines.Length - 1;
                if (commonRegion.EndLine >= commonRegion.StartLine)
                    regions.Add(commonRegion);
            }

            doc.Regions = regions;
            return doc;
        }

        /// <summary>
        /// Gets all conflict markers in the content with their line positions.
        /// </summary>
        public static List<Models.ConflictMarkerInfo> GetConflictMarkers(string content)
        {
            var markers = new List<Models.ConflictMarkerInfo>();
            if (string.IsNullOrEmpty(content))
                return markers;

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int offset = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineStart = offset;
                var lineEnd = offset + line.Length;

                if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    markers.Add(new Models.ConflictMarkerInfo
                    {
                        LineNumber = i,
                        StartOffset = lineStart,
                        EndOffset = lineEnd,
                        Type = Models.ConflictMarkerType.Start
                    });
                }
                else if (line.StartsWith("|||||||", StringComparison.Ordinal))
                {
                    markers.Add(new Models.ConflictMarkerInfo
                    {
                        LineNumber = i,
                        StartOffset = lineStart,
                        EndOffset = lineEnd,
                        Type = Models.ConflictMarkerType.Base
                    });
                }
                else if (line.StartsWith("=======", StringComparison.Ordinal))
                {
                    markers.Add(new Models.ConflictMarkerInfo
                    {
                        LineNumber = i,
                        StartOffset = lineStart,
                        EndOffset = lineEnd,
                        Type = Models.ConflictMarkerType.Separator
                    });
                }
                else if (line.StartsWith(">>>>>>>", StringComparison.Ordinal))
                {
                    markers.Add(new Models.ConflictMarkerInfo
                    {
                        LineNumber = i,
                        StartOffset = lineStart,
                        EndOffset = lineEnd,
                        Type = Models.ConflictMarkerType.End
                    });
                }

                // Account for line ending (approximate)
                offset = lineEnd + (i < lines.Length - 1 ? Environment.NewLine.Length : 0);
            }

            return markers;
        }

        private enum ParseState
        {
            Common,
            Ours,
            Base,
            Theirs
        }
    }
}
