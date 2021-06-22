using System.Collections.Generic;

namespace SourceGit.Models {

    /// <summary>
    ///     支持的头像服务器
    /// </summary>
    public class AvatarServer {
        public string Name { get; set; }
        public string Url { get; set; }

        public static List<AvatarServer> Supported = new List<AvatarServer>() {
            new AvatarServer("Gravatar", "https://www.gravatar.com/avatar/"),
            new AvatarServer("Gravatar - 极客族", "https://sdn.geekzu.org/avatar/"),
        };

        public AvatarServer(string name, string url) {
            Name = name;
            Url = url;
        }
    }
}
