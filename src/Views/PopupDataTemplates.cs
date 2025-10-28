using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace SourceGit.Views
{
    public class PopupDataTemplates : IDataTemplate
    {
        public Control Build(object param) => App.CreateViewForViewModel(param);
        public bool Match(object data) => data is ViewModels.Popup;
    }
}
