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
        private static readonly ExtraGrammar[] s_extraGrammas =
        [
            new ExtraGrammar("source.toml", ".toml", "toml.json"),
            new ExtraGrammar("source.kotlin", ".kotlin", "kotlin.json"),
            new ExtraGrammar("source.hx", ".hx", "haxe.json"),
            new ExtraGrammar("source.hxml", ".hxml", "hxml.json"),
        ];

        public static string GetExtension(string file)
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

            return extension;
        }

        public static string GetScopeByExtension(string extension)
        {
            foreach (var grammar in s_extraGrammas)
            {
                if (grammar.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase))
                    return grammar.Scope;
            }

            return null;
        }

        public static IRawGrammar Load(string scopeName)
        {
            foreach (var grammar in s_extraGrammas)
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

            return null;
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
        public IRawTheme GetTheme(string scopeName)
        {
            return _backend.GetTheme(scopeName);
        }

        public IRawGrammar GetGrammar(string scopeName)
        {
            return GrammarUtility.Load(scopeName) ?? _backend.GetGrammar(scopeName);
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
            var ext = GrammarUtility.GetExtension(filename);
            return GrammarUtility.GetScopeByExtension(ext) ?? _backend.GetScopeByExtension(ext);
        }

        private readonly RegistryOptions _backend = new(defaultTheme);
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
