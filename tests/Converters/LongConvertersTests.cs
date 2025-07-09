using System.Globalization;
using SourceGit.Converters;

namespace SourceGit.Tests.Converters
{
    public class LongConvertersTests
    {
        [Theory]
        [InlineData(4, "4 B")]
        [InlineData(44, "44 B")]
        [InlineData(444, "444 B")]
        [InlineData(4444, "4.34 KB (4,444 B)")]
        [InlineData(44444, "43.4 KB (44,444 B)")]
        [InlineData(444444, "434 KB (444,444 B)")]
        [InlineData(4444444, "4.24 MB (4,444,444 B)")]
        [InlineData(44444444, "42.4 MB (44,444,444 B)")]
        [InlineData(444444444, "424 MB (444,444,444 B)")]
        [InlineData(4444444444, "4.14 GB (4,444,444,444 B)")]
        [InlineData(44444444444, "41.4 GB (44,444,444,444 B)")]
        [InlineData(444444444444, "414 GB (444,444,444,444 B)")]
        [InlineData(4444444444444, "4,139 GB (4,444,444,444,444 B)")]
        [InlineData(long.MinValue, "-9,223,372,036,854,775,808 B")]
        [InlineData(0, "0 B")]
        [InlineData(long.MaxValue, "8,589,934,592 GB (9,223,372,036,854,775,807 B)")]
        public void ToFileSize_ShouldReturnCorrectFormat(long bytes, string expected)
        {
            var actual = LongConverters.ToFileSize.Convert(bytes, typeof(string), null, CultureInfo.CurrentCulture) as string;
            Assert.Equal(expected, actual);
        }
    }
}
