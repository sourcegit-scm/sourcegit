using System;
using System.Collections.Concurrent;

namespace SourceGit.Models
{
    public class User
    {
        public static readonly User Invalid = new User();

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public User()
        {
            // Only used by User.Invalid
        }

        public User(string data)
        {
            var nameEndIdx = data.IndexOf('±', StringComparison.Ordinal);

            Name = nameEndIdx > 0 ? data.Substring(0, nameEndIdx) : string.Empty;
            Email = data.Substring(nameEndIdx + 1);
            _hash = data.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is User))
                return false;

            var other = obj as User;
            return Name == other.Name && Email == other.Email;
        }

        public override int GetHashCode()
        {
            return _hash;
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
        private readonly int _hash;
    }
}
