using System;

namespace SourceGit.Models
{
    public class Count : IDisposable
    {
        public int Value { get; set; }

        public Count(int value)
        {
            Value = value;
        }

        public void Dispose()
        {
            // Ignore
        }
    }
}
