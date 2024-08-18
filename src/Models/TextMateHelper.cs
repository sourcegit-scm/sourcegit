using System;
using System.Collections.Generic;
using System.IO;

using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;

using AvaloniaEdit;
using AvaloniaEdit.TextMate;

using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace SourceGit.Models
{
    public class RegistryOptionsWrapper : IRegistryOptions
    {
        public RegistryOptionsWrapper(ThemeName defaultTheme)
        {
            _backend = new RegistryOptions(defaultTheme);
            _extraGrammars = new List<IRawGrammar>();

            string[] extraGrammarFiles = ["toml.json"];
            foreach (var file in extraGrammarFiles)
            {
                var asset = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/Grammars/{file}",
                    UriKind.RelativeOrAbsolute));

                try
                {
                    var grammar = GrammarReader.ReadGrammarSync(new StreamReader(asset));
                    _extraGrammars.Add(grammar);
                }
                catch
                {
                    // ignore
                }
            }
        }
        
        public IRawTheme GetTheme(string scopeName)
        {
            return _backend.GetTheme(scopeName);
        }

        public IRawGrammar GetGrammar(string scopeName)
        {
            var grammar = _extraGrammars.Find(x => x.GetScopeName().Equals(scopeName, StringComparison.Ordinal));
            return grammar ?? _backend.GetGrammar(scopeName);
        }

        public ICollection<string> GetInjections(string scopeName)
        {
            return _backend.GetInjections(scopeName);
        }

        public IRawTheme GetDefaultTheme()
        {
            return _backend.GetDefaultTheme();
        }

        public IRawTheme LoadTheme(ThemeName name)
        {
            return _backend.LoadTheme(name);
        }

        public string GetScopeByFileName(string filename)
        {
            var extension = Path.GetExtension(filename);
            var grammar = _extraGrammars.Find(x => x.GetScopeName().EndsWith(extension, StringComparison.OrdinalIgnoreCase));
            if (grammar != null)
                return grammar.GetScopeName();
            
            if (extension == ".h")
                extension = ".cpp";
            else if (extension == ".resx" || extension == ".plist" || extension == ".manifest")
                extension = ".xml";
            else if (extension == ".command")
                extension = ".sh";

            return _backend.GetScopeByExtension(extension);
        }

        private readonly RegistryOptions _backend;
        private readonly List<IRawGrammar> _extraGrammars;
    }
    
    public static class TextMateHelper
    {
        public static TextMate.Installation CreateForEditor(TextEditor editor)
        {
            if (Application.Current?.ActualThemeVariant == ThemeVariant.Dark)
                return editor.InstallTextMate(new RegistryOptionsWrapper(ThemeName.DarkPlus));

            return editor.InstallTextMate(new RegistryOptionsWrapper(ThemeName.LightPlus));
        }

        public static void SetThemeByApp(TextMate.Installation installation)
        {
            if (installation == null)
                return;

            if (installation.RegistryOptions is RegistryOptionsWrapper reg)
            {
                var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                installation.SetTheme(reg.LoadTheme(isDark ? ThemeName.DarkPlus : ThemeName.LightPlus));
            }
        }

        public static void SetGrammarByFileName(TextMate.Installation installation, string filePath)
        {
            if (installation is { RegistryOptions: RegistryOptionsWrapper reg })
            {
                installation.SetGrammar(reg.GetScopeByFileName(filePath));
                GC.Collect();
            }
        }
    }
}
