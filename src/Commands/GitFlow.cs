using System;
using System.Collections.Generic;
using System.Text;

using Avalonia.Threading;

namespace SourceGit.Commands
{
    public static class GitFlow
    {
        public class BranchDetectResult
        {
            public bool IsGitFlowBranch { get; set; } = false;
            public string Type { get; set; } = string.Empty;
            public string Prefix { get; set; } = string.Empty;
        }

        public static bool IsEnabled(string repo, List<Models.Branch> branches)
        {
            var localBrancheNames = new HashSet<string>();
            foreach (var branch in branches)
            {
                if (branch.IsLocal)
                    localBrancheNames.Add(branch.Name);
            }

            var config = new Config(repo).ListAll();
            if (!config.TryGetValue("gitflow.branch.master", out string master) || !localBrancheNames.Contains(master))
                return false;

            if (!config.TryGetValue("gitflow.branch.develop", out string develop) || !localBrancheNames.Contains(develop))
                return false;

            return config.ContainsKey("gitflow.prefix.feature") &&
                config.ContainsKey("gitflow.prefix.release") &&
                config.ContainsKey("gitflow.prefix.hotfix");
        }

        public static bool Init(string repo, List<Models.Branch> branches, string master, string develop, string feature, string release, string hotfix, string version, Models.ICommandLog log)
        {
            var current = branches.Find(x => x.IsCurrent);

            var masterBranch = branches.Find(x => x.Name == master);
            if (masterBranch == null && current != null)
                Branch.Create(repo, master, current.Head, log);

            var devBranch = branches.Find(x => x.Name == develop);
            if (devBranch == null && current != null)
                Branch.Create(repo, develop, current.Head, log);

            var config = new Config(repo);
            config.Set("gitflow.branch.master", master);
            config.Set("gitflow.branch.develop", develop);
            config.Set("gitflow.prefix.feature", feature);
            config.Set("gitflow.prefix.bugfix", "bugfix/");
            config.Set("gitflow.prefix.release", release);
            config.Set("gitflow.prefix.hotfix", hotfix);
            config.Set("gitflow.prefix.support", "support/");
            config.Set("gitflow.prefix.versiontag", version, true);

            var init = new Command();
            init.WorkingDirectory = repo;
            init.Context = repo;
            init.Args = "flow init -d";
            init.Log = log;
            return init.Exec();
        }

        public static string GetPrefix(string repo, string type)
        {
            return new Config(repo).Get($"gitflow.prefix.{type}");
        }

        public static BranchDetectResult DetectType(string repo, List<Models.Branch> branches, string branch)
        {
            var rs = new BranchDetectResult();
            var localBrancheNames = new HashSet<string>();
            foreach (var b in branches)
            {
                if (b.IsLocal)
                    localBrancheNames.Add(b.Name);
            }

            var config = new Config(repo).ListAll();
            if (!config.TryGetValue("gitflow.branch.master", out string master) || !localBrancheNames.Contains(master))
                return rs;

            if (!config.TryGetValue("gitflow.branch.develop", out string develop) || !localBrancheNames.Contains(develop))
                return rs;

            if (!config.TryGetValue("gitflow.prefix.feature", out var feature) ||
                !config.TryGetValue("gitflow.prefix.release", out var release) ||
                !config.TryGetValue("gitflow.prefix.hotfix", out var hotfix))
                return rs;

            if (branch.StartsWith(feature, StringComparison.Ordinal))
            {
                rs.IsGitFlowBranch = true;
                rs.Type = "feature";
                rs.Prefix = feature;
            }
            else if (branch.StartsWith(release, StringComparison.Ordinal))
            {
                rs.IsGitFlowBranch = true;
                rs.Type = "release";
                rs.Prefix = release;
            }
            else if (branch.StartsWith(hotfix, StringComparison.Ordinal))
            {
                rs.IsGitFlowBranch = true;
                rs.Type = "hotfix";
                rs.Prefix = hotfix;
            }

            return rs;
        }

        public static bool Start(string repo, string type, string name, Models.ICommandLog log)
        {
            if (!SUPPORTED_BRANCH_TYPES.Contains(type))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    App.RaiseException(repo, "Bad branch type!!!");
                });

                return false;
            }

            var start = new Command();
            start.WorkingDirectory = repo;
            start.Context = repo;
            start.Args = $"flow {type} start {name}";
            start.Log = log;
            return start.Exec();
        }

        public static bool Finish(string repo, string type, string name, bool squash, bool push, bool keepBranch, Models.ICommandLog log)
        {
            if (!SUPPORTED_BRANCH_TYPES.Contains(type))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    App.RaiseException(repo, "Bad branch type!!!");
                });

                return false;
            }

            var builder = new StringBuilder();
            builder.Append("flow ");
            builder.Append(type);
            builder.Append(" finish ");
            if (squash)
                builder.Append("--squash ");
            if (push)
                builder.Append("--push ");
            if (keepBranch)
                builder.Append("-k ");
            builder.Append(name);

            var finish = new Command();
            finish.WorkingDirectory = repo;
            finish.Context = repo;
            finish.Args = builder.ToString();
            finish.Log = log;
            return finish.Exec();
        }

        private static readonly List<string> SUPPORTED_BRANCH_TYPES = new List<string>()
        {
            "feature",
            "release",
            "bugfix",
            "hotfix",
            "support",
        };
    }
}
