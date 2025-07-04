using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class ViewLogs : ObservableObject
    {
        public AvaloniaList<CommandLog> Logs
        {
            get => _repo.Logs;
        }

        public CommandLog SelectedLog
        {
            get => _selectedLog;
            set => SetProperty(ref _selectedLog, value);
        }

        public ViewLogs(Repository repo)
        {
            _repo = repo;
        }

        public void ClearAll()
        {
            SelectedLog = null;
            Logs.Clear();
        }

        private readonly Repository _repo;
        private CommandLog _selectedLog;
    }
}
