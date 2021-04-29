using System;

namespace SourceGit.Models {

    /// <summary>
    ///     错误通知
    /// </summary>
    public static class Exception {
        public static Action<string> Handler { get; set; }

        public static void Raise(string error) {
            Handler?.Invoke(error);
        }
    }
}
