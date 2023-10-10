using System.Collections.Generic;

namespace SourceGit.Models {
    /// <summary>
    ///     Git用户
    /// </summary>
    public class User {
        public static User Invalid = new User();
        public static Dictionary<string, User> Caches = new Dictionary<string, User>();

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public override bool Equals(object obj) {
            if (obj == null || !(obj is User)) return false; 
            
            var other = obj as User;
            return Name == other.Name && Email == other.Email;
        }

        public override int GetHashCode() {
            return base.GetHashCode();
        }

        public static User FindOrAdd(string name, string email) {
            string key = $"{name}#&#{email}";
            if (Caches.ContainsKey(key)) {
                return Caches[key];
            } else {
                User user = new User() { Name = name, Email = email };
                Caches.Add(key, user);
                return user;
            }
        }
    }
}
