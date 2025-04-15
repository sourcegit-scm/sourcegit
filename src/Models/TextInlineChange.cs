using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public class TextInlineChange
    {
        public int DeletedStart { get; set; }
        public int DeletedCount { get; set; }
        public int AddedStart { get; set; }
        public int AddedCount { get; set; }
        public int DeletedLine { get; set; }
        public int AddedLine { get; set; }

        private TextInlineChange(int deletedLine, int dp, int dc, int addedLine, int ap, int ac)
        {
            DeletedLine = deletedLine;
            DeletedStart = dp;
            DeletedCount = dc;
            AddedLine = addedLine;
            AddedStart = ap;
            AddedCount = ac;
        }

        private TextInlineChange(int dp, int dc, int ap, int ac)
        {
            DeletedStart = dp;
            DeletedCount = dc;
            AddedStart = ap;
            AddedCount = ac;
        }

        private static List<string> Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var tokens = new List<string>();
            var delims = new HashSet<char>(" \t+-*/=!,:;.'\"/?|&#@%`<>()[]{}\\".ToCharArray());

            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (!delims.Contains(text[i]))
                    continue;

                if (start != i)
                    tokens.Add(text.Substring(start, i - start));

                tokens.Add(text.Substring(i, 1));
                start = i + 1;
            }

            if (start < text.Length)
                tokens.Add(text.Substring(start));

            return tokens;
        }

        private static List<EditOperation> ComputeDiff(List<string> oldTokens, List<string> newTokens)
        {
            var operations = new List<EditOperation>();

            // Implementation of Myers diff algorithm
            var n = oldTokens.Count;
            var m = newTokens.Count;
            var max = n + m;
            var trace = new List<Dictionary<int, int>>();
            var v = new Dictionary<int, int> { { 1, 0 } };

            for (var d = 0; d <= max; d++)
            {
                trace.Add(new Dictionary<int, int>(v));

                for (var k = -d; k <= d; k += 2)
                {
                    int x;
                    if (k == -d || (k != d && v.GetValueOrDefault(k - 1, 0) < v.GetValueOrDefault(k + 1, 0)))
                        x = v.GetValueOrDefault(k + 1, 0);
                    else
                        x = v.GetValueOrDefault(k - 1, 0) + 1;

                    int y = x - k;

                    while (x < n && y < m && oldTokens[x].Equals(newTokens[y]))
                    {
                        x++;
                        y++;
                    }

                    v[k] = x;

                    if (x >= n && y >= m)
                    {
                        // Backtrack edit path
                        BacktrackEditPath(trace, operations, n, m, d);
                        return operations;
                    }
                }
            }

            return operations;
        }

        private static void BacktrackEditPath(
            List<Dictionary<int, int>> trace, 
            List<EditOperation> operations, 
            int n, int m, int d)
        {
            int px = n, py = m;
            for (int i = d; i > 0; i--)
            {
                var kk = px - py;
                int prevK;

                if (kk == -i || (kk != i && trace[i].GetValueOrDefault(kk - 1, 0) <
                        trace[i].GetValueOrDefault(kk + 1, 0)))
                    prevK = kk + 1;
                else
                    prevK = kk - 1;

                int prevX = trace[i][prevK];
                int prevY = prevX - prevK;

                while (px > prevX && py > prevY)
                {
                    operations.Add(new EditOperation(EditType.Equal, px - 1, py - 1));
                    px--;
                    py--;
                }

                operations.Add(px == prevX ?
                    new EditOperation(EditType.Insert, px, py - 1) :
                    new EditOperation(EditType.Delete, px - 1, py));

                px = prevX;
                py = prevY;
            }

            while (px > 0 && py > 0)
            {
                operations.Add(new EditOperation(EditType.Equal, px - 1, py - 1));
                px--;
                py--;
            }

            while (px > 0)
            {
                operations.Add(new EditOperation(EditType.Delete, px - 1, 0));
                px--;
            }

            while (py > 0)
            {
                operations.Add(new EditOperation(EditType.Insert, 0, py - 1));
                py--;
            }

            operations.Reverse();
        }

        private static int[] CalculateOffsets(List<string> tokens, string text)
        {
            var offsets = new int[tokens.Count];
            var pos = 0;

            for (var i = 0; i < tokens.Count; i++)
            {
                // Find position of token in original string
                while (pos < text.Length && pos + tokens[i].Length <= text.Length && 
                       !text.Substring(pos, tokens[i].Length).Equals(tokens[i]))
                {
                    pos++;
                }

                offsets[i] = pos;
                pos += tokens[i].Length;
            }

            return offsets;
        }

        // Merge adjacent or overlapping changes
        private static List<TextInlineChange> MergeChanges(List<TextInlineChange> changes)
        {
            if (changes.Count <= 1)
                return changes;

            var result = new List<TextInlineChange>();
            var current = changes[0];
            const int MERGE_THRESHOLD = 3; // Distance threshold to merge changes

            for (var i = 1; i < changes.Count; i++)
            {
                var next = changes[i];

                // If distance between changes is small, merge them
                if ((next.DeletedStart - (current.DeletedStart + current.DeletedCount) <= MERGE_THRESHOLD) ||
                    (next.AddedStart - (current.AddedStart + current.AddedCount) <= MERGE_THRESHOLD))
                {
                    // Calculate merged range
                    int deleteEnd = Math.Max(current.DeletedStart + current.DeletedCount,
                        next.DeletedStart + next.DeletedCount);
                    int insertEnd = Math.Max(current.AddedStart + current.AddedCount,
                        next.AddedStart + next.AddedCount);

                    current = new TextInlineChange(
                        Math.Min(current.DeletedStart, next.DeletedStart),
                        deleteEnd - Math.Min(current.DeletedStart, next.DeletedStart),
                        Math.Min(current.AddedStart, next.AddedStart),
                        insertEnd - Math.Min(current.AddedStart, next.AddedStart));
                }
                else
                {
                    result.Add(current);
                    current = next;
                }
            }

            result.Add(current);
            return result;
        }

        public static List<TextInlineChange> CompareMultiLine(
            string oldValue, string newValue,
            List<TextDiffLine> oldLines, List<TextDiffLine> newLines)
        {
            var changes = new List<TextInlineChange>();

            // Tokenize multi-line text
            var oldTokens = TokenizeMultiLine(oldValue);
            var newTokens = TokenizeMultiLine(newValue);

            // Map tokens to their lines and positions
            var oldLineMap = MapTokensToLines(oldTokens, oldLines);
            var newLineMap = MapTokensToLines(newTokens, newLines);

            // Calculate differences
            var operations = ComputeDiff(oldTokens, newTokens);

            // Process operations to generate inline changes
            ProcessMultiLineOperations(operations, oldTokens, newTokens, oldLineMap, newLineMap, changes);

            return changes;
        }

        private static void ProcessMultiLineOperations(
            List<EditOperation> operations, 
            List<string> oldTokens, 
            List<string> newTokens,
            TokenLocation[] oldLineMap, 
            TokenLocation[] newLineMap,
            List<TextInlineChange> changes)
        {
            var deleteStart = -1;
            var deleteSize = 0;
            var deleteLine = -1;

            var insertStart = -1;
            var insertSize = 0;
            var insertLine = -1;

            foreach (var op in operations)
            {
                switch (op.Type)
                {
                    case EditType.Delete:
                        ProcessDeleteOperation(op, oldTokens, oldLineMap, ref deleteStart, ref deleteSize, 
                            ref deleteLine, ref insertStart, ref insertSize, 
                            ref insertLine, changes);
                        break;

                    case EditType.Insert:
                        ProcessInsertOperation(op, newTokens, newLineMap, ref deleteStart, ref deleteSize, 
                            ref deleteLine, ref insertStart, ref insertSize, 
                            ref insertLine, changes);
                        break;

                    case EditType.Equal:
                        // If there are pending changes, add them to result
                        if (deleteStart != -1 || insertStart != -1)
                        {
                            changes.Add(new TextInlineChange(
                                deleteLine, deleteStart, deleteSize,
                                insertLine, insertStart, insertSize));

                            deleteStart = -1;
                            deleteSize = 0;
                            deleteLine = -1;

                            insertStart = -1;
                            insertSize = 0;
                            insertLine = -1;
                        }
                        break;
                }
            }

            // Process final changes
            if (deleteStart != -1 || insertStart != -1)
            {
                changes.Add(new TextInlineChange(
                    deleteLine, deleteStart, deleteSize,
                    insertLine, insertStart, insertSize));
            }
        }

        private static void ProcessDeleteOperation(
            EditOperation op, 
            List<string> oldTokens, 
            TokenLocation[] oldLineMap,
            ref int deleteStart, 
            ref int deleteSize, 
            ref int deleteLine,
            ref int insertStart, 
            ref int insertSize, 
            ref int insertLine,
            List<TextInlineChange> changes)
        {
            if (deleteStart == -1)
            {
                deleteStart = oldLineMap[op.OldIndex].Position;
                deleteLine = oldLineMap[op.OldIndex].Line;
            }
            // If deletion is in a different line, process previous changes and start new ones
            else if (deleteLine != oldLineMap[op.OldIndex].Line)
            {
                if (insertStart != -1)
                {
                    changes.Add(new TextInlineChange(
                        deleteLine, deleteStart, deleteSize,
                        insertLine, insertStart, insertSize));
                }
                else
                {
                    changes.Add(new TextInlineChange(
                        deleteLine, deleteStart, deleteSize,
                        -1, 0, 0));
                }

                deleteStart = oldLineMap[op.OldIndex].Position;
                deleteLine = oldLineMap[op.OldIndex].Line;
                deleteSize = 0;

                insertStart = -1;
                insertSize = 0;
                insertLine = -1;
            }

            deleteSize += oldTokens[op.OldIndex].Length;
        }

        private static void ProcessInsertOperation(
            EditOperation op, 
            List<string> newTokens, 
            TokenLocation[] newLineMap,
            ref int deleteStart, 
            ref int deleteSize, 
            ref int deleteLine,
            ref int insertStart, 
            ref int insertSize, 
            ref int insertLine,
            List<TextInlineChange> changes)
        {
            if (insertStart == -1)
            {
                insertStart = newLineMap[op.NewIndex].Position;
                insertLine = newLineMap[op.NewIndex].Line;
            }
            // If insertion is in a different line, process previous changes and start new ones
            else if (insertLine != newLineMap[op.NewIndex].Line)
            {
                if (deleteStart != -1)
                {
                    changes.Add(new TextInlineChange(
                        deleteLine, deleteStart, deleteSize,
                        insertLine, insertStart, insertSize));
                }
                else
                {
                    changes.Add(new TextInlineChange(
                        -1, 0, 0,
                        insertLine, insertStart, insertSize));
                }

                insertStart = newLineMap[op.NewIndex].Position;
                insertLine = newLineMap[op.NewIndex].Line;
                insertSize = 0;

                deleteStart = -1;
                deleteSize = 0;
                deleteLine = -1;
            }

            insertSize += newTokens[op.NewIndex].Length;
        }

        private static List<string> TokenizeMultiLine(string text)
        {
            var tokens = new List<string>();
            var lines = text.Split('\n');

            foreach (var line in lines)
            {
                var lineTokens = Tokenize(line);
                tokens.AddRange(lineTokens);
                tokens.Add("\n"); // Add newline as special token
            }

            // Remove trailing newline token if present
            if (tokens.Count > 0 && tokens[tokens.Count - 1] == "\n")
            {
                tokens.RemoveAt(tokens.Count - 1);
            }

            return tokens;
        }

        private static TokenLocation[] MapTokensToLines(List<string> tokens, List<TextDiffLine> lines)
        {
            var map = new TokenLocation[tokens.Count];
            var currentLine = 0;
            var posInLine = 0;

            for (var i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] == "\n")
                {
                    currentLine++;
                    posInLine = 0;
                    map[i] = new TokenLocation { Line = -1, Position = -1 }; // Newlines don't belong to any position
                }
                else
                {
                    if (currentLine < lines.Count)
                    {
                        map[i] = new TokenLocation { Line = currentLine, Position = posInLine };
                        posInLine += tokens[i].Length;
                    }
                    else
                    {
                        map[i] = new TokenLocation { Line = -1, Position = -1 };
                    }
                }
            }

            return map;
        }

        private class TokenLocation
        {
            public int Line { get; init; }
            public int Position { get; init; }
        }

        private enum EditType
        {
            Equal,
            Insert,
            Delete
        }

        private class EditOperation(EditType type, int oldIndex, int newIndex)
        {
            public EditType Type { get; } = type;
            public int OldIndex { get; } = oldIndex;
            public int NewIndex { get; } = newIndex;
        }
    }
}
