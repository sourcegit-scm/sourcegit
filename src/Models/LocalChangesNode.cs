namespace SourceGit.Models
{
    /// <summary>
    ///     Pseudo commit used to display local changes as a node (not yet attached) in the commit graph.
    /// </summary>
    public class LocalChangesNode : Commit
    {
        public const string NodeSHA = "LOCALCHANGES0000000000000000000000000000";

        public override bool IsLocalChanges => true;

        public int Count
        {
            get => _count;
            set
            {
                if (SetProperty(ref _count, value))
                {
                    // The localized label is provided by view (so it follows locale changes), subject only holds the count.
                    Subject = $"({value})";
                    OnPropertyChanged(nameof(Subject));
                }
            }
        }

        public LocalChangesNode()
        {
            SHA = NodeSHA;
            IsMerged = true;
        }

        private int _count = 0;
    }
}
