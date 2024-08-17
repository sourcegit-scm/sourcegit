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
            _extraGrammars = new Dictionary<string, IRawGrammar>();

            string[] extraGrammarFiles = ["toml.json"];
            foreach (var file in extraGrammarFiles)
            {
                var asset = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/Grammars/{file}",
                    UriKind.RelativeOrAbsolute));

                try
                {
                    var grammar = GrammarReader.ReadGrammarSync(new StreamReader(asset));
                    _extraGrammars.Add(grammar.GetScopeName(), grammar);
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
            if (_extraGrammars.TryGetValue(scopeName, out var grammar))
                return grammar;

            return _backend.GetGrammar(scopeName);
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
            var scope = $"source{extension}";
            if (_extraGrammars.ContainsKey(scope))
                return scope;
            
            if (extension == ".h")
                extension = ".cpp";
            else if (extension == ".resx" || extension == ".plist")
                extension = ".xml";
            else if (extension == ".command")
                extension = ".sh";

            return _backend.GetScopeByExtension(extension);
        }

        private RegistryOptions _backend = null;
        private Dictionary<string, IRawGrammar> _extraGrammars = null;
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
                if (Application.Current?.ActualThemeVariant == ThemeVariant.Dark)
                    installation.SetTheme(reg.LoadTheme(ThemeName.DarkPlus));
                else
                    installation.SetTheme(reg.LoadTheme(ThemeName.LightPlus));
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
