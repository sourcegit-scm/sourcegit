using System;

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

                bool isDigit1 = char.IsDigit(c1);
                bool isDigit2 = char.IsDigit(c2);
                if (isDigit1 != isDigit2)
                    return s1.AsSpan(marker1, 1).CompareTo(s2.AsSpan(marker2, 1), StringComparison.InvariantCultureIgnoreCase);

                int subLen1 = 1;
                while (marker1 + subLen1 < len1 && char.IsDigit(s1[marker1 + subLen1]) == isDigit1)
                    subLen1++;

                int subLen2 = 1;
                while (marker2 + subLen2 < len2 && char.IsDigit(s2[marker2 + subLen2]) == isDigit2)
                    subLen2++;

                if (isDigit1 && subLen1 != subLen2)
                    return subLen1 - subLen2;

                var sub1 = s1.AsSpan(marker1, subLen1);
                var sub2 = s2.AsSpan(marker2, subLen2);

                marker1 += subLen1;
                marker2 += subLen2;

                int result = 0;
                if (isDigit1)
                    result = sub1.SequenceCompareTo(sub2);
                else
                    result = sub1.CompareTo(sub2, StringComparison.InvariantCultureIgnoreCase);

                if (result != 0)
                    return result;
            }

            return len1 - len2;
        }
    }
}
