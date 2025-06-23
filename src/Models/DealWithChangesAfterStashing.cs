using System.Collections.Generic;

namespace SourceGit.Models
{
    public class DealWithChangesAfterStashing
    {
        public string Label { get; set; }
        public string Desc { get; set; }

        public static readonly List<DealWithChangesAfterStashing> Supported = [
            new ("Discard", "All (or selected) changes will be discarded"),
            new ("Keep Index", "Staged changes are left intact"),
            new ("Keep All", "All (or selected) changes are left intact"),
        ];

        public DealWithChangesAfterStashing(string label, string desc)
        {
            Label = label;
            Desc = desc;
        }
    }
}
