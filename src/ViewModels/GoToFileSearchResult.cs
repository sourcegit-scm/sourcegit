namespace DevBoard.ViewModels
{
    public enum GoToFileMatchKind
    {
        Path,
        Content,
    }

    public sealed class GoToFileSearchResult
    {
        public string RelativePath { get; }

        public string FileName { get; }

        public GoToFileMatchKind MatchKind { get; }

        public string PreviewText { get; }

        public int Rank { get; }

        public GoToFileSearchResult(string relativePath, GoToFileMatchKind matchKind, string previewText, int rank)
        {
            RelativePath = relativePath;
            FileName = System.IO.Path.GetFileName(relativePath);
            MatchKind = matchKind;
            PreviewText = previewText ?? string.Empty;
            Rank = rank;
        }
    }
}
