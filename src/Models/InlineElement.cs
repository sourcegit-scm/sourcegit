namespace SourceGit.Models
{
    public enum InlineElementType
    {
        None = 0,
        Keyword,
        Link,
        CommitSHA,
        Code,
    }

    public class InlineElement
    {
        public InlineElementType Type { get; set; } = InlineElementType.None;
        public int Start { get; set; } = 0;
        public int Length { get; set; } = 0;
        public string Link { get; set; } = "";

        public InlineElement(InlineElementType type, int start, int length, string link)
        {
            Type = type;
            Start = start;
            Length = length;
            Link = link;
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
