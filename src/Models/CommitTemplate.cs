using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public partial class CommitTemplate : ObservableObject
    {
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public string Apply(Branch branch, List<Change> changes)
        {
            var te = new TemplateEngine();
            return te.Eval(_content, branch, changes);
        }

        private string _name = string.Empty;
        private string _content = string.Empty;
    }
}
