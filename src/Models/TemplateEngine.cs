using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Models
{
    public class TemplateEngine
    {
        private class Context(Branch branch, IReadOnlyList<Change> changes)
        {
            public Branch branch = branch;
            public IReadOnlyList<Change> changes = changes;
        }

        private class Text(string text)
        {
            public string text = text;
        }

        private class Variable(string name)
        {
            public string name = name;
        }

        private class SlicedVariable(string name, int count)
        {
            public string name = name;
            public int count = count;
        }

        private class RegexVariable(string name, Regex regex, string replacement)
        {
            public string name = name;
            public Regex regex = regex;
            public string replacement = replacement;
        }

        private const char ESCAPE = '\\';
        private const char VARIABLE_ANCHOR = '$';
        private const char VARIABLE_START = '{';
        private const char VARIABLE_END = '}';
        private const char VARIABLE_SLICE = ':';
        private const char VARIABLE_REGEX = '/';
        private const char NEWLINE = '\n';
        private const RegexOptions REGEX_OPTIONS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        public string Eval(string text, Branch branch, IReadOnlyList<Change> changes)
        {
            Reset();

            _chars = text.ToCharArray();
            Parse();

            var context = new Context(branch, changes);
            var sb = new StringBuilder();
            sb.EnsureCapacity(text.Length);
            foreach (var token in _tokens)
            {
                switch (token)
                {
                    case Text text_token:
                        sb.Append(text_token.text);
                        break;
                    case Variable var_token:
                        sb.Append(EvalVariable(context, var_token));
                        break;
                    case SlicedVariable sliced_var:
                        sb.Append(EvalVariable(context, sliced_var));
                        break;
                    case RegexVariable regex_var:
                        sb.Append(EvalVariable(context, regex_var));
                        break;
                }
            }

            return sb.ToString();
        }

        private void Reset()
        {
            _pos = 0;
            _chars = [];
            _tokens.Clear();
        }

        private char? Next()
        {
            var c = Peek();
            if (c is not null)
                _pos++;
            return c;
        }

        private char? Peek()
        {
            return (_pos >= _chars.Length) ? null : _chars[_pos];
        }

        private int? Integer()
        {
            var start = _pos;
            while (Peek() is >= '0' and <= '9')
            {
                _pos++;
            }
            if (start >= _pos)
                return null;

            var chars = new ReadOnlySpan<char>(_chars, start, _pos - start);
            return int.Parse(chars);
        }

        private void Parse()
        {
            // text token start
            var tok = _pos;
            bool esc = false;
            while (Next() is { } c)
            {
                if (esc)
                {
                    esc = false;
                    continue;
                }
                switch (c)
                {
                    case ESCAPE:
                        // allow to escape only \ and $
                        if (Peek() is ESCAPE or VARIABLE_ANCHOR)
                        {
                            esc = true;
                            FlushText(tok, _pos - 1);
                            tok = _pos;
                        }
                        break;
                    case VARIABLE_ANCHOR:
                        // backup the position
                        var bak = _pos;
                        var variable = TryParseVariable();
                        if (variable is null)
                        {
                            // no variable found, rollback
                            _pos = bak;
                        }
                        else
                        {
                            // variable found, flush a text token
                            FlushText(tok, bak - 1);
                            _tokens.Add(variable);
                            tok = _pos;
                        }
                        break;
                }
            }
            // flush text token
            FlushText(tok, _pos);
        }

        private void FlushText(int start, int end)
        {
            int len = end - start;
            if (len <= 0)
                return;
            var text = new string(_chars, start, len);
            _tokens.Add(new Text(text));
        }

        private object TryParseVariable()
        {
            if (Next() != VARIABLE_START)
                return null;
            var nameStart = _pos;
            while (Next() is { } c)
            {
                // name character, continue advancing
                if (IsNameChar(c))
                    continue;

                var nameEnd = _pos - 1;
                // not a name character but name is empty, cancel
                if (nameStart >= nameEnd)
                    return null;
                var name = new string(_chars, nameStart, nameEnd - nameStart);

                return c switch
                {
                    // variable
                    VARIABLE_END => new Variable(name),
                    // sliced variable
                    VARIABLE_SLICE => TryParseSlicedVariable(name),
                    // regex variable
                    VARIABLE_REGEX => TryParseRegexVariable(name),
                    _ => null,
                };
            }

            return null;
        }

        private object TryParseSlicedVariable(string name)
        {
            int? n = Integer();
            if (n is null)
                return null;
            if (Next() != VARIABLE_END)
                return null;

            return new SlicedVariable(name, (int)n);
        }

        private object TryParseRegexVariable(string name)
        {
            var regex = ParseRegex();
            if (regex == null)
                return null;
            var replacement = ParseReplacement();
            if (replacement == null)
                return null;

            return new RegexVariable(name, regex, replacement);
        }

        private Regex ParseRegex()
        {
            var sb = new StringBuilder();
            var tok = _pos;
            var esc = false;
            while (Next() is { } c)
            {
                if (esc)
                {
                    esc = false;
                    continue;
                }
                switch (c)
                {
                    case ESCAPE:
                        // allow to escape only / as \ and { used frequently in regexes
                        if (Peek() == VARIABLE_REGEX)
                        {
                            esc = true;
                            sb.Append(_chars, tok, _pos - 1 - tok);
                            tok = _pos;
                        }
                        break;
                    case VARIABLE_REGEX:
                        // goto is fine
                        goto Loop_exit;
                    case NEWLINE:
                        // no newlines allowed
                        return null;
                }
            }
        Loop_exit:
            sb.Append(_chars, tok, _pos - 1 - tok);

            try
            {
                var pattern = sb.ToString();
                if (pattern.Length == 0)
                    return null;
                var regex = new Regex(pattern, REGEX_OPTIONS);

                return regex;
            }
            catch (RegexParseException)
            {
                return null;
            }
        }

        private string ParseReplacement()
        {
            var sb = new StringBuilder();
            var tok = _pos;
            var esc = false;
            while (Next() is { } c)
            {
                if (esc)
                {
                    esc = false;
                    continue;
                }
                switch (c)
                {
                    case ESCAPE:
                        // allow to escape only right-brace
                        if (Peek() == VARIABLE_END)
                        {
                            esc = true;
                            sb.Append(_chars, tok, _pos - 1 - tok);
                            tok = _pos;
                        }
                        break;
                    case VARIABLE_END:
                        // goto is fine
                        goto Loop_exit;
                    case NEWLINE:
                        // no newlines allowed
                        return null;
                }
            }
        Loop_exit:
            sb.Append(_chars, tok, _pos - 1 - tok);

            var replacement = sb.ToString();

            return replacement;
        }

        private static bool IsNameChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
        }

        // (?) notice or log if variable is not found
        private static string EvalVariable(Context context, string name)
        {
            if (!s_variables.TryGetValue(name, out var getter))
                return string.Empty;
            return getter(context);
        }

        private static string EvalVariable(Context context, Variable variable)
        {
            return EvalVariable(context, variable.name);
        }

        private static string EvalVariable(Context context, SlicedVariable variable)
        {
            if (!s_slicedVariables.TryGetValue(variable.name, out var getter))
                return string.Empty;
            return getter(context, variable.count);
        }

        private static string EvalVariable(Context context, RegexVariable variable)
        {
            var str = EvalVariable(context, variable.name);
            if (string.IsNullOrEmpty(str))
                return str;
            return variable.regex.Replace(str, variable.replacement);
        }

        private int _pos = 0;
        private char[] _chars = [];
        private readonly List<object> _tokens = [];

        private delegate string VariableGetter(Context context);

        private static readonly IReadOnlyDictionary<string, VariableGetter> s_variables = new Dictionary<string, VariableGetter>() {
            // legacy variables
            {"branch_name", GetBranchName},
            {"files_num", GetFilesCount},
            {"files", GetFiles},
            //
            {"BRANCH", GetBranchName},
            {"FILES_COUNT", GetFilesCount},
            {"FILES", GetFiles},
        };

        private static string GetBranchName(Context context)
        {
            return context.branch.Name;
        }

        private static string GetFilesCount(Context context)
        {
            return context.changes.Count.ToString();
        }

        private static string GetFiles(Context context)
        {
            var paths = new List<string>();
            foreach (var c in context.changes)
                paths.Add(c.Path);
            return string.Join(", ", paths);
        }

        private delegate string VariableSliceGetter(Context context, int count);

        private static readonly IReadOnlyDictionary<string, VariableSliceGetter> s_slicedVariables = new Dictionary<string, VariableSliceGetter>() {
            // legacy variables
            {"files", GetFilesSliced},
            //
            {"FILES", GetFilesSliced},
        };

        private static string GetFilesSliced(Context context, int count)
        {
            var sb = new StringBuilder();
            var paths = new List<string>();
            var max = Math.Min(count, context.changes.Count);
            for (int i = 0; i < max; i++)
                paths.Add(context.changes[i].Path);

            sb.AppendJoin(", ", paths);
            if (max < context.changes.Count)
                sb.Append($" and {context.changes.Count - max} other files");

            return sb.ToString();
        }
    }
}
