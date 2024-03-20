using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class TwoSideTextDiff : ObservableObject
    {
        public string File { get; set; } = string.Empty;
        public List<Models.TextDiffLine> Old { get; set; } = new List<Models.TextDiffLine>();
        public List<Models.TextDiffLine> New { get; set; } = new List<Models.TextDiffLine>();
        public int MaxLineNumber = 0;

        public TwoSideTextDiff(Models.TextDiff diff)
        {
            File = diff.File;
            MaxLineNumber = diff.MaxLineNumber;

            foreach (var line in diff.Lines)
            {
                switch (line.Type)
                {
                    case Models.TextDiffLineType.Added:
                        New.Add(line);
                        break;
                    case Models.TextDiffLineType.Deleted:
                        Old.Add(line);
                        break;
                    default:
                        FillEmptyLines();
                        Old.Add(line);
                        New.Add(line);
                        break;
                }
            }

            FillEmptyLines();
        }

        private void FillEmptyLines()
        {
            if (Old.Count < New.Count)
            {
                int diff = New.Count - Old.Count;
                for (int i = 0; i < diff; i++) Old.Add(new Models.TextDiffLine());
            }
            else if (Old.Count > New.Count)
            {
                int diff = Old.Count - New.Count;
                for (int i = 0; i < diff; i++) New.Add(new Models.TextDiffLine());
            }
        }
    }
}