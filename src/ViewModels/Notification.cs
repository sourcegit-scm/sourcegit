namespace SourceGit.ViewModels
{
    public class Notification
    {
        public bool IsError { get; set; } = false;
        public string Message { get; set; } = string.Empty;

        public void CopyMessage()
        {
            App.CopyText(Message);
        }
    }
}
