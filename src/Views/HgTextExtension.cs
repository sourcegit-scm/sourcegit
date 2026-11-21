using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace SourceGit.Views
{
    public class HgTextExtension : MarkupExtension
    {
        public string Key { get; set; }
        public string Text { get; set; }
        public IBinding Label { get; set; }

        public HgTextExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var label = Label;
            if (label == null)
            {
                label = Text == null
                    ? (IBinding)new DynamicResourceExtension($"Text.{Key}").ProvideValue(serviceProvider)
                    : new Binding() { Source = Text };
            }

            return new MultiBinding()
            {
                Bindings =
                {
                    label,
                    new Binding(nameof(ViewModels.Preferences.ExtendContextMenusWithHgTerminology))
                    {
                        Source = ViewModels.Preferences.Instance,
                    },
                },
                Converter = _converter,
                ConverterParameter = Key,
            };
        }

        private class HgTextConverter : IMultiValueConverter
        {
            public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
            {
                if (values.Count != 2 || values[0] is not string label)
                    return BindingOperations.DoNothing;

                return Models.HgTerminology.Extend(parameter as string, label, values[1] is true);
            }
        }

        private static readonly HgTextConverter _converter = new();
    }
}
