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
    public static class GrammarUtility
    {
        private static readonly ExtraGrammar[] s_extraGrammars =
        [
            new ExtraGrammar("source.toml", ".toml", "toml.json"),
            new ExtraGrammar("source.kotlin", ".kotlin", "kotlin.json"),
            new ExtraGrammar("source.hx", ".hx", "haxe.json"),
            new ExtraGrammar("source.hxml", ".hxml", "hxml.json"),
        ];

        public static string GetScope(string file, RegistryOptions reg)
        {
            var extension = Path.GetExtension(file);
            if (extension == ".h")
                extension = ".cpp";
            else if (extension == ".resx" || extension == ".plist" || extension == ".manifest")
                extension = ".xml";
            else if (extension == ".command")
                extension = ".sh";
            else if (extension == ".kt" || extension == ".kts")
                extension = ".kotlin";
            
            foreach (var grammar in s_extraGrammars)
            {
                if (grammar.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase))
                    return grammar.Scope;
            }

            return reg.GetScopeByExtension(extension);
        }

        public static IRawGrammar GetGrammar(string scopeName, RegistryOptions reg)
        {
            foreach (var grammar in s_extraGrammars)
            {
                if (grammar.Scope.Equals(scopeName, StringComparison.OrdinalIgnoreCase))
                {
                    var asset = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/Grammars/{grammar.File}",
                        UriKind.RelativeOrAbsolute));

                    try
                    {
                        return GrammarReader.ReadGrammarSync(new StreamReader(asset));
                    }
                    catch
                    {
                        break;
                    }
                }
            }

            return reg.GetGrammar(scopeName);
        }

        private record ExtraGrammar(string Scope, string Extension, string File)
        {
            public readonly string Scope = Scope;
            public readonly string Extension = Extension;
            public readonly string File = File;
        }
    }

    public class RegistryOptionsWrapper(ThemeName defaultTheme) : IRegistryOptions
    {
        public IRawTheme GetTheme(string scopeName) => _backend.GetTheme(scopeName);
        public IRawTheme GetDefaultTheme() => _backend.GetDefaultTheme();
        public IRawTheme LoadTheme(ThemeName name) => _backend.LoadTheme(name);
        public ICollection<string> GetInjections(string scopeName) => _backend.GetInjections(scopeName);
        public IRawGrammar GetGrammar(string scopeName) => GrammarUtility.GetGrammar(scopeName, _backend);
        public string GetScope(string filename) => GrammarUtility.GetScope(filename, _backend);
        
        private readonly RegistryOptions _backend = new(defaultTheme);
    }

    public static class TextMateHelper
    {
        public static TextMate.Installation CreateForEditor(TextEditor editor)
        {
            return editor.InstallTextMate(Application.Current?.ActualThemeVariant == ThemeVariant.Dark ? 
                new RegistryOptionsWrapper(ThemeName.DarkPlus) : 
                new RegistryOptionsWrapper(ThemeName.LightPlus));
        }

        public static void SetThemeByApp(TextMate.Installation installation)
        {
            if (installation is { RegistryOptions: RegistryOptionsWrapper reg })
            {
                var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                installation.SetTheme(reg.LoadTheme(isDark ? ThemeName.DarkPlus : ThemeName.LightPlus));
            }
        }

        public static void SetGrammarByFileName(TextMate.Installation installation, string filePath)
        {
            if (installation is { RegistryOptions: RegistryOptionsWrapper reg })
            {
                installation.SetGrammar(reg.GetScope(filePath));
                GC.Collect();
            }
        }
    }
}
