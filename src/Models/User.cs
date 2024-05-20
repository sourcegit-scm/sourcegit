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
            return _caches.GetOrAdd(data, key =>
            {
                var nameEndIdx = key.IndexOf('<', System.StringComparison.Ordinal);
                var name = nameEndIdx >= 2 ? key.Substring(0, nameEndIdx - 1) : string.Empty;
                var email = key.Substring(nameEndIdx + 1);

                return new User() { Name = name, Email = email };
            });
        }

        private static ConcurrentDictionary<string, User> _caches = new ConcurrentDictionary<string, User>();
    }
}
