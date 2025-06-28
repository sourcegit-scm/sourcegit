using System.Collections.Generic;

namespace SourceGit.Models
{
    public class DealWithChangesAfterStashing(string label, string desc)
    {
        public string Label { get; set; } = label;
        public string Desc { get; set; } = desc;

        public static readonly List<DealWithChangesAfterStashing> Supported = [
            new ("Discard", "All (or selected) changes will be discarded"),
            new ("Keep Index", "Staged changes are left intact"),
            new ("Keep All", "All (or selected) changes are left intact"),
        ];
    }
}
