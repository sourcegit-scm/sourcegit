using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SourceGit.Views.Widgets {

    /// <summary>
    ///     主界面标题栏中的页面标签
    /// </summary>
    public partial class PageTabItem : UserControl {
        public string Title { get; private set; }
        public bool IsWelcomePage { get; private set; }
        public int Bookmark { get; private set; }
        public string Tip { get; private set; }

        public PageTabItem(string title, bool isWelcomePage, int bookmark, string tip) {
            Title = title;
            IsWelcomePage = isWelcomePage;
            Bookmark = bookmark;
            Tip = tip;

            InitializeComponent();
        }

        private void OnContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            if (IsWelcomePage) return;

            var refresh = new MenuItem();
            refresh.Header = App.Text("RepoCM.Refresh");
            refresh.Click += (o, e) => {
                Models.Watcher.Get(Tip)?.Refresh();
                e.Handled = true;
            };

            var iconBookmark = FindResource("Icon.Bookmark") as Geometry;
            var bookmark = new MenuItem();
            bookmark.Header = App.Text("RepoCM.Bookmark");
            for (int i = 0; i < Controls.Bookmark.COLORS.Length; i++) {
                var icon = new System.Windows.Shapes.Path();
                icon.Data = iconBookmark;
                icon.Fill = Controls.Bookmark.COLORS[i];
                icon.Width = 8;

                var mark = new MenuItem();
                mark.Icon = icon;
                mark.Header = $"{i}";

                var refIdx = i;
                mark.Click += (o, e) => {
                    var repo = Models.Preference.Instance.FindRepository(Tip);
                    if (repo == null) return;

                    repo.Bookmark = refIdx;
                    Bookmark = refIdx;
                    ctrlBookmark.GetBindingExpression(Controls.Bookmark.ColorProperty).UpdateTarget();
                    e.Handled = true;
                };

                bookmark.Items.Add(mark);
            }

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("RepoCM.CopyPath");
            copyPath.Click += (o, e) => {
                Clipboard.SetText(Tip);
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(refresh);
            menu.Items.Add(bookmark);
            menu.Items.Add(copyPath);
            menu.IsOpen = true;

            ev.Handled = true;
        }
    }
}
