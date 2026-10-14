using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SourceGit.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter<PullRequestPlatformType>))]
    public enum PullRequestPlatformType
    {
        None = 0,
        GitHub,
        GitLab,
        Gitea,
        Forgejo,
        BitbucketCloud,
        AzureDevOps,
        Gitee,
    }

    public class PullRequestPlatform
    {
        public const int MaxURLLength = 7000;

        public static readonly List<PullRequestPlatform> Supported = [
            new(PullRequestPlatformType.GitHub, "GitHub", true, true, true),
            new(PullRequestPlatformType.GitLab, "GitLab", true, true, true),
            new(PullRequestPlatformType.Gitea, "Gitea", true, true, true),
            new(PullRequestPlatformType.Forgejo, "Forgejo / Codeberg", false, false, true),
            new(PullRequestPlatformType.BitbucketCloud, "Bitbucket Cloud", false, false, false),
            new(PullRequestPlatformType.AzureDevOps, "Azure DevOps", false, false, false),
            new(PullRequestPlatformType.Gitee, "Gitee", false, false, false),
        ];

        public PullRequestPlatformType Type { get; }
        public string Name { get; }
        public bool SupportsTitle { get; }
        public bool SupportsBody { get; }
        public bool SupportsCrossRepo { get; }

        public PullRequestPlatform(PullRequestPlatformType type, string name, bool supportsTitle, bool supportsBody, bool supportsCrossRepo)
        {
            Type = type;
            Name = name;
            SupportsTitle = supportsTitle;
            SupportsBody = supportsBody;
            SupportsCrossRepo = supportsCrossRepo;
        }

        public static PullRequestPlatform Get(PullRequestPlatformType type)
        {
            return Supported.Find(x => x.Type == type);
        }

        public static PullRequestPlatformType Detect(string host)
        {
            if (string.IsNullOrEmpty(host))
                return PullRequestPlatformType.None;

            host = host.ToLowerInvariant();

            if (host.Contains("github", StringComparison.Ordinal))
                return PullRequestPlatformType.GitHub;

            if (host.Contains("gitlab", StringComparison.Ordinal))
                return PullRequestPlatformType.GitLab;

            if (host.Equals("codeberg.org", StringComparison.Ordinal) || host.Contains("forgejo", StringComparison.Ordinal))
                return PullRequestPlatformType.Forgejo;

            if (host.Contains("gitea", StringComparison.Ordinal))
                return PullRequestPlatformType.Gitea;

            if (host.Equals("bitbucket.org", StringComparison.Ordinal))
                return PullRequestPlatformType.BitbucketCloud;

            if (host.Equals("dev.azure.com", StringComparison.Ordinal) || host.EndsWith("visualstudio.com", StringComparison.Ordinal))
                return PullRequestPlatformType.AzureDevOps;

            if (host.Equals("gitee.com", StringComparison.Ordinal))
                return PullRequestPlatformType.Gitee;

            return PullRequestPlatformType.None;
        }

        public static PullRequestPlatform Resolve(Remote remote, RepositorySettings settings)
        {
            if (remote == null)
                return null;

            if (settings != null &&
                settings.PullRequestPlatforms.TryGetValue(remote.Name, out var overrided) &&
                overrided != PullRequestPlatformType.None)
                return Get(overrided);

            if (!remote.TryGetVisitURL(out var url))
                return null;

            return Get(Detect(new Uri(url).Host));
        }

        /// <summary>
        ///     Builds the URL of the platform's "create pull request" page.
        /// </summary>
        /// <param name="targetBaseURL">Web URL of the repository that receives the pull request</param>
        /// <param name="sourceBaseURL">Web URL of the repository that contains the source branch (same as target if not a fork)</param>
        /// <param name="contentDropped">True if the title or body could not be passed by URL</param>
        public string BuildCreateURL(string targetBaseURL, string sourceBaseURL, string sourceBranch, string targetBranch, string title, string body, out bool contentDropped)
        {
            contentDropped = (!string.IsNullOrEmpty(title) && !SupportsTitle) || (!string.IsNullOrEmpty(body) && !SupportsBody);
            title = SupportsTitle ? title : null;
            body = SupportsBody ? body : null;

            var url = BuildCreateURLWithContent(targetBaseURL, sourceBaseURL, sourceBranch, targetBranch, title, body);
            if (url.Length > MaxURLLength && !string.IsNullOrEmpty(body))
            {
                contentDropped = true;
                url = BuildCreateURLWithContent(targetBaseURL, sourceBaseURL, sourceBranch, targetBranch, title, null);
            }

            if (url.Length > MaxURLLength && !string.IsNullOrEmpty(title))
                url = BuildCreateURLWithContent(targetBaseURL, sourceBaseURL, sourceBranch, targetBranch, null, null);

            return url;
        }

        private string BuildCreateURLWithContent(string targetBaseURL, string sourceBaseURL, string sourceBranch, string targetBranch, string title, string body)
        {
            targetBaseURL = targetBaseURL.TrimEnd('/');
            sourceBaseURL = string.IsNullOrEmpty(sourceBaseURL) ? targetBaseURL : sourceBaseURL.TrimEnd('/');

            var isCrossRepo = !sourceBaseURL.Equals(targetBaseURL, StringComparison.OrdinalIgnoreCase);
            var builder = new StringBuilder(1024);

            switch (Type)
            {
                case PullRequestPlatformType.GitHub:
                case PullRequestPlatformType.Gitea:
                case PullRequestPlatformType.Forgejo:
                    {
                        var head = EscapePath(sourceBranch);
                        if (isCrossRepo)
                            head = $"{Uri.EscapeDataString(GetOwner(sourceBaseURL))}:{head}";

                        builder.Append(targetBaseURL).Append("/compare/").Append(EscapePath(targetBranch)).Append("...").Append(head);
                        builder.Append(Type == PullRequestPlatformType.GitHub ? "?quick_pull=1" : "?expand=1");
                        AppendQuery(builder, "title", title);
                        AppendQuery(builder, "body", body);
                        break;
                    }
                case PullRequestPlatformType.GitLab:
                    // For forks, GitLab creates the merge request from the source project, and the target project defaults to the upstream one.
                    builder.Append(sourceBaseURL).Append("/-/merge_requests/new?");
                    builder.Append("merge_request%5Bsource_branch%5D=").Append(Uri.EscapeDataString(sourceBranch));
                    AppendQuery(builder, "merge_request%5Btarget_branch%5D", targetBranch);
                    AppendQuery(builder, "merge_request%5Btitle%5D", title);
                    AppendQuery(builder, "merge_request%5Bdescription%5D", body);
                    break;
                case PullRequestPlatformType.BitbucketCloud:
                    builder.Append(targetBaseURL).Append("/pull-requests/new?source=").Append(Uri.EscapeDataString(sourceBranch));
                    AppendQuery(builder, "dest", targetBranch);
                    break;
                case PullRequestPlatformType.AzureDevOps:
                    builder.Append(targetBaseURL).Append("/pullrequestcreate?sourceRef=").Append(Uri.EscapeDataString(sourceBranch));
                    AppendQuery(builder, "targetRef", targetBranch);
                    break;
                case PullRequestPlatformType.Gitee:
                    builder.Append(targetBaseURL).Append("/pulls/new?source=").Append(Uri.EscapeDataString(sourceBranch));
                    AppendQuery(builder, "target", targetBranch);
                    break;
                default:
                    return targetBaseURL;
            }

            return builder.ToString();
        }

        private static void AppendQuery(StringBuilder builder, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            builder.Append('&').Append(key).Append('=').Append(Uri.EscapeDataString(value));
        }

        private static string EscapePath(string branch)
        {
            var segments = branch.Split('/');
            for (int i = 0; i < segments.Length; i++)
                segments[i] = Uri.EscapeDataString(segments[i]);
            return string.Join('/', segments);
        }

        private static string GetOwner(string baseURL)
        {
            var path = new Uri(baseURL).AbsolutePath.Trim('/');
            var idx = path.IndexOf('/');
            return idx > 0 ? path.Substring(0, idx) : path;
        }
    }
}
