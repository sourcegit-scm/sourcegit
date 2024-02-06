using Avalonia.Media;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels {
    public class ResetMode {
        public string Name { get; set; }
        public string Desc { get; set; }
        public string Arg { get; set; }
        public IBrush Color { get; set; }

        public ResetMode(string n, string d, string a, IBrush b) {
            Name = n;
            Desc = d;
            Arg = a;
            Color = b;
        }
    }

    public class Reset : Popup {
        public Models.Branch Current {
            get;
            private set;
        }

        public Models.Commit To {
            get;
            private set;
        }

        public List<ResetMode> Modes {
            get;
            private set;
        }

        public ResetMode SelectedMode {
            get;
            set;
        }

        public Reset(Repository repo, Models.Branch current, Models.Commit to) {
            _repo = repo;
            Current = current;
            To = to;
            Modes = new List<ResetMode>() {
                new ResetMode("Soft", "Keep all changes. Stage differences", "--soft", Brushes.Green),
                new ResetMode("Mixed", "Keep all changes. Unstage differences", "--mixed", Brushes.Orange),
                new ResetMode("Hard", "Discard all changes", "--hard", Brushes.Red),
            };
            SelectedMode = Modes[0];
            View = new Views.Reset() { DataContext = this };
        }

        public override Task<bool> Sure() {
            _repo.SetWatcherEnabled(false);
            return Task.Run(() => {
                SetProgressDescription($"Reset current branch to {To.SHA} ...");
                var succ = new Commands.Reset(_repo.FullPath, To.SHA, SelectedMode.Arg).Exec();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return succ;
            });
        }

        private Repository _repo = null;
    }
}
