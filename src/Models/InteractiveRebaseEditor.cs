using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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

    public class InteractiveRebaseJob
    {
        public string SHA { get; set; } = string.Empty;
        public InteractiveRebaseAction Action { get; set; } = InteractiveRebaseAction.Pick;
        public string Message { get; set; } = string.Empty;
    }

    public static class InteractiveRebaseEditor
    {
        public static int Process(string file)
        {
            try
            {
                var filename = Path.GetFileName(file);
                if (filename.Equals("git-rebase-todo", StringComparison.OrdinalIgnoreCase))
                {
                    var dirInfo = new DirectoryInfo(Path.GetDirectoryName(file));
                    if (!dirInfo.Exists || !dirInfo.Name.Equals("rebase-merge", StringComparison.Ordinal))
                        return -1;

                    var jobsFile = Path.Combine(dirInfo.Parent.FullName, "sourcegit_rebase_jobs.json");
                    if (!File.Exists(jobsFile))
                        return -1;

                    var jobs = JsonSerializer.Deserialize(File.ReadAllText(jobsFile), JsonCodeGen.Default.ListInteractiveRebaseJob);
                    var lines = new List<string>();
                    foreach (var job in jobs)
                    {
                        switch (job.Action)
                        {
                            case InteractiveRebaseAction.Pick:
                                lines.Add($"p {job.SHA}");
                                break;
                            case InteractiveRebaseAction.Edit:
                                lines.Add($"e {job.SHA}");
                                break;
                            case InteractiveRebaseAction.Reword:
                                lines.Add($"r {job.SHA}");
                                break;
                            case InteractiveRebaseAction.Squash:
                                lines.Add($"s {job.SHA}");
                                break;
                            case InteractiveRebaseAction.Fixup:
                                lines.Add($"f {job.SHA}");
                                break;
                            default:
                                lines.Add($"d {job.SHA}");
                                break;
                        }
                    }

                    File.WriteAllLines(file, lines);
                } 
                else if (filename.Equals("COMMIT_EDITMSG", StringComparison.OrdinalIgnoreCase))
                {
                    var jobsFile = Path.Combine(Path.GetDirectoryName(file), "sourcegit_rebase_jobs.json");
                    if (!File.Exists(jobsFile))
                        return 0;

                    var jobs = JsonSerializer.Deserialize(File.ReadAllText(jobsFile), JsonCodeGen.Default.ListInteractiveRebaseJob);
                    var doneFile = Path.Combine(Path.GetDirectoryName(file), "rebase-merge", "done");
                    if (!File.Exists(doneFile))
                        return -1;

                    var done = File.ReadAllText(doneFile).Split(new char[] {'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);
                    if (done.Length > jobs.Count)
                        return -1;

                    var job = jobs[done.Length - 1];
                    File.WriteAllText(file, job.Message);
                }

                return 0;
            }
            catch
            {
                return -1;
            }
        }
    }
}
