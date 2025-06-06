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

            char[] tmp = new char[Math.Max(len1, len2)];

            while (marker1 < len1 && marker2 < len2)
            {
                char c1 = s1[marker1];
                char c2 = s2[marker2];

                bool isDigit1 = char.IsDigit(c1);
                bool isDigit2 = char.IsDigit(c2);
                if (isDigit1 != isDigit2)
                    return c1.CompareTo(c2);

                int subLen1 = GetCoherentSubstringLength(s1, len1, marker1, isDigit1, ref tmp);
                int subLen2 = GetCoherentSubstringLength(s2, len2, marker2, isDigit2, ref tmp);

                string sub1 = s1.Substring(marker1, subLen1);
                string sub2 = s2.Substring(marker2, subLen2);

                marker1 += subLen1;
                marker2 += subLen2;

                int result;
                if (isDigit1)
                {
                    // NOTE: We don't strip leading zeroes before comparing substring digits/lengths - should we?
                    result = (subLen1 == subLen2) ? string.CompareOrdinal(sub1, sub2) : (subLen1 - subLen2);
                }
                else
                {
                    result = string.CompareOrdinal(sub1, sub2);
                }
                if (result != 0)
                    return result;
            }

            return len1 - len2;
        }

        private static int GetCoherentSubstringLength(string s, int len, int start, bool isDigit, ref char[] tmp)
        {
            int num = 1;
            while (start + num < len && char.IsDigit(s[start + num]) == isDigit)
                num++;
            return num;
        }
    }
}
