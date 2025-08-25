using System.Collections.Generic;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class RepositoryToolbar : UserControl
    {
        public RepositoryToolbar()
        {
            InitializeComponent();
        }

        private void OpenWithExternalTools(object sender, RoutedEventArgs ev)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var fullpath = repo.FullPath;
                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

                RenderOptions.SetBitmapInterpolationMode(menu, BitmapInterpolationMode.HighQuality);
                RenderOptions.SetEdgeMode(menu, EdgeMode.Antialias);
                RenderOptions.SetTextRenderingMode(menu, TextRenderingMode.Antialias);

                var explore = new MenuItem();
                explore.Header = App.Text("Repository.Explore");
                explore.Icon = App.CreateMenuIcon("Icons.Explore");
                explore.Click += (_, e) =>
                {
                    Native.OS.OpenInFileManager(fullpath);
                    e.Handled = true;
                };

                var terminal = new MenuItem();
                terminal.Header = App.Text("Repository.Terminal");
                terminal.Icon = App.CreateMenuIcon("Icons.Terminal");
                terminal.Click += (_, e) =>
                {
                    Native.OS.OpenTerminal(fullpath);
                    e.Handled = true;
                };

                menu.Items.Add(explore);
                menu.Items.Add(terminal);

                var tools = Native.OS.ExternalTools;
                if (tools.Count > 0)
                {
                    menu.Items.Add(new MenuItem() { Header = "-" });

                    foreach (var tool in tools)
                    {
                        var dupTool = tool;

                        var item = new MenuItem();
                        item.Header = App.Text("Repository.OpenIn", dupTool.Name);
                        item.Icon = new Image { Width = 16, Height = 16, Source = dupTool.IconImage };
                        item.Click += (_, e) =>
                        {
                            dupTool.Open(fullpath);
                            e.Handled = true;
                        };

                        menu.Items.Add(item);
                    }
                }

                var urls = new Dictionary<string, string>();
                foreach (var r in repo.Remotes)
                {
                    if (r.TryGetVisitURL(out var visit))
                        urls.Add(r.Name, visit);
                }

                if (urls.Count > 0)
                {
                    menu.Items.Add(new MenuItem() { Header = "-" });

                    foreach (var (name, addr) in urls)
                    {
                        var dupUrl = addr;

                        var item = new MenuItem();
                        item.Header = App.Text("Repository.Visit", name);
                        item.Icon = App.CreateMenuIcon("Icons.Remotes");
                        item.Click += (_, e) =>
                        {
                            Native.OS.OpenBrowser(dupUrl);
                            e.Handled = true;
                        };

                        menu.Items.Add(item);
                    }
                }

                menu.Open(button);
                ev.Handled = true;
            }
        }

        private async void OpenStatistics(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await App.ShowDialog(new ViewModels.Statistics(repo.FullPath));
                e.Handled = true;
            }
        }

        private async void OpenConfigure(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await App.ShowDialog(new ViewModels.RepositoryConfigure(repo));
                e.Handled = true;
            }
        }

        private async void Fetch(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await repo.FetchAsync(e.KeyModifiers is KeyModifiers.Control);
                e.Handled = true;
            }
        }

        private async void FetchDirectlyByHotKey(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await repo.FetchAsync(true);
                e.Handled = true;
            }
        }

        private async void Pull(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await repo.PullAsync(e.KeyModifiers is KeyModifiers.Control);
                e.Handled = true;
            }
        }

        private async void PullDirectlyByHotKey(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await repo.PullAsync(true);
                e.Handled = true;
            }
        }

        private async void Push(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await repo.PushAsync(e.KeyModifiers is KeyModifiers.Control);
                e.Handled = true;
            }
        }

        private async void PushDirectlyByHotKey(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await repo.PushAsync(true);
                e.Handled = true;
            }
        }

        private async void StashAll(object _, TappedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await repo.StashAllAsync(e.KeyModifiers is KeyModifiers.Control);
                e.Handled = true;
            }
        }

        private void OpenGitFlowMenu(object sender, RoutedEventArgs ev)
        {
            if (DataContext is ViewModels.Repository repo && sender is Control control)
            {
                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

                if (repo.IsGitFlowEnabled())
                {
                    var startFeature = new MenuItem();
                    startFeature.Header = App.Text("GitFlow.StartFeature");
                    startFeature.Icon = App.CreateMenuIcon("Icons.GitFlow.Feature");
                    startFeature.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.GitFlowStart(repo, Models.GitFlowBranchType.Feature));
                        e.Handled = true;
                    };

                    var startRelease = new MenuItem();
                    startRelease.Header = App.Text("GitFlow.StartRelease");
                    startRelease.Icon = App.CreateMenuIcon("Icons.GitFlow.Release");
                    startRelease.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.GitFlowStart(repo, Models.GitFlowBranchType.Release));
                        e.Handled = true;
                    };

                    var startHotfix = new MenuItem();
                    startHotfix.Header = App.Text("GitFlow.StartHotfix");
                    startHotfix.Icon = App.CreateMenuIcon("Icons.GitFlow.Hotfix");
                    startHotfix.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.GitFlowStart(repo, Models.GitFlowBranchType.Hotfix));
                        e.Handled = true;
                    };

                    menu.Items.Add(startFeature);
                    menu.Items.Add(startRelease);
                    menu.Items.Add(startHotfix);
                }
                else
                {
                    var init = new MenuItem();
                    init.Header = App.Text("GitFlow.Init");
                    init.Icon = App.CreateMenuIcon("Icons.Init");
                    init.Click += (_, e) =>
                    {
                        if (repo.CurrentBranch == null)
                            App.RaiseException(repo.FullPath, "Git flow init failed: No branch found!!!");
                        else if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.InitGitFlow(repo));

                        e.Handled = true;
                    };
                    menu.Items.Add(init);
                }

                menu.Open(control);
            }

            ev.Handled = true;
        }

        private void OpenGitLFSMenu(object sender, RoutedEventArgs ev)
        {
            if (DataContext is ViewModels.Repository repo && sender is Control control)
            {
                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

                if (repo.IsLFSEnabled())
                {
                    var addPattern = new MenuItem();
                    addPattern.Header = App.Text("GitLFS.AddTrackPattern");
                    addPattern.Icon = App.CreateMenuIcon("Icons.File.Add");
                    addPattern.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.LFSTrackCustomPattern(repo));

                        e.Handled = true;
                    };
                    menu.Items.Add(addPattern);
                    menu.Items.Add(new MenuItem() { Header = "-" });

                    var fetch = new MenuItem();
                    fetch.Header = App.Text("GitLFS.Fetch");
                    fetch.Icon = App.CreateMenuIcon("Icons.Fetch");
                    fetch.IsEnabled = repo.Remotes.Count > 0;
                    fetch.Click += async (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                        {
                            if (repo.Remotes.Count == 1)
                                await repo.ShowAndStartPopupAsync(new ViewModels.LFSFetch(repo));
                            else
                                repo.ShowPopup(new ViewModels.LFSFetch(repo));
                        }

                        e.Handled = true;
                    };
                    menu.Items.Add(fetch);

                    var pull = new MenuItem();
                    pull.Header = App.Text("GitLFS.Pull");
                    pull.Icon = App.CreateMenuIcon("Icons.Pull");
                    pull.IsEnabled = repo.Remotes.Count > 0;
                    pull.Click += async (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                        {
                            if (repo.Remotes.Count == 1)
                                await repo.ShowAndStartPopupAsync(new ViewModels.LFSPull(repo));
                            else
                                repo.ShowPopup(new ViewModels.LFSPull(repo));
                        }

                        e.Handled = true;
                    };
                    menu.Items.Add(pull);

                    var push = new MenuItem();
                    push.Header = App.Text("GitLFS.Push");
                    push.Icon = App.CreateMenuIcon("Icons.Push");
                    push.IsEnabled = repo.Remotes.Count > 0;
                    push.Click += async (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                        {
                            if (repo.Remotes.Count == 1)
                                await repo.ShowAndStartPopupAsync(new ViewModels.LFSPush(repo));
                            else
                                repo.ShowPopup(new ViewModels.LFSPush(repo));
                        }

                        e.Handled = true;
                    };
                    menu.Items.Add(push);

                    var prune = new MenuItem();
                    prune.Header = App.Text("GitLFS.Prune");
                    prune.Icon = App.CreateMenuIcon("Icons.Clean");
                    prune.Click += async (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            await repo.ShowAndStartPopupAsync(new ViewModels.LFSPrune(repo));

                        e.Handled = true;
                    };
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(prune);

                    var locks = new MenuItem();
                    locks.Header = App.Text("GitLFS.Locks");
                    locks.Icon = App.CreateMenuIcon("Icons.Lock");
                    locks.IsEnabled = repo.Remotes.Count > 0;
                    if (repo.Remotes.Count == 1)
                    {
                        locks.Click += async (_, e) =>
                        {
                            await App.ShowDialog(new ViewModels.LFSLocks(repo, repo.Remotes[0].Name));
                            e.Handled = true;
                        };
                    }
                    else
                    {
                        foreach (var remote in repo.Remotes)
                        {
                            var remoteName = remote.Name;
                            var lockRemote = new MenuItem();
                            lockRemote.Header = remoteName;
                            lockRemote.Click += async (_, e) =>
                            {
                                await App.ShowDialog(new ViewModels.LFSLocks(repo, remoteName));
                                e.Handled = true;
                            };
                            locks.Items.Add(lockRemote);
                        }
                    }

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(locks);
                }
                else
                {
                    var install = new MenuItem();
                    install.Header = App.Text("GitLFS.Install");
                    install.Icon = App.CreateMenuIcon("Icons.Init");
                    install.Click += async (_, e) =>
                    {
                        await repo.InstallLFSAsync();
                        e.Handled = true;
                    };
                    menu.Items.Add(install);
                }

                menu.Open(control);
            }

            ev.Handled = true;
        }

        private async void StartBisect(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository { IsBisectCommandRunning: false, InProgressContext: null } repo &&
                repo.CanCreatePopup())
            {
                if (repo.LocalChangesCount > 0)
                    App.RaiseException(repo.FullPath, "You have un-committed local changes. Please discard or stash them first.");
                else if (repo.IsBisectCommandRunning || repo.BisectState != Models.BisectState.None)
                    App.RaiseException(repo.FullPath, "Bisect is running! Please abort it before starting a new one.");
                else
                    await repo.ExecBisectCommandAsync("start");
            }

            e.Handled = true;
        }

        private async void Cleanup(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await repo.CleanupAsync();
                e.Handled = true;
            }
        }

        private void OpenCustomActionMenu(object sender, RoutedEventArgs ev)
        {
            if (DataContext is ViewModels.Repository repo && sender is Control control)
            {
                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

                var actions = repo.GetCustomActions(Models.CustomActionScope.Repository);
                if (actions.Count > 0)
                {
                    foreach (var action in actions)
                    {
                        var (dup, label) = action;
                        var item = new MenuItem();
                        item.Icon = App.CreateMenuIcon("Icons.Action");
                        item.Header = label;
                        item.Click += async (_, e) =>
                        {
                            await repo.ExecCustomActionAsync(dup, null);
                            e.Handled = true;
                        };

                        menu.Items.Add(item);
                    }
                }
                else
                {
                    menu.Items.Add(new MenuItem() { Header = App.Text("Repository.CustomActions.Empty") });
                }

                menu.Open(control);
            }

            ev.Handled = true;
        }

        private async void OpenGitLogs(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await App.ShowDialog(new ViewModels.ViewLogs(repo));
                e.Handled = true;
            }
        }

        private void NavigateToHead(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository { CurrentBranch: not null } repo)
            {
                var repoView = TopLevel.GetTopLevel(this)?.FindDescendantOfType<Repository>();
                repoView?.LocalBranchTree?.Select(repo.CurrentBranch);

                repo.NavigateToCommit(repo.CurrentBranch.Head);
                e.Handled = true;
            }
        }
    }
}
