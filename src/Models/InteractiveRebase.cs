using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SourceGit.Models
{
    public enum InteractiveRebaseAction
    {
        Pick,
        Edit,
        Reword,
        Squash,
        Fixup,
        Drop,
    }

    public enum InteractiveRebasePendingType
    {
        None = 0,
        Target,
        Pending,
        Ignore,
        Last,
    }

    public class InteractiveCommit
    {
        public Commit Commit { get; set; } = new Commit();
        public string Message { get; set; } = string.Empty;
    }

    public class InteractiveRebaseJob
    {
        public string SHA { get; set; } = string.Empty;
        public InteractiveRebaseAction Action { get; set; } = InteractiveRebaseAction.Pick;
        public string Message { get; set; } = string.Empty;
    }

    public partial class InteractiveRebaseJobCollection
    {
        public string OrigHead { get; set; } = string.Empty;
        public string Onto { get; set; } = string.Empty;
        public List<InteractiveRebaseJob> Jobs { get; set; } = new List<InteractiveRebaseJob>();

        public void WriteTodoList(string todoFile)
        {
            using var writer = new StreamWriter(todoFile);
            foreach (var job in Jobs)
            {
                var code = job.Action switch
                {
                    InteractiveRebaseAction.Pick => 'p',
                    InteractiveRebaseAction.Edit => 'e',
                    InteractiveRebaseAction.Reword => 'r',
                    InteractiveRebaseAction.Squash => 's',
                    InteractiveRebaseAction.Fixup => 'f',
                    _ => 'd'
                };
                writer.WriteLine($"{code} {job.SHA}");
            }

            writer.Flush();
        }

        public void WriteCommitMessage(string doneFile, string msgFile)
        {
            var done = File.ReadAllText(doneFile).Trim().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (done.Length == 0)
                return;

            var current = done[^1].Trim();
            var match = REG_REBASE_TODO().Match(current);
            if (!match.Success)
                return;

            var sha = match.Groups[1].Value;
            foreach (var job in Jobs)
            {
                if (job.SHA.StartsWith(sha))
                {
                    File.WriteAllText(msgFile, job.Message);
                    return;
                }
            }
        }

        [GeneratedRegex(@"^[a-z]+\s+([a-fA-F0-9]{4,64})(\s+.*)?$")]
        private static partial Regex REG_REBASE_TODO();
    }
}
