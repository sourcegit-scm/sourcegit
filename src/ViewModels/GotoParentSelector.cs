using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    public class GotoParentSelector
    {
        public List<Models.Commit> Parents
        {
            get;
        }

        public GotoParentSelector(Histories owner, List<Models.Commit> parents)
        {
            Parents = parents;
            _owner = owner;
        }

        public void Sure(Models.Commit commit)
        {
            _owner.NavigateTo(commit.SHA);
        }

        private Histories _owner;
    }
}
