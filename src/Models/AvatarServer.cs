using System.Collections.Generic;

namespace SourceGit.Models {

    /// <summary>
    ///     支持的头像服务器
    /// </summary>
    public class AvatarServer {
        public string Name { get; set; }
        public string Url { get; set; }

        public static List<AvatarServer> Supported = new List<AvatarServer>() {
            new AvatarServer("Gravatar官网", "https://www.gravatar.com/avatar/"),
            new AvatarServer("Gravatar中国CDN", "https://cdn.s.loli.top/avatar/"),
        };

        public AvatarServer(string name, string url) {
            Name = name;
            Url = url;
        }
    }
}
