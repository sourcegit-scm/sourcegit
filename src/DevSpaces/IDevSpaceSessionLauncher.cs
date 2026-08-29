using System.Collections.Generic;

namespace SourceGit.DevSpaces
{
    public readonly record struct DevSpaceLaunchSpec(
        string Process,
        string[] Arguments,
        string WorkingDirectory,
        IReadOnlyDictionary<string, string> Environment = null);

    public interface IDevSpaceSessionLauncher
    {
        DevSpaceLaunchSpec Create(string terminal, string workingDirectory, string startupCommand = null);
    }
}
