using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace SourceGit.Views
{
    public class CommandPaletteDataTemplates : IDataTemplate
    {
        public Control Build(object param) => ControlExtensions.CreateFromViewModels(param);
        public bool Match(object data) => data is ViewModels.ICommandPalette;
    }
}
