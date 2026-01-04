using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    public interface ICommitTreeNode
    {
        IEnumerable<ICommitTreeNode> Children { get; }
        string CommitSHA { get; }
        bool IsSelected { get; }
    }
}
