using System;
using System.Windows;

namespace SourceGit.Models {
    /// <summary>
    ///     主题
    /// </summary>
    public static class Theme {
        /// <summary>
        ///     主题切换事件
        /// </summary>
        public static event Action Changed;

        /// <summary>
        ///     启用主题变化监听
        /// </summary>
        /// <param name="elem"></param>
        public static void AddListener(FrameworkElement elem, Action callback) {
            elem.Loaded += (_, __) => Changed += callback;
            elem.Unloaded += (_, __) => Changed -= callback;
        }

        /// <summary>
        ///     切换主题
        /// </summary>
        public static void Change() {
            var theme = Preference.Instance.General.UseDarkTheme ? "Dark" : "Light";
            foreach (var rs in App.Current.Resources.MergedDictionaries) {
                if (rs.Source != null && rs.Source.OriginalString.StartsWith("pack://application:,,,/Resources/Themes/", StringComparison.Ordinal)) {
                    rs.Source = new Uri($"pack://application:,,,/Resources/Themes/{theme}.xaml", UriKind.Absolute);
                    break;
                }
            }

            Changed?.Invoke();
        }
    }
}
