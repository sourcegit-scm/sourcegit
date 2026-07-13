using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;

namespace SourceGit.Models
{
    public enum TextDiffLineType
    {
        None,
        Normal,
        Indicator,
        Added,
        Deleted,
    }

    public class TextRange(int p, int n)
    {
        public int Start { get; set; } = p;
        public int End { get; set; } = p + n - 1;
    }

    public class TextDiffLine
    {
        public TextDiffLineType Type { get; set; } = TextDiffLineType.None;
        public byte[] RawContent { get; set; } = [];
        public string Content { get; set; } = "";
        public int OldLineNumber { get; set; } = 0;
        public int NewLineNumber { get; set; } = 0;
        public List<TextRange> Highlights { get; set; } = new List<TextRange>();
        public bool NoNewLineEndOfFile { get; set; } = false;

        public string OldLine => OldLineNumber == 0 ? string.Empty : OldLineNumber.ToString();
        public string NewLine => NewLineNumber == 0 ? string.Empty : NewLineNumber.ToString();

        public TextDiffLine() { }
        public TextDiffLine(TextDiffLineType type, string content, byte[] rawContent, int oldLine, int newLine)
        {
            Type = type;
            Content = content;
            RawContent = rawContent;
            OldLineNumber = oldLine;
            NewLineNumber = newLine;
        }
    }

    public partial class TextDiff
    {
        public List<TextDiffLine> Lines { get; set; } = new List<TextDiffLine>();
        public int MaxLineNumber = 0;
        public int AddedLines { get; set; } = 0;
        public int DeletedLines { get; set; } = 0;
        public int OldMode { get; set; } = 0;
        public int NewMode { get; set; } = 0;
        public string OldHash { get; set; } = string.Empty;
        public string NewHash { get; set; } = string.Empty;
    }

    public class LFSDiff
    {
        public LFSObject Old { get; set; } = new LFSObject();
        public LFSObject New { get; set; } = new LFSObject();
    }

    public class BinaryDiff
    {
        public long OldSize { get; set; } = 0;
        public long NewSize { get; set; } = 0;
    }

    public class ImageDiff
    {
        public Bitmap Old { get; set; } = null;
        public Bitmap New { get; set; } = null;

        public long OldFileSize { get; set; } = 0;
        public long NewFileSize { get; set; } = 0;

        public string OldImageSize => Old != null ? $"{Old.PixelSize.Width} x {Old.PixelSize.Height}" : "0 x 0";
        public string NewImageSize => New != null ? $"{New.PixelSize.Width} x {New.PixelSize.Height}" : "0 x 0";
    }

    public class EmptyFile
    {
        public const string SHA1 = "e69de29bb2d1d6434b8b29ae775ad8c2e48c5391";
        public const string SHA256 = "473a0f4c3be8a93681a267e3b1e9a7dcda1185436fe141f7749120a303721813";
    }

    public class NoOrEOLChange;

    public class SubmoduleDiff
    {
        public string FullPath { get; set; } = string.Empty;
        public RevisionSubmodule Old { get; set; } = null;
        public RevisionSubmodule New { get; set; } = null;

        public bool CanOpenDetails => File.Exists(Path.Combine(FullPath, ".git")) &&
            Old != null && Old.Commit.Author != User.Invalid &&
            New != null && New.Commit.Author != User.Invalid;
    }

    public class DiffResult
    {
        public bool IsBinary { get; set; } = false;
        public bool IsSubmoduleChange { get; set; } = false;
        public string OldHash { get; set; } = string.Empty;
        public string NewHash { get; set; } = string.Empty;
        public int OldMode { get; set; } = 0;
        public int NewMode { get; set; } = 0;
        public TextDiff TextDiff { get; set; } = null;
        public LFSDiff LFSDiff { get; set; } = null;
    }
}
