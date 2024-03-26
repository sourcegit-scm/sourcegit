using System;
using System.IO;

using Avalonia.Styling;

using AvaloniaEdit;
using AvaloniaEdit.TextMate;

using TextMateSharp.Grammars;

namespace SourceGit.Models
{
    public static class TextMateHelper
    {
        public static TextMate.Installation CreateForEditor(TextEditor editor)
        {
            if (App.Current?.ActualThemeVariant == ThemeVariant.Dark)
            {
                return editor.InstallTextMate(new RegistryOptions(ThemeName.DarkPlus));
            }
            else
            {
                return editor.InstallTextMate(new RegistryOptions(ThemeName.LightPlus));
            }
        }

        public static void SetThemeByApp(TextMate.Installation installation)
        {
            if (installation == null) return;

            var reg = installation.RegistryOptions as RegistryOptions;
            if (App.Current?.ActualThemeVariant == ThemeVariant.Dark)
            {
                installation.SetTheme(reg.LoadTheme(ThemeName.DarkPlus));
            }
            else
            {
                installation.SetTheme(reg.LoadTheme(ThemeName.LightPlus));
            }
        }

        public static void SetGrammarByFileName(TextMate.Installation installation, string filePath)
        {
            if (installation == null) return;

            var ext = Path.GetExtension(filePath);
            if (ext == ".h")
            {
                ext = ".cpp";
            }
            else if (ext == ".resx" || ext == ".plist")
            {
                ext = ".xml";
            }

            var reg = installation.RegistryOptions as RegistryOptions;
            installation.SetGrammar(reg.GetScopeByExtension(ext));
            GC.Collect();
        }
    }
}