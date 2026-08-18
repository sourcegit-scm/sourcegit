using System;

namespace SourceGit.Models
{
    /// <summary>
    ///     Validates branch and tag names using the same rules that git itself enforces,
    ///     mirroring `git check-ref-format --allow-onelevel`. Rules are taken from the git-check-ref-format documentation.
    ///     Rule 2 (a refname must contain at least one slash) is intentionally waived, matching `--allow-onelevel`.
    /// </summary>
    public static class RefName
    {
        public static bool IsValidBranchName(string name)
        {
            if (string.Equals(name, "HEAD", StringComparison.Ordinal))
                return false;

            return IsValidRefName(name);
        }

        public static bool IsValidTagName(string name) => IsValidRefName(name);

        private static bool IsValidRefName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            // Anything starting with '-' is treated as a CLI option.
            if (name.StartsWith('-'))
                return false;

            // Rule 9: cannot be the single character '@'.
            if (name.Equals("@", StringComparison.Ordinal))
                return false;

            // Rule 6: cannot begin or end with '/', or contain consecutive slashes.
            if (name[0] == '/' || name[^1] == '/' || name.Contains("//", StringComparison.Ordinal))
                return false;

            // Rule 7: cannot end with a dot.
            if (name[^1] == '.')
                return false;

            // Rule 3: cannot contain two consecutive dots.
            if (name.Contains("..", StringComparison.Ordinal))
                return false;

            // Rule 8: cannot contain the sequence '@{'.
            if (name.Contains("@{", StringComparison.Ordinal))
                return false;

            // Rules 4, 5 & 10: no control chars, DEL, space, or ~ ^ : ? * [ \.
            foreach (var ch in name)
            {
                if (ch is < ' ' or '\x7f')
                    return false;

                switch (ch)
                {
                    case ' ':
                    case '~':
                    case '^':
                    case ':':
                    case '?':
                    case '*':
                    case '[':
                    case '\\':
                        return false;
                }
            }

            // Rule 1: no slash-separated component may begin with a dot or end with ".lock".
            foreach (var component in name.Split('/'))
            {
                if (component[0] == '.')
                    return false;

                if (component.EndsWith(".lock", StringComparison.Ordinal))
                    return false;
            }

            return true;
        }
    }
}
