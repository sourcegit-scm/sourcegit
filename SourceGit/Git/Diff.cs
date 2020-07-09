using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Git {

    /// <summary>
    ///     Diff helper.
    /// </summary>
    public class Diff {
        private static readonly Regex REG_INDICATOR = new Regex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@", RegexOptions.None);

        /// <summary>
        ///     Line mode.
        /// </summary>
        public enum LineMode {
            Normal,
            Indicator,
            Empty,
            Added,
            Deleted,
        }

        /// <summary>
        ///     Side
        /// </summary>
        public enum Side {
            Left,
            Right,
            Both,
        }

        /// <summary>
        ///     Block
        /// </summary>
        public class Block {
            public Side Side = Side.Both;
            public LineMode Mode = LineMode.Normal;
            public int LeftStart = 0;
            public int RightStart = 0;
            public int Count = 0;
            public StringBuilder Builder = new StringBuilder();

            public bool IsLeftDelete => Side == Side.Left && Mode == LineMode.Deleted;
            public bool IsRightAdded => Side == Side.Right && Mode == LineMode.Added;
            public bool IsBothSideNormal => Side == Side.Both && Mode == LineMode.Normal;
            public bool CanShowNumber => Mode != LineMode.Indicator && Mode != LineMode.Empty;

            public void Append(string data) {
                if (Count > 0) Builder.AppendLine();
                Builder.Append(data);
                Count++;
            }
        }

        /// <summary>
        ///     Diff result.
        /// </summary>
        public class Result {
            public bool IsValid = false;
            public bool IsBinary = false;
            public List<Block> Blocks = new List<Block>();
            public int LeftLineCount = 0;
            public int RightLineCount = 0;

            public void SetBinary() {
                IsValid = true;
                IsBinary = true;
            }

            public void Add(Block b) {
                if (b.Count == 0) return;

                switch (b.Side) {
                case Side.Left:
                    LeftLineCount += b.Count;
                    break;
                case Side.Right:
                    RightLineCount += b.Count;
                    break;
                default:
                    LeftLineCount += b.Count;
                    RightLineCount += b.Count;
                    break;
                }

                Blocks.Add(b);
            }

            public void Fit() {
                if (LeftLineCount > RightLineCount) {
                    var b = new Block();
                    b.Side = Side.Right;
                    b.Mode = LineMode.Empty;

                    var delta = LeftLineCount - RightLineCount;
                    for (int i = 0; i < delta; i++) b.Append("");

                    Add(b);
                } else if (LeftLineCount < RightLineCount) {
                    var b = new Block();
                    b.Side = Side.Left;
                    b.Mode = LineMode.Empty;

                    var delta = RightLineCount - LeftLineCount;
                    for (int i = 0; i < delta; i++) b.Append("");

                    Add(b);
                }
            }
        }

        /// <summary>
        ///     Run diff process.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static Result Run(Repository repo, string args) {
            var rs = new Result();
            var current = new Block();
            var left = 0;
            var right = 0;

            repo.RunCommand($"diff --ignore-cr-at-eol {args}", line => {
                if (rs.IsBinary) return;

                if (!rs.IsValid) {
                    var match = REG_INDICATOR.Match(line);
                    if (!match.Success) {
                        if (line.StartsWith("Binary ")) rs.SetBinary();
                        return;
                    }

                    rs.IsValid = true;
                    left = int.Parse(match.Groups[1].Value);
                    right = int.Parse(match.Groups[2].Value);
                    current.Mode = LineMode.Indicator;
                    current.Append(line);
                } else {
                    if (line[0] == '-') {
                        if (current.IsLeftDelete) {
                            current.Append(line.Substring(1));
                        } else {
                            rs.Add(current);

                            current = new Block();
                            current.Side = Side.Left;
                            current.Mode = LineMode.Deleted;
                            current.LeftStart = left;
                            current.Append(line.Substring(1));
                        }

                        left++;
                    } else if (line[0] == '+') {
                        if (current.IsRightAdded) {
                            current.Append(line.Substring(1));
                        } else {
                            rs.Add(current);

                            current = new Block();
                            current.Side = Side.Right;
                            current.Mode = LineMode.Added;
                            current.RightStart = right;
                            current.Append(line.Substring(1));
                        }

                        right++;
                    } else if (line[0] == '\\') {
                        var tmp = new Block();
                        tmp.Side = current.Side;
                        tmp.Mode = LineMode.Indicator;
                        tmp.Append(line.Substring(1));

                        rs.Add(current);
                        rs.Add(tmp);
                        rs.Fit();

                        current = new Block();
                        current.LeftStart = left;
                        current.RightStart = right;
                    } else {
                        var match = REG_INDICATOR.Match(line);
                        if (match.Success) {
                            rs.Add(current);
                            rs.Fit();

                            left = int.Parse(match.Groups[1].Value);
                            right = int.Parse(match.Groups[2].Value);

                            current = new Block();
                            current.Mode = LineMode.Indicator;
                            current.Append(line);
                        } else {
                            if (current.IsBothSideNormal) {
                                current.Append(line.Substring(1));
                            } else {
                                rs.Add(current);
                                rs.Fit();

                                current = new Block();
                                current.LeftStart = left;
                                current.RightStart = right;
                                current.Append(line.Substring(1));
                            }

                            left++;
                            right++;
                        }
                    }
                }
            });

            rs.Add(current);
            rs.Fit();

            if (rs.IsBinary) {
                var b = new Block();
                b.Mode = LineMode.Indicator;
                b.Append("BINARY FILES NOT SUPPORTED!!!");
                rs.Blocks.Clear();
                rs.Blocks.Add(b);
            } else if (rs.Blocks.Count == 0) {
                var b = new Block();
                b.Mode = LineMode.Indicator;
                b.Append("NO CHANGES OR ONLY WHITESPACE CHANGES!!!");
                rs.Blocks.Add(b);
            }

            return rs;
        }
    }
}
