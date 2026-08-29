using System.IO;

using DevBoard.DevSpaces;
using DevBoard.Models;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class DevSpacesTerminalLayoutTests
    {
        [Fact]
        public void ListModeStacksEverySessionInOneColumn()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                spaces.CreateTerminal();
                spaces.CreateTerminal();
                spaces.CreateTerminal();

                spaces.TerminalDisplayMode = DevSpaceTerminalDisplayMode.List;

                Assert.True(spaces.IsListLayout);
                Assert.False(spaces.IsGridLayout);
                Assert.Equal(3, spaces.GridRows);
                Assert.Equal(1, spaces.GridColumns);
                Assert.Equal(3, spaces.VisibleSlots.Count);
                Assert.All(spaces.VisibleSlots, slot => Assert.NotNull(slot.Terminal));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void SwitchingBackToGridKeepsSelectedGridLayoutAndSessions()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                spaces.Layout = DevSpaceLayout.TwoByTwo;
                var first = spaces.CreateTerminal();
                var second = spaces.CreateTerminal();

                spaces.TerminalDisplayMode = DevSpaceTerminalDisplayMode.List;
                spaces.TerminalDisplayMode = DevSpaceTerminalDisplayMode.Grid;

                Assert.Equal(DevSpaceLayout.TwoByTwo, spaces.Layout);
                Assert.Equal(2, spaces.GridRows);
                Assert.Equal(2, spaces.GridColumns);
                Assert.Equal(2, spaces.Sessions.Count);
                Assert.Same(first, spaces.Sessions[0]);
                Assert.Same(second, spaces.Sessions[1]);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"sourcegit-terminal-layout-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class FakeLauncher : IDevSpaceSessionLauncher
        {
            public DevSpaceLaunchSpec Create(string terminal, string workingDirectory, string startupCommand = null)
            {
                return new DevSpaceLaunchSpec(terminal ?? string.Empty, [], workingDirectory);
            }
        }
    }
}
