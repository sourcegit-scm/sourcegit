using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Stash : Command
    {
        public Stash(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public bool Push(string message)
        {
            Args = $"stash push -m \"{message}\"";
            return Exec();
        }

        public bool Push(List<Models.Change> changes, string message, bool onlyStaged, bool keepIndex)
        {
            var pathsBuilder = new StringBuilder();

            var indexOpts = keepIndex ? "--keep-index" : "";

            if (onlyStaged)
            {
                foreach (var c in changes)
                    pathsBuilder.Append($"\"{c.Path}\" ");

                var paths = pathsBuilder.ToString();
                Args = $"stash push {indexOpts} --staged -m \"{message}\" -- {paths}";
            }
            else
            {
                var needAdd = new List<Models.Change>();
                foreach (var c in changes)
                {
                    pathsBuilder.Append($"\"{c.Path}\" ");

                    if (c.WorkTree == Models.ChangeState.Added || c.WorkTree == Models.ChangeState.Untracked)
                    {
                        needAdd.Add(c);
                        if (needAdd.Count > 10)
                        {
                            new Add(WorkingDirectory, needAdd).Exec();
                            needAdd.Clear();
                        }
                    }
                }
                if (needAdd.Count > 0)
                {
                    new Add(WorkingDirectory, needAdd).Exec();
                    needAdd.Clear();
                }

                var paths = pathsBuilder.ToString();
                Args = $"stash push {indexOpts} -m \"{message}\" -- {paths}";
            }

            return Exec();
        }

        public bool Apply(string name)
        {
            Args = $"stash apply -q {name}";
            return Exec();
        }

        public bool Pop(string name)
        {
            Args = $"stash pop -q {name}";
            return Exec();
        }

        public bool Drop(string name)
        {
            Args = $"stash drop -q {name}";
            return Exec();
        }

        public bool Clear()
        {
            Args = "stash clear";
            return Exec();
        }
    }
}
