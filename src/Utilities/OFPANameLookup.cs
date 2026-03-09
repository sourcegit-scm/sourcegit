using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SourceGit.Utilities
{
    internal static class OFPANameLookup
    {
        internal readonly struct WorkingTreeCandidate
        {
            public string RelativePath { get; }
            public string WorkingTreePath { get; }
            public string IndexObjectSpec { get; }
            public string HeadObjectSpec { get; }

            public bool HasWorkingTreeFile => !string.IsNullOrEmpty(WorkingTreePath);

            public WorkingTreeCandidate(string relativePath, string workingTreePath, string indexObjectSpec, string headObjectSpec)
            {
                RelativePath = relativePath;
                WorkingTreePath = workingTreePath;
                IndexObjectSpec = indexObjectSpec;
                HeadObjectSpec = headObjectSpec;
            }
        }

        internal readonly struct RevisionObjectSpec
        {
            public string RelativePath { get; }
            public string ObjectSpec { get; }

            public RevisionObjectSpec(string relativePath, string objectSpec)
            {
                RelativePath = relativePath;
                ObjectSpec = objectSpec;
            }
        }

        public static async Task<Dictionary<string, string>> LookupWorkingTreeAsync(string repositoryPath, IReadOnlyList<WorkingTreeCandidate> candidates)
        {
            var results = new Dictionary<string, string>(StringComparer.Ordinal);
            if (candidates.Count == 0)
                return results;

            var fallbackCandidates = new List<WorkingTreeCandidate>();
            foreach (var candidate in candidates)
            {
                if (candidate.HasWorkingTreeFile)
                {
                    var decoded = OFPAParser.Decode(candidate.WorkingTreePath);
                    if (decoded.HasValue)
                        results[candidate.RelativePath] = decoded.Value.LabelValue;
                    else
                        fallbackCandidates.Add(candidate);
                }
                else
                {
                    fallbackCandidates.Add(candidate);
                }
            }

            if (fallbackCandidates.Count == 0)
                return results;

            var indexSpecs = fallbackCandidates.Select(c => c.IndexObjectSpec).ToList();

            var indexData = await OFPAGitBatchReader.ReadAsync(repositoryPath, indexSpecs, OFPAParser.MaxSampleSize).ConfigureAwait(false);
            var headSpecs = new List<string>();
            var headCandidates = new List<WorkingTreeCandidate>();

            foreach (var candidate in fallbackCandidates)
            {
                if (TryDecode(results, candidate.RelativePath, candidate.IndexObjectSpec, indexData))
                    continue;

                if (!string.IsNullOrEmpty(candidate.HeadObjectSpec))
                {
                    headSpecs.Add(candidate.HeadObjectSpec);
                    headCandidates.Add(candidate);
                }
            }

            if (headCandidates.Count == 0)
                return results;

            var headData = await OFPAGitBatchReader.ReadAsync(repositoryPath, headSpecs, OFPAParser.MaxSampleSize).ConfigureAwait(false);
            foreach (var candidate in headCandidates)
                TryDecode(results, candidate.RelativePath, candidate.HeadObjectSpec, headData);

            return results;
        }

        public static async Task<Dictionary<string, string>> LookupRevisionObjectsAsync(string repositoryPath, IReadOnlyList<RevisionObjectSpec> specs)
        {
            var results = new Dictionary<string, string>(StringComparer.Ordinal);
            if (specs.Count == 0)
                return results;

            var objectSpecs = specs.Select(s => s.ObjectSpec).ToList();

            var batchResults = await OFPAGitBatchReader.ReadAsync(repositoryPath, objectSpecs, OFPAParser.MaxSampleSize).ConfigureAwait(false);
            foreach (var spec in specs)
                TryDecode(results, spec.RelativePath, spec.ObjectSpec, batchResults);

            return results;
        }

        private static bool TryDecode(Dictionary<string, string> results, string relativePath, string objectSpec, IReadOnlyDictionary<string, byte[]> batchResults)
        {
            if (!batchResults.TryGetValue(objectSpec, out var data) || data.Length == 0)
                return false;

            var decoded = OFPAParser.DecodeFromData(data);
            if (!decoded.HasValue)
                return false;

            results[relativePath] = decoded.Value.LabelValue;
            return true;
        }
    }
}
