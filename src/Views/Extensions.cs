using Avalonia.Markup.Xaml;
using System;

namespace SourceGit.Views {
    public class IntExtension : MarkupExtension {
        public int Value { get; set; }
        public IntExtension(int value) { this.Value = value; }
        public override object ProvideValue(IServiceProvider serviceProvider) {
            return Value;
        }
    }
}
