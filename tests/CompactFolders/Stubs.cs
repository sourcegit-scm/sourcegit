// UI notification infrastructure only; tree construction and compaction are production code.
namespace CommunityToolkit.Mvvm.ComponentModel
{
    public class ObservableObject
    {
        protected bool SetProperty<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            return true;
        }

        public event Action<string> PropertyChangedForTest;

        protected void OnPropertyChanged(string name) => PropertyChangedForTest?.Invoke(name);
    }
}
