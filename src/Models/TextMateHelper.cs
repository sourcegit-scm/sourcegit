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
            new ExtraGrammar("source.toml", [".toml"], "toml.json"),
            new ExtraGrammar("source.kotlin", [".kotlin", ".kt", ".kts"], "kotlin.json"),
            new ExtraGrammar("source.hx", [".hx"], "haxe.json"),
            new ExtraGrammar("source.hxml", [".hxml"], "hxml.json"),
            new ExtraGrammar("text.html.jsp", [".jsp", ".jspf", ".tag"], "jsp.json"),
            new ExtraGrammar("source.vue", [".vue"], "vue.json"),
        ];

        public static string GetScope(string file, RegistryOptions reg)
        {
            var extension = Path.GetExtension(file);
            if (extension == ".h")
                extension = ".cpp";
            else if (extension is ".resx" or ".plist" or ".manifest")
                extension = ".xml";
            else if (extension == ".command")
                extension = ".sh";

            foreach (var grammar in s_extraGrammars)
            {
                foreach (var ext in grammar.Extensions)
                {
                    if (ext.Equals(extension, StringComparison.OrdinalIgnoreCase))
                        return grammar.Scope;
                }
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

        private record ExtraGrammar(string Scope, List<string> Extensions, string File)
        {
            public readonly string Scope = Scope;
            public readonly List<string> Extensions = Extensions;
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
        private static RegistryOptionsWrapper s_darkRegistry;
        private static RegistryOptionsWrapper s_lightRegistry;

        private static RegistryOptionsWrapper GetRegistry(bool isDark)
        {
            if (isDark)
                return s_darkRegistry ??= new RegistryOptionsWrapper(ThemeName.DarkPlus);
            return s_lightRegistry ??= new RegistryOptionsWrapper(ThemeName.LightPlus);
        }

        public static TextMate.Installation CreateForEditor(TextEditor editor)
        {
            var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            return editor.InstallTextMate(GetRegistry(isDark == true));
        }

        public static void SetThemeByApp(TextMate.Installation installation)
        {
            if (installation is { RegistryOptions: RegistryOptionsWrapper reg })
            {
                var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                installation.SetTheme(reg.LoadTheme(isDark ? ThemeName.DarkPlus : ThemeName.LightPlus));
            }
        }

        public static void SetGrammarByFileName(TextMate.Installation installation, string filePath, ref string lastScope)
        {
            if (installation is { RegistryOptions: RegistryOptionsWrapper reg } && !string.IsNullOrEmpty(filePath))
            {
                // Registry options are shared across editors, so the last grammar scope must stay editor-local.
                var scope = reg.GetScope(filePath);
                if (lastScope != scope)
                {
                    lastScope = scope;
                    installation.SetGrammar(scope);
                    GC.Collect();
                }
            }
        }

        public static void DisposeInstallation(ref TextMate.Installation installation, ref string lastScope, bool collectGarbage = false)
        {
            installation?.Dispose();
            installation = null;
            lastScope = string.Empty;

            if (collectGarbage)
                GC.Collect();
        }
    }
}
