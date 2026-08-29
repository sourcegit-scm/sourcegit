using System.IO;
using System.Linq;
using System.Threading.Tasks;

using DevBoard.DevSpaces;
using DevBoard.Models;
using DevBoard.ViewModels;

using Xunit;

namespace DevBoard.Tests
{
    public sealed class DevSpacesDashboardTests
    {
        [Fact]
        public void DashboardIsDefaultAndNavigationUsesSinglePageState()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());

                Assert.Equal(DevSpacePage.Dashboard, spaces.ActivePage);
                Assert.True(spaces.IsDashboardActive);
                Assert.False(spaces.IsFilesActive);
                Assert.False(spaces.IsTerminalsActive);
                Assert.False(spaces.IsRoslynActive);

                spaces.ActivateFiles();
                Assert.Equal(DevSpacePage.Files, spaces.ActivePage);
                spaces.ActivateTerminals();
                Assert.Equal(DevSpacePage.Terminals, spaces.ActivePage);
                spaces.ActivateRoslyn();
                Assert.Equal(DevSpacePage.Roslyn, spaces.ActivePage);
                spaces.ActivateDashboard();
                Assert.Equal(DevSpacePage.Dashboard, spaces.ActivePage);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void AutomaticFirstSessionKeepsDashboardAsLandingPage()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());

                spaces.EnsureFirstSession();

                Assert.Single(spaces.Sessions);
                Assert.NotNull(spaces.ActiveTerminal);
                Assert.Equal(DevSpacePage.Dashboard, spaces.ActivePage);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void GitSummaryCanNavigateToWorkingCopy()
        {
            var root = CreateTempDirectory();
            var gitDir = Path.Combine(root, ".git");
            Directory.CreateDirectory(gitDir);
            try
            {
                var repository = new Repository(false, root, gitDir);
                using var spaces = new ViewModels.DevSpaces(repository, root, new FakeLauncher());

                spaces.Dashboard.OpenWorkingCopy();

                Assert.Equal(1, repository.SelectedViewIndex);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public async Task OpenFileSelectsFilesWithoutChangingSessions()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var before = spaces.Sessions.Count;

                var opened = spaces.OpenFile("missing-file.cs");

                Assert.False(opened);
                Assert.Equal(DevSpacePage.Files, spaces.ActivePage);
                Assert.Equal(before, spaces.Sessions.Count);
                await spaces.Files.InitialRefreshTask;
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void QuickStartAndSessionSelectionReuseExistingSessionObjects()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var created = spaces.Dashboard.StartDefaultTerminal();

                Assert.Single(spaces.Sessions);
                Assert.Same(created, spaces.ActiveTerminal);
                Assert.Equal(DevSpacePage.Terminals, spaces.ActivePage);

                spaces.ActivateDashboard();
                spaces.Dashboard.OpenSession(created);

                Assert.Single(spaces.Sessions);
                Assert.Same(created, spaces.ActiveTerminal);
                Assert.Equal(DevSpacePage.Terminals, spaces.ActivePage);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Theory]
        [InlineData("Codex", "codex")]
        [InlineData("Antigravity", "agy")]
        public void AgentQuickStartUsesBuiltInCommandMapping(string name, string command)
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var agent = DevSpaceAgent.BuiltIn.Single(x => x.Name == name);

                var created = spaces.Dashboard.StartAgent(agent);

                Assert.Equal(command, created.StartupCommand);
                Assert.Equal(DevSpacePage.Terminals, spaces.ActivePage);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void DashboardCanCloseSingleSessionThroughExistingLifecycle()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                var first = spaces.Dashboard.StartDefaultTerminal();
                var second = spaces.Dashboard.StartDefaultTerminal();

                spaces.Dashboard.CloseSession(first);

                Assert.Single(spaces.Sessions);
                Assert.DoesNotContain(first, spaces.Sessions);
                Assert.Same(second, spaces.ActiveTerminal);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void CloseAllDelegatesToExistingSessionLifecycle()
        {
            var root = CreateTempDirectory();
            try
            {
                using var spaces = new ViewModels.DevSpaces(root, new FakeLauncher());
                spaces.Dashboard.StartDefaultTerminal();
                spaces.Dashboard.StartDefaultTerminal();

                spaces.Dashboard.CloseAllSessions();

                Assert.Empty(spaces.Sessions);
                Assert.Null(spaces.ActiveTerminal);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void DashboardActivityIsIsolatedByWorkspaceInstance()
        {
            var firstRoot = CreateTempDirectory();
            var secondRoot = CreateTempDirectory();
            try
            {
                using var first = new ViewModels.DevSpaces(firstRoot, new FakeLauncher());
                using var second = new ViewModels.DevSpaces(secondRoot, new FakeLauncher());

                first.Dashboard.AddActivity(DevSpaceActivityKind.FileOpened, "first.cs");

                Assert.Single(first.Dashboard.Activity);
                Assert.Empty(second.Dashboard.Activity);
            }
            finally
            {
                Directory.Delete(firstRoot, true);
                Directory.Delete(secondRoot, true);
            }
        }

        [Fact]
        public void ToolHealthFindsCommandsFromProvidedPathAndReturnsUnavailableOtherwise()
        {
            var root = CreateTempDirectory();
            try
            {
                var command = System.OperatingSystem.IsWindows() ? "dashboard-tool.cmd" : "dashboard-tool";
                File.WriteAllText(Path.Combine(root, command), string.Empty);

                Assert.Equal(DevSpaceCapabilityState.Available, DevSpaceToolHealth.CheckCommand("dashboard-tool", root));
                Assert.Equal(DevSpaceCapabilityState.Unavailable, DevSpaceToolHealth.CheckCommand("missing-dashboard-tool", root));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"devboard-dashboard-{System.Guid.NewGuid():N}");
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
