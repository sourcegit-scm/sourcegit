using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.UI {

    /// <summary>
    ///     Rebase mode.
    /// </summary>
    public enum InteractiveRebaseMode {
        Pick,
        Reword,
        Squash,
        Fixup,
        Drop,
    }

    /// <summary>
    ///     Rebase mode information to display in UI.
    /// </summary>
    public class InteractiveRebaseModeInfo {
        public InteractiveRebaseMode Mode { get; set; }
        public string Title { get; set; }
        public string Desc { get; set; }
        public Brush Theme { get; set; }

        public InteractiveRebaseModeInfo(InteractiveRebaseMode mode, string title, string desc, Brush brush) {
            Mode = mode;
            Title = title;
            Desc = desc;
            Theme = brush;
        }

        public static List<InteractiveRebaseModeInfo> Supported = new List<InteractiveRebaseModeInfo>() {
            new InteractiveRebaseModeInfo(InteractiveRebaseMode.Pick, "Pick", "Use this commit", Brushes.Green),
            new InteractiveRebaseModeInfo(InteractiveRebaseMode.Reword, "Reword", "Edit the commit message", Brushes.Yellow),
            new InteractiveRebaseModeInfo(InteractiveRebaseMode.Squash, "Squash", "Meld into previous commit", App.Preference.UIUseLightTheme ? Brushes.Gray : Brushes.White),
            new InteractiveRebaseModeInfo(InteractiveRebaseMode.Fixup, "Fixup", "Like 'Squash' but discard log message", App.Preference.UIUseLightTheme ? Brushes.Gray : Brushes.White),
            new InteractiveRebaseModeInfo(InteractiveRebaseMode.Drop, "Drop", "Remove commit", Brushes.Red),
        };
    }

    /// <summary>
    ///     Rebase item.
    /// </summary>
    public class InteractiveRebaseItem : INotifyPropertyChanged {
        private InteractiveRebaseMode mode = InteractiveRebaseMode.Pick;
        private bool isEditorOpened = false;
        private string editSubject = null;
        private string editMsg = null;

        public event PropertyChangedEventHandler PropertyChanged;

        public Git.Commit Commit { get; set; }

        public int Mode {
            get { return (int)mode; }
            set {
                if (value != (int)mode) {
                    mode = (InteractiveRebaseMode)value;
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs("Mode"));
                }
            }
        }

        public bool IsEditorOpened {
            get { return isEditorOpened; }
            set {
                if (value != isEditorOpened) {
                    isEditorOpened = value;
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs("IsEditorOpened"));
                }
            }
        }

        public string Subject {
            get { return Commit.Subject; }
            set {
                if (value != Commit.Subject) {
                    Commit.Subject = value;
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs("Subject"));
                }
            }
        }

        public string EditSubject {
            get { return editSubject; }
            set {
                if (value != editMsg) {
                    editSubject = value;
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs("EditSubject"));
                }
            }
        }

        public string EditMessage {
            get { return editMsg; }
            set {
                if (value != editMsg) {
                    editMsg = value;
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs("EditMessage"));
                }
            }
        }
    }

    /// <summary>
    ///     Interactive rebase panel.
    /// </summary>
    public partial class InteractiveRebase : Window {
        private Git.Repository repo = null;
        private string from = null;

        /// <summary>
        ///     Edit commit list.
        /// </summary>
        public ObservableCollection<InteractiveRebaseItem> Items {
            get;
            set;
        }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="start"></param>
        public InteractiveRebase(Git.Repository opened, Git.Commit start) {
            repo = opened;
            Items = new ObservableCollection<InteractiveRebaseItem>();
            from = start.ShortSHA;

            InitializeComponent();

            branch.Content = opened.CurrentBranch().Name;
            on.Content = $"{start.ShortSHA}  {start.Subject}";

            Task.Run(() => {
                var commits = repo.Commits($"{start.SHA}..HEAD");
                commits.Add(start);

                Dispatcher.Invoke(() => {
                    Items.Clear();
                    foreach (var c in commits) Items.Add(new InteractiveRebaseItem() { Commit = c });
                });
            });
        }

        #region WINDOW_COMMANDS
        private void LogoMouseButtonDown(object sender, MouseButtonEventArgs e) {
            var element = e.OriginalSource as FrameworkElement;
            if (element == null) return;

            var pos = PointToScreen(new Point(0, 33));
            SystemCommands.ShowSystemMenu(this, pos);
        }

        private void Minimize(object sender, RoutedEventArgs e) {
            SystemCommands.MinimizeWindow(this);
        }

        private void MaximizeOrRestore(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Normal) {
                SystemCommands.MaximizeWindow(this);
            } else {
                SystemCommands.RestoreWindow(this);
            }
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }
        #endregion

        private void CommitSelectionChanged(object sender, SelectionChangedEventArgs e) {
            foreach (var obj in e.RemovedItems) {
                var item = obj as InteractiveRebaseItem;
                if (item != null) item.IsEditorOpened = false;
            }

            if (e.AddedItems.Count == 1) {
                var item = e.AddedItems[0] as InteractiveRebaseItem;
                if (item != null) commitViewer.SetData(repo, item.Commit);
            }
        }

        private void PopupMessageEditor(object sender, MouseButtonEventArgs e) {
            var item = (sender as Control).DataContext as InteractiveRebaseItem;
            if (item == null) return;

            item.EditSubject = item.Commit.Subject;
            item.EditMessage = item.Commit.Message;
            item.IsEditorOpened = true;
        }

        private void HideMessageEditor(object sender, RoutedEventArgs e) {
            var item = (sender as Button).DataContext as InteractiveRebaseItem;
            if (item == null) return;
            item.IsEditorOpened = false;
        }

        private void ApplyMessageEdit(object sender, RoutedEventArgs e) {
            var item = (sender as Button).DataContext as InteractiveRebaseItem;
            if (item == null) return;

            item.Subject = item.EditSubject;
            item.Commit.Message = item.EditMessage;
            item.Mode = (int)InteractiveRebaseMode.Reword;
            item.IsEditorOpened = false;
        }

        private void CommitMessageChanged(object sender, TextChangedEventArgs e) {
            (sender as TextBox).ScrollToEnd();
        }

        private void MoveUp(object sender, RoutedEventArgs e) {
            var item = (sender as Button).DataContext as InteractiveRebaseItem;
            if (item == null) return;

            var idx = -1;
            for (int i = 0; i < Items.Count; i++) {
                if (Items[i].Commit.SHA == item.Commit.SHA) {
                    idx = i;
                    break;
                }
            }

            if (idx > 0) {
                Items.RemoveAt(idx);
                Items.Insert(idx - 1, item);
            }
        }

        private void MoveDown(object sender, RoutedEventArgs e) {
            var item = (sender as Button).DataContext as InteractiveRebaseItem;
            if (item == null) return;

            var idx = -1;
            for (int i = 0; i < Items.Count; i++) {
                if (Items[i].Commit.SHA == item.Commit.SHA) {
                    idx = i;
                    break;
                }
            }

            if (idx < Items.Count - 1) {
                Items.RemoveAt(idx);
                Items.Insert(idx + 1, item);
            }
        }

        private void Start(object sender, RoutedEventArgs e) {
            var stream = new FileStream(App.InteractiveRebaseTodo, FileMode.Create);
            var writer = new StreamWriter(stream);

            for (int i = Items.Count - 1; i >= 0; i--) {
                var item = Items[i];

                switch ((InteractiveRebaseMode)item.Mode) {
                case InteractiveRebaseMode.Pick:
                    writer.WriteLine($"pick {item.Commit.ShortSHA} {item.Subject}");
                    break;
                case InteractiveRebaseMode.Reword:
                    writer.WriteLine($"reword {item.Commit.ShortSHA} {item.Subject}");
                    break;
                case InteractiveRebaseMode.Squash:
                    writer.WriteLine($"squash {item.Commit.ShortSHA} {item.Subject}");
                    break;
                case InteractiveRebaseMode.Fixup:
                    writer.WriteLine($"fixup {item.Commit.ShortSHA} {item.Subject}");
                    break;
                case InteractiveRebaseMode.Drop:
                    writer.WriteLine($"drop {item.Commit.ShortSHA} {item.Subject}");
                    break;
                }
            }

            writer.Flush();
            stream.Flush();
            writer.Close();
            stream.Close();

            repo.SetWatcherEnabled(false);
            var errs = repo.RunCommand($"-c sequence.editor=\"\\\"{App.InteractiveRebaseScript}\\\"\" rebase -i {from}^", null);
            repo.AssertCommand(errs);

            Close();
        }

        private void Cancel(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
