using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     用于方便切换子页面的组件
    /// </summary>
    public class PageContainer : Grid {
        private Dictionary<string, UIElement> pages;
        private string front;

        public PageContainer() {
            pages = new Dictionary<string, UIElement>();
            front = null;

            Loaded += OnLoaded;
        }

        public void Add(string id, UIElement view) {
            view.Visibility = Visibility.Collapsed;
            pages.Add(id, view);
            Children.Add(view);
        }

        public UIElement Get(string id) {
            if (pages.ContainsKey(id)) return pages[id];
            return null;
        }

        public void Goto(string id) {
            if (!pages.ContainsKey(id)) return;

            if (!string.IsNullOrEmpty(front)) {
                if (front == id) return;
                pages[front].Visibility = Visibility.Collapsed;
            }

            front = id;
            pages[front].Visibility = Visibility.Visible;
        }

        public void Remove(string id) {
            if (!pages.ContainsKey(id)) return;
            if (front == id) front = null;
            Children.Remove(pages[id]);
            pages.Remove(id);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            foreach (var child in Children) {
                var elem = child as UIElement;
                var id = elem.Uid;
                if (string.IsNullOrEmpty(id)) continue;

                pages.Add(id, elem);
                front = id;
            }

            if (!string.IsNullOrEmpty(front)) {
                pages[front].Visibility = Visibility.Visible;
            }
        }
    }
}
