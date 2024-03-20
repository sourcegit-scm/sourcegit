namespace SourceGit.Models
{
    public class ApplyWhiteSpaceMode
    {
        public string Name { get; set; }
        public string Desc { get; set; }
        public string Arg { get; set; }

        public ApplyWhiteSpaceMode(string n, string d, string a)
        {
            Name = App.Text(n);
            Desc = App.Text(d);
            Arg = a;
        }
    }
}