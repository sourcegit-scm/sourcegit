using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;

namespace SourceGit.Models
{
    public partial class Remote
    {
        [GeneratedRegex(@"^https?://[^/]+/.+[^/\.]$")]
        private static partial Regex REG_HTTPS();

        [GeneratedRegex(@"^git://[^/]+/.+[^/\.]$")]
        private static partial Regex REG_GIT();

        [GeneratedRegex(@"^[\w\-]+@[\w\.\-]+(\:[0-9]+)?:([a-zA-z0-9~%][\w\-\./~%]*)?[a-zA-Z0-9](\.git)?$")]
        private static partial Regex REG_SSH1();

        [GeneratedRegex(@"^ssh://([\w\-]+@)?[\w\.\-]+(\:[0-9]+)?/([a-zA-z0-9~%][\w\-\./~%]*)?[a-zA-Z0-9](\.git)?$")]
        private static partial Regex REG_SSH2();

        [GeneratedRegex(@"^[\w\-\.]+@([\w\.\-]+):/?([\w\.\-/~%]+/[\w\-\.%]+?)(\.git)?/?$")]
        private static partial Regex REG_TO_VISIT_URL_CAPTURE_SCP();

        [GeneratedRegex(@"^ssh://([\w\-\.]+@)?([\w\.\-]+)(\:[0-9]+)?/([\w\.\-/~%]+/[\w\-\.%]+?)(\.git)?/?$")]
        private static partial Regex REG_TO_VISIT_URL_CAPTURE_SSH();

        private static readonly Regex[] URL_FORMATS = [
            REG_HTTPS(),
            REG_GIT(),
            REG_SSH1(),
            REG_SSH2(),
        ];

        public string Name { get; set; }
        public string URL { get; set; }
        public string PushURL { get; set; }
        public string PrivateSSHKey { get; set; }
        public bool DisableAutoFetch { get; set; }

        public static bool IsSSH(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (REG_SSH1().IsMatch(url))
                return true;

            return REG_SSH2().IsMatch(url);
        }

        public static bool IsValidURL(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            foreach (var fmt in URL_FORMATS)
            {
                if (fmt.IsMatch(url))
                    return true;
            }

            return url.StartsWith("file://", StringComparison.Ordinal) ||
                url.StartsWith("./", StringComparison.Ordinal) ||
                url.StartsWith("../", StringComparison.Ordinal) ||
                Directory.Exists(url);
        }

        public bool TryGetVisitURL(out string url)
        {
            url = null;

            if (URL.StartsWith("http://", StringComparison.Ordinal) || URL.StartsWith("https://", StringComparison.Ordinal))
            {
                var trimmed = URL.EndsWith(".git", StringComparison.Ordinal) ? URL.Substring(0, URL.Length - 4) : URL;
                var uri = new Uri(trimmed);
                if (uri.Port != 80 && uri.Port != 443)
                    url = $"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.AbsolutePath}";
                else
                    url = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
                return true;
            }

            if (!TryParseSSH(URL, out var host, out var path))
                return false;

            // Azure DevOps uses a different path layout for SSH remotes.
            //   git@ssh.dev.azure.com:v3/{org}/{project}/{repo} => https://dev.azure.com/{org}/{project}/_git/{repo}
            //   {org}@vs-ssh.visualstudio.com:v3/{org}/{project}/{repo} => https://{org}.visualstudio.com/{project}/_git/{repo}
            if (path.StartsWith("v3/", StringComparison.Ordinal))
            {
                var parts = path.Split('/');
                if (parts.Length == 4)
                {
                    if (host.Equals("ssh.dev.azure.com", StringComparison.Ordinal))
                    {
                        url = $"https://dev.azure.com/{parts[1]}/{parts[2]}/_git/{parts[3]}";
                        return true;
                    }

                    if (host.Equals("vs-ssh.visualstudio.com", StringComparison.Ordinal))
                    {
                        url = $"https://{parts[1]}.visualstudio.com/{parts[2]}/_git/{parts[3]}";
                        return true;
                    }
                }
            }

            var supportHTTPS = HTTPSValidator.IsWellKnownHost(host) || HTTPSValidator.IsSupported(host);
            var scheme = supportHTTPS ? "https" : "http";
            url = $"{scheme}://{host}/{path}";
            return true;
        }

        public static bool TryGetSSHHost(string url, out string host)
        {
            return TryParseSSH(url, out host, out _);
        }

        private static bool TryParseSSH(string url, out string host, out string path)
        {
            host = null;
            path = null;

            if (string.IsNullOrEmpty(url))
                return false;

            // NOTE: The port of SSH remotes is not used, since the port of web page is unknown.
            var match = REG_TO_VISIT_URL_CAPTURE_SSH().Match(url);
            if (match.Success)
            {
                host = match.Groups[2].Value;
                path = match.Groups[4].Value;
                return true;
            }

            match = REG_TO_VISIT_URL_CAPTURE_SCP().Match(url);
            if (match.Success)
            {
                host = match.Groups[1].Value;
                path = match.Groups[2].Value;
                return true;
            }

            return false;
        }

        public bool TryGetCreatePullRequestURL(out string url, string mergeBranch)
        {
            url = null;

            if (!TryGetVisitURL(out var baseURL))
                return false;

            var uri = new Uri(baseURL);
            var host = uri.Host;
            var encodedBranch = HttpUtility.UrlEncode(mergeBranch);

            if (host.Contains("github.com", StringComparison.Ordinal))
            {
                url = $"{baseURL}/compare/{encodedBranch}?expand=1";
                return true;
            }

            if (host.Contains("gitlab", StringComparison.Ordinal))
            {
                url = $"{baseURL}/-/merge_requests/new?merge_request%5Bsource_branch%5D={encodedBranch}";
                return true;
            }

            if (host.Equals("gitee.com", StringComparison.Ordinal))
            {
                url = $"{baseURL}/pulls/new?source={encodedBranch}";
                return true;
            }

            if (host.Equals("bitbucket.org", StringComparison.Ordinal))
            {
                url = $"{baseURL}/pull-requests/new?source={encodedBranch}";
                return true;
            }

            if (host.Equals("gitea.org", StringComparison.Ordinal))
            {
                url = $"{baseURL}/compare/{encodedBranch}";
                return true;
            }

            if (host.Contains("azure.com", StringComparison.Ordinal) ||
                host.Contains("visualstudio.com", StringComparison.Ordinal))
            {
                url = $"{baseURL}/pullrequestcreate?sourceRef={encodedBranch}";
                return true;
            }

            return false;
        }
    }
}
