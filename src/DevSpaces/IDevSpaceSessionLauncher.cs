namespace SourceGit.DevSpaces
{
    public readonly record struct DevSpaceLaunchSpec(
        string Process,
        string[] Arguments,
        string WorkingDirectory);

    public interface IDevSpaceSessionLauncher
    {
        DevSpaceLaunchSpec Create(string terminal, string workingDirectory, string startupCommand = null);
    }
}
