using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class SelfUpdate : ObservableObject
    {
        public object Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        private object _data = null;
    }
}