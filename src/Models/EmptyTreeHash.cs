namespace SourceGit.Models
{
    public static class EmptyTreeHash
    {
        public static string Guess(string revision)
        {
            return revision.Length == 40 ? SHA1 : SHA256;
        }

        private const string SHA1 = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";
        private const string SHA256 = "6ef19b41225c5369f1c104d45d8d85efa9b057b53b14b4b9b939dd74decc5321";
    }
}
