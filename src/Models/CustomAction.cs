using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public enum CustomActionScope
    {
        Repository,
        Commit,
        Branch,
        Tag,
        Remote,
        File,
    }

    public enum CustomActionControlType
    {
        TextBox = 0,
        PathSelector,
        CheckBox,
        ComboBox,
    }

    public record CustomActionTargetFile(string File, Commit Revision);

    public class CustomActionControl : ObservableObject
    {
        public CustomActionControl()
        {
        }

        public CustomActionControl(CustomActionControl cac)
        {
            if (cac != null)
            {
                Type = cac.Type;
                Description = cac.Description;
                Label = cac.Label;
                Description = cac.Description;
                StringValue = cac.StringValue;
                BoolValue = cac.BoolValue;
            }
        }

        public CustomActionControlType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string StringValue
        {
            get => _stringValue;
            set => SetProperty(ref _stringValue, value);
        }

        public bool BoolValue
        {
            get => _boolValue;
            set => SetProperty(ref _boolValue, value);
        }

        private CustomActionControlType _type = CustomActionControlType.TextBox;
        private string _label = string.Empty;
        private string _description = string.Empty;
        private string _stringValue = string.Empty;
        private bool _boolValue = false;
    }

    public class CustomAction : ObservableObject
    {
        public CustomAction()
        {
        }

        public CustomAction(CustomAction action)
        {
            if (action != null)
            {
                Name = action.Name;
                Scope = action.Scope;
                Executable = action.Executable;
                Arguments = action.Arguments;
                WaitForExit = action.WaitForExit;
                foreach (var control in action.Controls)
                    Controls.Add(new CustomActionControl(control));
            }
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public CustomActionScope Scope
        {
            get => _scope;
            set => SetProperty(ref _scope, value);
        }

        public string Executable
        {
            get => _executable;
            set => SetProperty(ref _executable, value);
        }

        public string Arguments
        {
            get => _arguments;
            set => SetProperty(ref _arguments, value);
        }

        public AvaloniaList<CustomActionControl> Controls
        {
            get;
            set;
        } = [];

        public bool WaitForExit
        {
            get => _waitForExit;
            set => SetProperty(ref _waitForExit, value);
        }

        private string _name = string.Empty;
        private CustomActionScope _scope = CustomActionScope.Repository;
        private string _executable = string.Empty;
        private string _arguments = string.Empty;
        private bool _waitForExit = true;
    }
}
