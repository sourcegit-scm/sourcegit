using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class PopupDataTemplates : IDataTemplate
    {
        public bool Match(object data)
        {
            return data is ViewModels.Popup;
        }

        public Control Build(object param)
        {
            var control = ControlExtensions.CreateFromViewModels(param);

            control.Loaded += (o, e) =>
            {
                if (o is not Control ctl)
                    return;

                var inputs = ctl.GetVisualDescendants();
                foreach (var input in inputs)
                {
                    if (input is SelectableTextBlock)
                        continue;

                    if (input is InputElement { Focusable: true, IsEffectivelyEnabled: true } focusable)
                    {
                        focusable.Focus(NavigationMethod.Directional);
                        if (input is TextBox box)
                            box.CaretIndex = box.CaretIndex = box.Text?.Length ?? 0;
                        return;
                    }
                }

                // Although this control may not be focusable, we only want to change the window's previous focus after the call.
                ctl.Focus(NavigationMethod.Directional);
            };

            return control;
        }
    }
}
