namespace SourceGit.ViewModels
{
    public class CustomActionContextMenuLabel(string name, bool isGlobal)
    {
        public string Name { get; set; } = name;
        public bool IsGlobal { get; set; } = isGlobal;
    }
}
