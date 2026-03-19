using SourceGit.Utilities;

namespace SourceGit.Tests;

public class OFPAFilePrefixReaderTests
{
    [Fact]
    public void Read_WithLargeFile_ReturnsBoundedPrefix()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = new byte[OFPAParser.MaxSampleSize + 1024];
            new Random(1234).NextBytes(data);
            File.WriteAllBytes(tempFile, data);

            var prefix = OFPAFilePrefixReader.Read(tempFile, OFPAParser.MaxSampleSize);

            Assert.NotNull(prefix);
            Assert.Equal(OFPAParser.MaxSampleSize, prefix!.Length);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
