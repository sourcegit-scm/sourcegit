using System;

namespace SourceGit.Models
{
    public static class NumericSort
    {
        public static int Compare(string s1, string s2)
        {
            var comparer = StringComparer.InvariantCultureIgnoreCase;

            int len1 = s1.Length;
            int len2 = s2.Length;

            int marker1 = 0;
            int marker2 = 0;

            while (marker1 < len1 && marker2 < len2)
            {
                char c1 = s1[marker1];
                char c2 = s2[marker2];

                bool isDigit1 = char.IsDigit(c1);
                bool isDigit2 = char.IsDigit(c2);
                if (isDigit1 != isDigit2)
                    return comparer.Compare(c1.ToString(), c2.ToString());

                int subLen1 = 1;
                while (marker1 + subLen1 < len1 && char.IsDigit(s1[marker1 + subLen1]) == isDigit1)
                    subLen1++;

                int subLen2 = 1;
                while (marker2 + subLen2 < len2 && char.IsDigit(s2[marker2 + subLen2]) == isDigit2)
                    subLen2++;

                string sub1 = s1.Substring(marker1, subLen1);
                string sub2 = s2.Substring(marker2, subLen2);

                marker1 += subLen1;
                marker2 += subLen2;

                int result;
                if (isDigit1)
                    result = (subLen1 == subLen2) ? string.CompareOrdinal(sub1, sub2) : (subLen1 - subLen2);
                else
                    result = comparer.Compare(sub1, sub2);

                if (result != 0)
                    return result;
            }

            return len1 - len2;
        }
    }
}
