namespace SourceGit.Models
{
    public interface ICommandLogReceiver
    {
        void OnReceiveCommandLog(string line);
    }

    public interface ICommandLog
    {
        void AppendLine(string line);
    }
}
