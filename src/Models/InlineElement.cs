namespace SourceGit.Models
{
    public enum InlineElementType
    {
        Keyword = 0,
        Link,
        CommitSHA,
        Code,
    }

    public class InlineElement
    {
        public InlineElementType Type { get; }
        public int Start { get; }
        public int Length { get; }
        public string Link { get; }

        public InlineElement(InlineElementType type, int start, int length, string link)
        {
            Type = type;
            Start = start;
            Length = length;
            Link = link;
        }

        public bool IsIntersecting(int start, int length)
        {
            if (start == Start)
                return true;

            if (start < Start)
                return start + length > Start;

            return start < Start + Length;
        }
    }
}
