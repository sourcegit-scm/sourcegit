using SourceGit.Native;
using Xunit;

namespace SourceGit.Tests
{
    public class WSLTests
    {
        [Theory]
        [InlineData("//wsl.localhost/Ubuntu/home/me/repo", "Ubuntu", "/home/me/repo")]
        [InlineData(@"\\wsl.localhost\Ubuntu\home\me\repo", "Ubuntu", "/home/me/repo")]
        [InlineData("//WSL$/Ubuntu-26.04/home/me/my repo/", "Ubuntu-26.04", "/home/me/my repo")]
        [InlineData("//wsl.localhost/Ubuntu", "Ubuntu", "/")]
        [InlineData("//wsl.localhost/Ubuntu/", "Ubuntu", "/")]
        public void ParsesWslPaths(string path, string distro, string linux)
        {
            Assert.True(WSL.TryParse(path, out var d, out var l));
            Assert.Equal(distro, d);
            Assert.Equal(linux, l);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(@"C:\Users\me\repo")]
        [InlineData("//server/share/repo")]
        [InlineData("//wsl.localhost/")]
        [InlineData("/home/me/repo")]
        public void RejectsNonWslPaths(string path)
        {
            Assert.False(WSL.TryParse(path, out _, out _));
        }

        [Theory]
        [InlineData("//wsl.localhost/Ubuntu/home/me/repo", "/home/me/repo")]
        [InlineData(@"\\wsl$\ubuntu\home\me", "/home/me")] // distro names compare case-insensitively
        [InlineData(@"C:\Users\Me\a b\SourceGit.exe", "/mnt/c/Users/Me/a b/SourceGit.exe")]
        [InlineData(@"D:\", "/mnt/d")]
        [InlineData("/already/linux", "/already/linux")]
        public void ConvertsToLinux(string path, string expected)
        {
            Assert.Equal(expected, WSL.ToLinux("Ubuntu", path));
        }

        [Fact]
        public void KeepsOtherDistroPathUntouched()
        {
            Assert.Equal("//wsl.localhost/Debian/home/me", WSL.ToLinux("Ubuntu", "//wsl.localhost/Debian/home/me"));
        }

        [Theory]
        [InlineData("/home/me/repo", "//wsl.localhost/Ubuntu/home/me/repo")]
        [InlineData("/", "//wsl.localhost/Ubuntu/")]
        [InlineData("/mnt/c/Users/Me", "C:/Users/Me")]
        [InlineData("/mnt/c", "C:/")]
        [InlineData("/mnt/wsl/foo", "//wsl.localhost/Ubuntu/mnt/wsl/foo")] // not a drive mount
        public void ConvertsToWindows(string linux, string expected)
        {
            Assert.Equal(expected, WSL.ToWindows("Ubuntu", linux));
        }

        [Fact]
        public void ParsesDistroList()
        {
            // wsl.exe -l -q output as decoded from UTF-16LE: BOM, CRLF, NUL padding, blank lines, helper distros.
            var output = "\uFEFFUbuntu-26.04\r\nDebian\0\r\ndocker-desktop\r\ndocker-desktop-data\r\n\r\n";
            Assert.Equal(["Ubuntu-26.04", "Debian"], WSL.ParseDistros(output));
            Assert.Empty(WSL.ParseDistros(string.Empty));
        }

        [Fact]
        public void ResolvesCredentialHelperFromOverride()
        {
            try
            {
                WSL.CredentialHelperOverride = d => d == "Ubuntu" ? "  store " : string.Empty;
                Assert.Equal("store", WSL.CredentialHelperFor("Ubuntu"));
            }
            finally
            {
                WSL.CredentialHelperOverride = null;
            }
        }

        [Fact]
        public void UnknownDistroHasNoHomeDirectory()
        {
            Assert.Equal(string.Empty, WSL.GetHomeDirectory("no-such-distro-for-sourcegit-tests"));
        }

        [Fact]
        public void ReadsHomeDirectoryFromInstalledDistro()
        {
            // Needs a real distro, set WSL_TEST_DISTRO to run it.
            var distro = System.Environment.GetEnvironmentVariable("WSL_TEST_DISTRO");
            if (string.IsNullOrEmpty(distro))
                return;

            var home = WSL.GetHomeDirectory(distro);
            Assert.StartsWith("/", home);
            Assert.True(System.IO.Directory.Exists(WSL.ToWindows(distro, home)));
        }

        [Fact]
        public void UnknownDistroHasNoGitVersion()
        {
            Assert.Equal(new System.Version(0, 0, 0), WSL.GetGitVersion("no-such-distro-for-sourcegit-tests"));
        }

        [Fact]
        public void ReadsGitVersionFromInstalledDistro()
        {
            // Needs a real distro, set WSL_TEST_DISTRO to run it.
            var distro = System.Environment.GetEnvironmentVariable("WSL_TEST_DISTRO");
            if (string.IsNullOrEmpty(distro))
                return;

            var version = WSL.GetGitVersion(distro);
            Assert.True(version >= new System.Version(2, 25, 1), version.ToString());
            Assert.Same(version, WSL.GetGitVersion(distro)); // second call is served from the cache
        }

        [Fact]
        public void RoundTripsUncPaths()
        {
            const string unc = "//wsl.localhost/Ubuntu/home/me/my repo";
            Assert.Equal(unc, WSL.ToWindows("Ubuntu", WSL.ToLinux("Ubuntu", unc)));
        }
    }
}
