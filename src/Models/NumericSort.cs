namespace SourceGit.Models
{
    public static class NumericSort
    {
        public static int Compare(string s1, string s2)
        {
            int len1 = s1.Length;
            int len2 = s2.Length;

            int marker1 = 0;
            int marker2 = 0;

            while (marker1 < len1 && marker2 < len2)
            {
                char c1 = s1[marker1];
                char c2 = s2[marker2];

                char[] space1 = new char[len1];
                char[] space2 = new char[len2];

                int loc1 = 0;
                int loc2 = 0;

                bool isDigit1 = char.IsDigit(c1);
                do
                {
                    space1[loc1] = c1;
                    loc1++;
                    marker1++;

                    if (marker1 < len1)
                        c1 = s1[marker1];
                    else
                        break;
                } while (char.IsDigit(c1) == isDigit1);

                bool isDigit2 = char.IsDigit(c2);
                do
                {
                    space2[loc2] = c2;
                    loc2++;
                    marker2++;

                    if (marker2 < len2)
                        c2 = s2[marker2];
                    else
                        break;
                } while (char.IsDigit(c2) == isDigit2);

                string sub1 = new string(space1, 0, loc1);
                string sub2 = new string(space2, 0, loc2);

                int result;
                if (isDigit1 && isDigit2)
                {
                    int num1 = int.Parse(sub1);
                    int num2 = int.Parse(sub2);
                    result = num1 - num2;
                }
                else
                {
                    result = string.Compare(sub1, sub2, System.StringComparison.Ordinal);
                }

                if (result != 0)
                    return result;
            }

            return len1 - len2;
        }
    }
}
