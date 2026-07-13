using System.Collections.Concurrent;

namespace SourceGit.Models
{
    public class User
    {
        public static readonly User Invalid = new User(string.Empty);

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public User(string data)
        {
            var parts = data.Split('±', 2);
            if (parts.Length < 2)
            {
                Email = data;
            }
            else
            {
                Name = parts[0];
                Email = parts[1];
            }
        }

        public static User FindOrAdd(string data)
        {
            return _caches.GetOrAdd(data, key => new User(key));
        }

        public override string ToString()
        {
            return $"{Name} <{Email}>";
        }

        private static ConcurrentDictionary<string, User> _caches = new ConcurrentDictionary<string, User>();
    }
}
