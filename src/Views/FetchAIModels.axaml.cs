using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public class FetchAIModelItem : INotifyPropertyChanged
    {
        public string Name { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        public FetchAIModelItem(string name, bool isChecked)
        {
            Name = name;
            _isChecked = isChecked;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private bool _isChecked;
    }

    public partial class FetchAIModels : ChromelessWindow
    {
        public List<string> SelectedModels { get; private set; } = [];

        public List<string> ServerModels { get; private set; } = [];

        public FetchAIModels()
        {
            InitializeComponent();
        }

        public async void LoadModels(AI.Service service)
        {
            try
            {
                var models = await Task.Run(() => service.FetchModelsFromServer());
                var existing = new HashSet<string>(service.AvailableModels);
                ServerModels = models;

                var items = new List<FetchAIModelItem>();
                foreach (var model in models)
                    items.Add(new FetchAIModelItem(model, existing.Contains(model)));

                ModelListBox.ItemsSource = items;

                LoadingPanel.IsVisible = false;
                ModelListBorder.IsVisible = true;
            }
            catch (Exception ex)
            {
                LoadingPanel.IsVisible = false;
                ErrorMessage.Text = ex.Message;
                ErrorMessage.IsVisible = true;
            }
        }

        private void OnAdd(object _1, RoutedEventArgs _2)
        {
            SelectedModels = [];
            if (ModelListBox.ItemsSource is IEnumerable<FetchAIModelItem> items)
            {
                foreach (var item in items)
                {
                    if (item.IsChecked)
                        SelectedModels.Add(item.Name);
                }
            }
            Close(true);
        }

        private void OnCancel(object _1, RoutedEventArgs _2)
        {
            Close(false);
        }
    }
}
