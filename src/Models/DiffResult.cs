using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

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
        public string Repository { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string NewRevision { get; set; } = string.Empty;

        public long OldSize { get; set; } = 0;
        public long NewSize { get; set; } = 0;
    }

    public class ImageDiff : ObservableObject
    {
        public Bitmap Old
        {
            get => _old;
            set
            {
                if (SetProperty(ref _old, value))
                {
                    if (HeadImage == null && !_isComparingWithStaged)
                        HeadImage = value;
                    _detectionResult = null;
                    OnPropertyChanged(nameof(OldImageSize));
                    OnPropertyChanged(nameof(DetectionResult));
                    OnPropertyChanged(nameof(ChangeOutlines));
                    OnPropertyChanged(nameof(ChangeCount));
                    OnPropertyChanged(nameof(ChangedPixelCount));
                    OnPropertyChanged(nameof(ChangedPixelPercentage));
                    OnPropertyChanged(nameof(DiffPixelStatsText));
                    OnPropertyChanged(nameof(DiffAreaStatsText));
                }
            }
        }

        public Bitmap New
        {
            get => _new;
            set
            {
                if (SetProperty(ref _new, value))
                {
                    _detectionResult = null;
                    OnPropertyChanged(nameof(NewImageSize));
                    OnPropertyChanged(nameof(DetectionResult));
                    OnPropertyChanged(nameof(ChangeOutlines));
                    OnPropertyChanged(nameof(ChangeCount));
                    OnPropertyChanged(nameof(ChangedPixelCount));
                    OnPropertyChanged(nameof(ChangedPixelPercentage));
                    OnPropertyChanged(nameof(DiffPixelStatsText));
                    OnPropertyChanged(nameof(DiffAreaStatsText));
                }
            }
        }

        public long OldFileSize
        {
            get => _oldFileSize;
            set
            {
                if (SetProperty(ref _oldFileSize, value))
                {
                    if (HeadFileSize == 0 && !_isComparingWithStaged)
                        HeadFileSize = value;
                }
            }
        }

        public long NewFileSize
        {
            get => _newFileSize;
            set => SetProperty(ref _newFileSize, value);
        }

        public string OldImageSize => Old != null ? $"{Old.PixelSize.Width} x {Old.PixelSize.Height}" : "0 x 0";
        public string NewImageSize => New != null ? $"{New.PixelSize.Width} x {New.PixelSize.Height}" : "0 x 0";

        public Bitmap HeadImage { get; set; } = null;
        public long HeadFileSize { get; set; } = 0;

        public Bitmap StagedImage { get; set; } = null;
        public long StagedFileSize { get; set; } = 0;

        public bool CanCompareWithStaged => StagedImage != null && (HeadImage != null || IsUnstaged);
        public bool IsUnstaged { get; set; } = false;

        public bool IsComparingWithStaged
        {
            get => _isComparingWithStaged;
            set
            {
                if (SetProperty(ref _isComparingWithStaged, value))
                {
                    if (value && StagedImage != null)
                    {
                        _old = StagedImage;
                        _oldFileSize = StagedFileSize;
                    }
                    else
                    {
                        _old = HeadImage;
                        _oldFileSize = HeadFileSize;
                    }

                    _detectionResult = null;
                    OnPropertyChanged(nameof(Old));
                    OnPropertyChanged(nameof(OldFileSize));
                    OnPropertyChanged(nameof(OldImageSize));
                    OnPropertyChanged(nameof(OldBadgeTitle));
                    OnPropertyChanged(nameof(DetectionResult));
                    OnPropertyChanged(nameof(ChangeOutlines));
                    OnPropertyChanged(nameof(ChangeCount));
                    OnPropertyChanged(nameof(ChangedPixelCount));
                    OnPropertyChanged(nameof(ChangedPixelPercentage));
                    OnPropertyChanged(nameof(DiffPixelStatsText));
                    OnPropertyChanged(nameof(DiffAreaStatsText));
                }
            }
        }

        public string OldBadgeTitle => _isComparingWithStaged ? "STAGED" : "OLD";

        public ImageDiffDetectionResult DetectionResult => _detectionResult ??= ImageDifferenceDetector.Detect(Old, New);

        public IReadOnlyList<Avalonia.Rect> ChangeOutlines => DetectionResult.ChangeBoxes;
        public int ChangeCount => DetectionResult.ChangeBoxes.Count;
        public long ChangedPixelCount => DetectionResult.ChangedPixels;
        public double ChangedPixelPercentage => DetectionResult.ChangedPercentage;

        public string DiffPixelStatsText => $"{ChangedPixelCount:N0} px ({ChangedPixelPercentage:F2}%)";
        public string DiffAreaStatsText => ChangeCount == 1 ? " · 1 area" : $" · {ChangeCount} areas";

        private Bitmap _old = null;
        private Bitmap _new = null;
        private long _oldFileSize = 0;
        private long _newFileSize = 0;
        private bool _isComparingWithStaged = false;
        private ImageDiffDetectionResult _detectionResult = null;
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
