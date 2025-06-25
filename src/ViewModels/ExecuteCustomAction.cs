using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public interface ICustomActionControlParameter
    {
        string GetValue();
    }

    public class CustomActionControlTextBox : ICustomActionControlParameter
    {
        public string Label { get; set; } = string.Empty;
        public string Placeholder { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        public CustomActionControlTextBox(string label, string placeholder, string defaultValue)
        {
            Label = label;
            Placeholder = placeholder;
            Text = defaultValue;
        }

        public string GetValue() => Text;
    }

    public class CustomActionControlPathSelector : ObservableObject, ICustomActionControlParameter
    {
        public string Label { get; set; } = string.Empty;
        public string Placeholder { get; set; } = string.Empty;
        public bool IsFolder { get; set; } = false;

        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        public CustomActionControlPathSelector(string label, string placeholder, bool isFolder, string defaultValue)
        {
            Label = label;
            Placeholder = placeholder;
            IsFolder = isFolder;
            _path = defaultValue;
        }

        public string GetValue() => _path;

        private string _path = string.Empty;
    }

    public class CustomActionControlCheckBox : ICustomActionControlParameter
    {
        public string Label { get; set; } = string.Empty;
        public string ToolTip { get; set; } = string.Empty;
        public string CheckedValue { get; set; } = string.Empty;
        public bool IsChecked { get; set; }

        public CustomActionControlCheckBox(string label, string tooltip, string checkedValue, bool isChecked)
        {
            Label = label;
            ToolTip = string.IsNullOrEmpty(tooltip) ? null : tooltip;
            CheckedValue = checkedValue;
            IsChecked = isChecked;
        }

        public string GetValue() => IsChecked ? CheckedValue : string.Empty;
    }

    public class ExecuteCustomAction : Popup
    {
        public Models.CustomAction CustomAction
        {
            get;
        }

        public List<ICustomActionControlParameter> ControlParameters
        {
            get;
        } = [];

        public bool IsSimpleMode
        {
            get => ControlParameters.Count == 0;
        }

        public ExecuteCustomAction(Repository repo, Models.CustomAction action)
        {
            _repo = repo;
            _commandline = action.Arguments.Replace("${REPO}", GetWorkdir());
            CustomAction = action;
            PrepareControlParameters();
        }

        public ExecuteCustomAction(Repository repo, Models.CustomAction action, Models.Branch branch)
        {
            _repo = repo;
            _commandline = action.Arguments.Replace("${REPO}", GetWorkdir()).Replace("${BRANCH}", branch.FriendlyName);
            CustomAction = action;
            PrepareControlParameters();
        }

        public ExecuteCustomAction(Repository repo, Models.CustomAction action, Models.Commit commit)
        {
            _repo = repo;
            _commandline = action.Arguments.Replace("${REPO}", GetWorkdir()).Replace("${SHA}", commit.SHA);
            CustomAction = action;
            PrepareControlParameters();
        }

        public override Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Run custom action ...";

            var cmdline = _commandline;
            for (var i = 0; i < ControlParameters.Count; i++)
            {
                var param = ControlParameters[i];
                cmdline = cmdline.Replace($"${i}", param.GetValue());
            }

            var log = _repo.CreateLog(CustomAction.Name);
            Use(log);

            return Task.Run(() =>
            {
                if (CustomAction.WaitForExit)
                    Commands.ExecuteCustomAction.RunAndWait(_repo.FullPath, CustomAction.Executable, cmdline, log);
                else
                    Commands.ExecuteCustomAction.Run(_repo.FullPath, CustomAction.Executable, cmdline);

                log.Complete();
                CallUIThread(() => _repo.SetWatcherEnabled(true));
                return true;
            });
        }

        private void PrepareControlParameters()
        {
            foreach (var ctl in CustomAction.Controls)
            {
                switch (ctl.Type)
                {
                    case Models.CustomActionControlType.TextBox:
                        ControlParameters.Add(new CustomActionControlTextBox(ctl.Label, ctl.Description, ctl.StringValue));
                        break;
                    case Models.CustomActionControlType.CheckBox:
                        ControlParameters.Add(new CustomActionControlCheckBox(ctl.Label, ctl.Description, ctl.StringValue, ctl.BoolValue));
                        break;
                    case Models.CustomActionControlType.PathSelector:
                        ControlParameters.Add(new CustomActionControlPathSelector(ctl.Label, ctl.Description, ctl.BoolValue, ctl.StringValue));
                        break;
                }
            }
        }

        private string GetWorkdir()
        {
            return OperatingSystem.IsWindows() ? _repo.FullPath.Replace("/", "\\") : _repo.FullPath;
        }

        private readonly Repository _repo = null;
        private readonly string _commandline = string.Empty;
    }
}
