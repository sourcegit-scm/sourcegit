namespace SourceGit.Models
{
    public class Hyperlink
    {
        public int Start { get; set; } = 0;
        public int Length { get; set; } = 0;
        public string Link { get; set; } = "";
        public bool IsCommitSHA { get; set; } = false;

        public Hyperlink(int start, int length, string link, bool isCommitSHA = false)
        {
            Start = start;
            Length = length;
            Link = link;
            IsCommitSHA = isCommitSHA;
        }

        public bool Intersect(int start, int length)
        {
            if (start == Start)
                return true;

            if (start < Start)
                return start + length > Start;

            return start < Start + Length;
        }
    }
}
