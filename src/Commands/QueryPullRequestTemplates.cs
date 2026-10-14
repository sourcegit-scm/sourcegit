using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     Finds the pull/merge request templates (GitHub, GitLab, Gitea, Forgejo, Azure DevOps) in the given revision.
    /// </summary>
    public class QueryPullRequestTemplates
    {
        public QueryPullRequestTemplates(string repo, string revision)
        {
            _repo = repo;
            _revision = revision;
        }

        public async Task<List<Models.PullRequestTemplate>> GetResultAsync()
        {
            var templates = new List<Models.PullRequestTemplate>();

            var lister = new QueryRevisionFileNames(_repo, _revision, LOOKUP_PATHS);
            var files = await lister.GetResultAsync().ConfigureAwait(false);
            foreach (var file in files)
            {
                if (!IsTemplate(file, out var isDefault))
                    continue;

                using var stream = await QueryFileContent.RunAsync(_repo, _revision, file).ConfigureAwait(false);
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync().ConfigureAwait(false);

                var template = new Models.PullRequestTemplate()
                {
                    Name = file,
                    Content = content.Trim(),
                    IsDefault = isDefault,
                };

                if (isDefault)
                    templates.Insert(0, template);
                else
                    templates.Add(template);
            }

            return templates;
        }

        private static bool IsTemplate(string file, out bool isDefault)
        {
            isDefault = false;

            var path = file.ToLowerInvariant();
            if (!path.EndsWith(".md", StringComparison.Ordinal))
                return false;

            var name = Path.GetFileName(path);
            var dir = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;

            if (name.Equals("pull_request_template.md", StringComparison.Ordinal))
            {
                isDefault = true;
                return Array.IndexOf(SINGLE_TEMPLATE_DIRS, dir) >= 0;
            }

            if (dir.Equals(".gitlab/merge_request_templates", StringComparison.Ordinal))
            {
                isDefault = name.Equals("default.md", StringComparison.Ordinal);
                return true;
            }

            return Array.IndexOf(MULTIPLE_TEMPLATES_DIRS, dir) >= 0;
        }

        private static readonly string[] LOOKUP_PATHS = [
            ".github", ".gitea", ".forgejo", ".gitlab/merge_request_templates", ".azuredevops", ".vsts",
            "docs/pull_request_template.md", "docs/PULL_REQUEST_TEMPLATE.md", "pull_request_template.md", "PULL_REQUEST_TEMPLATE.md",
        ];

        private static readonly string[] SINGLE_TEMPLATE_DIRS = ["", ".github", ".gitea", ".forgejo", ".azuredevops", ".vsts", "docs"];
        private static readonly string[] MULTIPLE_TEMPLATES_DIRS = [".github/pull_request_template", ".gitea/pull_request_template", ".forgejo/pull_request_template"];

        private readonly string _repo;
        private readonly string _revision;
    }
}
