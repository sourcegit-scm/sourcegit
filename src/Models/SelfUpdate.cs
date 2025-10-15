using System;
using System.Reflection;
using System.Text.Json.Serialization;

namespace SourceGit.Models
{
    public class Version
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        public bool IsNewVersion
        {
            get
            {
                try
                {
                    System.Version version = new System.Version(TagName.Substring(1));
                    System.Version current = Assembly.GetExecutingAssembly().GetName().Version!;
                    return current.CompareTo(version) < 0;
                }
                catch
                {
                    return false;
                }
            }
        }
    }

    public class AlreadyUpToDate;

    public class SelfUpdateFailed
    {
        public string Reason
        {
            get;
            private set;
        }

        public SelfUpdateFailed(Exception e)
        {
            if (e.InnerException is { } inner)
                Reason = inner.Message;
            else
                Reason = e.Message;
        }
    }
}
