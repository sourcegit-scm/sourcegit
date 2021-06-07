using System.Threading.Tasks;
using System.Windows.Controls;

namespace SourceGit.Views.Popups {
    /// <summary>
    ///     编辑子树
    /// </summary>
    public partial class EditSubTree : Controls.PopupWidget {
        private Models.Repository repo;
        private Models.SubTree subtree;

        public string Source {
            get { return subtree.Remote; }
            set { subtree.Remote = value; }
        }

        public EditSubTree(Models.Repository repo, string prefix) {
            this.repo = repo;
            this.subtree = repo.SubTrees.Find(x => x.Prefix == prefix);
            InitializeComponent();
            txtPrefix.Text = prefix;
        }

        public override string GetTitle() {
            return App.Text("EditSubTree");
        }

        public override Task<bool> Start() {
            txtSource.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            if (Validation.GetHasError(txtSource)) return null;
            return Task.Run(() => true);
        }
    }
}
