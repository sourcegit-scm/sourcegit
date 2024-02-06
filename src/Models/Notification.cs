namespace SourceGit.Models {
    public class Notification {
        public bool IsError { get; set; } = false;
        public string Message { get; set; } = string.Empty;
    }

    public interface INotificationReceiver {
        void OnReceiveNotification(string ctx, Notification notice);
    }
}
