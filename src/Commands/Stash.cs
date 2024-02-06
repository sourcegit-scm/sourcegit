using System.Collections.Generic;
using System.IO;

namespace SourceGit.Commands {
    public class Stash : Command {
        public Stash(string repo) {
            WorkingDirectory = repo;
            Context = repo;
        }

        public bool Push(string message) {
            Args = $"stash push -m \"{message}\"";
            return Exec();
        }

        public bool Push(List<Models.Change> changes, string message) {
            var temp = Path.GetTempFileName();
            var stream = new FileStream(temp, FileMode.Create);
            var writer = new StreamWriter(stream);

            var needAdd = new List<Models.Change>();
            foreach (var c in changes) {
                writer.WriteLine(c.Path);

                if (c.WorkTree == Models.ChangeState.Added || c.WorkTree == Models.ChangeState.Untracked) {
                    needAdd.Add(c);
                    if (needAdd.Count > 10) {
                        new Add(WorkingDirectory, needAdd).Exec();
                        needAdd.Clear();
                    }
                }
            }
            if (needAdd.Count > 0) {
                new Add(WorkingDirectory, needAdd).Exec();
                needAdd.Clear();
            }

            writer.Flush();
            stream.Flush();
            writer.Close();
            stream.Close();

            Args = $"stash push -m \"{message}\" --pathspec-from-file=\"{temp}\"";
            var succ = Exec();
            File.Delete(temp);
            return succ;
        }

        public bool Apply(string name) {
            Args = $"stash apply -q {name}";
            return Exec();
        }

        public bool Pop(string name) {
            Args = $"stash pop -q {name}";
            return Exec();
        }

        public bool Drop(string name) {
            Args = $"stash drop -q {name}";
            return Exec();
        }

        public bool Clear() {
            Args = "stash clear";
            return Exec();
        }
    }
}
