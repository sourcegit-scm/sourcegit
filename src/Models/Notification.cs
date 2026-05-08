using System;

namespace SourceGit.Models
{
    public class Notification
    {
        public static event Action<Notification> Raised;

        public string Group { get; set; }
        public string Message { get; set; }
        public bool IsError { get; set; }

        public static void Send(string group, string message, bool isError = false)
        {
            Raised?.Invoke(new Notification
            {
                Group = group,
                Message = message,
                IsError = isError
            });
        }
    }
}
