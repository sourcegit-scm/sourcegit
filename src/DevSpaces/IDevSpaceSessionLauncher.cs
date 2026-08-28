namespace SourceGit.DevSpaces
{
    public readonly record struct DevSpaceLaunchSpec(
        string Process,
        string[] Arguments,
        string WorkingDirectory);

    public interface IDevSpaceSessionLauncher
    {
        DevSpaceLaunchSpec Create(string command, string workingDirectory);
    }
}
