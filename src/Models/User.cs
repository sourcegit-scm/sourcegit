using System.Collections.Concurrent;

namespace SourceGit.Models
{
    public class User
    {
        public static readonly User Invalid = new User();

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is User))
                return false;

            var other = obj as User;
            return Name == other.Name && Email == other.Email;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static User FindOrAdd(string data)
        {
            if (_caches.TryGetValue(data, out var value))
            {
                return value;
            }
            else
            {
                var nameEndIdx = data.IndexOf('<', System.StringComparison.Ordinal);
                var name = nameEndIdx >= 2 ? data.Substring(0, nameEndIdx - 1) : string.Empty;
                var email = data.Substring(nameEndIdx + 1);

                User user = new User() { Name = name, Email = email };
                _caches.TryAdd(data, user);
                return user;
            }
        }

        private static ConcurrentDictionary<string, User> _caches = new ConcurrentDictionary<string, User>();
    }
}
