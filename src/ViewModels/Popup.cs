using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Popup : ObservableValidator, Models.ICommandLogReceiver
    {
        public bool InProgress
        {
            get => _inProgress;
            set => SetProperty(ref _inProgress, value);
        }

        public string ProgressDescription
        {
            get => _progressDescription;
            set => SetProperty(ref _progressDescription, value);
        }

        [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026:RequiresUnreferencedCode")]
        public bool Check()
        {
            if (HasErrors)
                return false;
            ValidateAllProperties();
            return !HasErrors;
        }

        public void OnReceiveCommandLog(string data)
        {
            var desc = data.Trim();
            if (!string.IsNullOrEmpty(desc))
                ProgressDescription = desc;
        }

        public void Cleanup()
        {
            _log?.Unsubscribe(this);
        }

        public virtual bool CanStartDirectly()
        {
            return true;
        }

        public virtual Task<bool> Sure()
        {
            return null;
        }

        protected void Use(CommandLog log)
        {
            _log = log;
            _log.Subscribe(this);
        }

        private bool _inProgress = false;
        private string _progressDescription = string.Empty;
        private CommandLog _log = null;
    }
}
