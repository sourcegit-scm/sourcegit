using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Histories : ObservableObject
    {
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public List<Models.Commit> Commits
        {
            get => _commits;
            set
            {
                var lastSelected = AutoSelectedCommit;
                if (SetProperty(ref _commits, value))
                {
                    if (value.Count > 0 && lastSelected != null)
                        AutoSelectedCommit = value.Find(x => x.SHA == lastSelected.SHA);
                }
            }
        }

        public Models.CommitGraph Graph
        {
            get => _graph;
            set => SetProperty(ref _graph, value);
        }

        public Models.Commit AutoSelectedCommit
        {
            get => _autoSelectedCommit;
            set => SetProperty(ref _autoSelectedCommit, value);
        }

        public long NavigationId
        {
            get => _navigationId;
            private set => SetProperty(ref _navigationId, value);
        }

        public object DetailContext
        {
            get => _detailContext;
            set => SetProperty(ref _detailContext, value);
        }

        public Histories(Repository repo)
        {
            _repo = repo;
        }

        public void Cleanup()
        {
            Commits = new List<Models.Commit>();

            _repo = null;
            _graph = null;
            _autoSelectedCommit = null;

            if (_detailContext is CommitDetail cd)
            {
                cd.Cleanup();
            }
            else if (_detailContext is RevisionCompare rc)
            {
                rc.Cleanup();
            }

            _detailContext = null;
        }

        public void NavigateTo(string commitSHA)
        {
            var commit = _commits.Find(x => x.SHA.StartsWith(commitSHA, StringComparison.Ordinal));
            if (commit != null)
            {
                AutoSelectedCommit = commit;
                NavigationId = _navigationId + 1;

                if (_detailContext is CommitDetail detail)
                {
                    detail.Commit = commit;
                }
                else
                {
                    var commitDetail = new CommitDetail(_repo);
                    commitDetail.Commit = commit;
                    DetailContext = commitDetail;
                }
            }
        }

        public void Select(IList commits)
        {
            if (commits.Count == 0)
            {
                _repo.SearchResultSelectedCommit = null;
                DetailContext = null;
            }
            else if (commits.Count == 1)
            {
                var commit = commits[0] as Models.Commit;
                _repo.SearchResultSelectedCommit = commit;

                AutoSelectedCommit = commit;
                NavigationId = _navigationId + 1;

                if (_detailContext is CommitDetail detail)
                {
                    detail.Commit = commit;
                }
                else
                {
                    var commitDetail = new CommitDetail(_repo);
                    commitDetail.Commit = commit;
                    DetailContext = commitDetail;
                }
            }
            else if (commits.Count == 2)
            {
                _repo.SearchResultSelectedCommit = null;

                var end = commits[0] as Models.Commit;
                var start = commits[1] as Models.Commit;
                DetailContext = new RevisionCompare(_repo.FullPath, start, end);
            }
            else
            {
                _repo.SearchResultSelectedCommit = null;
                DetailContext = commits.Count;
            }
        }

        public void DoubleTapped(Models.Commit commit)
        {
            if (commit == null || commit.IsCurrentHead)
                return;

            var firstRemoteBranch = null as Models.Branch;
            foreach (var d in commit.Decorators)
            {
                if (d.Type == Models.DecoratorType.LocalBranchHead)
                {
                    var b = _repo.Branches.Find(x => x.FriendlyName == d.Name);
                    if (b != null)
                    {
                        _repo.CheckoutBranch(b);
                        return;
                    }
                }
                else if (d.Type == Models.DecoratorType.RemoteBranchHead && firstRemoteBranch == null)
                {
                    firstRemoteBranch = _repo.Branches.Find(x => x.FriendlyName == d.Name);
                }
            }

            if (PopupHost.CanCreatePopup())
            {
                if (firstRemoteBranch != null)
                    PopupHost.ShowPopup(new CreateBranch(_repo, firstRemoteBranch));
                else
                    PopupHost.ShowPopup(new CheckoutCommit(_repo, commit));
            }
        }

        public ContextMenu MakeContextMenu(DataGrid datagrid)
        {
            if (datagrid.SelectedItems.Count != 1)
                return null;

            var current = _repo.CurrentBranch;
            if (current == null)
                return null;

            var commit = (datagrid.SelectedItem as Models.Commit)!;
            var menu = new ContextMenu();
            var tags = new List<Models.Tag>();

            if (commit.HasDecorators)
            {
                foreach (var d in commit.Decorators)
                {
                    if (d.Type == Models.DecoratorType.CurrentBranchHead)
                    {
                        FillCurrentBranchMenu(menu, current);
                    }
                    else if (d.Type == Models.DecoratorType.LocalBranchHead)
                    {
                        var b = _repo.Branches.Find(x => x.IsLocal && d.Name == x.Name);
                        FillOtherLocalBranchMenu(menu, b, current, commit.IsMerged);
                    }
                    else if (d.Type == Models.DecoratorType.RemoteBranchHead)
                    {
                        var b = _repo.Branches.Find(x => !x.IsLocal && d.Name == x.FriendlyName);
                        FillRemoteBranchMenu(menu, b, current, commit.IsMerged);
                    }
                    else if (d.Type == Models.DecoratorType.Tag)
                    {
                        var t = _repo.Tags.Find(x => x.Name == d.Name);
                        if (t != null)
                            tags.Add(t);
                    }
                }

                if (menu.Items.Count > 0)
                    menu.Items.Add(new MenuItem() { Header = "-" });
            }

            if (tags.Count > 0)
            {
                foreach (var tag in tags)
                    FillTagMenu(menu, tag);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            if (current.Head != commit.SHA)
            {
                var reset = new MenuItem();
                reset.Header = new Views.NameHighlightedTextBlock("CommitCM.Reset", current.Name);
                reset.Icon = App.CreateMenuIcon("Icons.Reset");
                reset.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new Reset(_repo, current, commit));
                    e.Handled = true;
                };
                menu.Items.Add(reset);
            }
            else
            {
                var reword = new MenuItem();
                reword.Header = App.Text("CommitCM.Reword");
                reword.Icon = App.CreateMenuIcon("Icons.Edit");
                reword.Click += (_, e) =>
                {
                    if (_repo.WorkingCopyChangesCount > 0)
                    {
                        App.RaiseException(_repo.FullPath, "You have local changes. Please run stash or discard first.");
                        return;
                    }

                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new Reword(_repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(reword);

                var squash = new MenuItem();
                squash.Header = App.Text("CommitCM.Squash");
                squash.Icon = App.CreateMenuIcon("Icons.SquashIntoParent");
                squash.IsEnabled = commit.Parents.Count == 1;
                squash.Click += (_, e) =>
                {
                    if (_repo.WorkingCopyChangesCount > 0)
                    {
                        App.RaiseException(_repo.FullPath, "You have local changes. Please run stash or discard first.");
                        return;
                    }

                    if (commit.Parents.Count == 1)
                    {
                        var parent = _commits.Find(x => x.SHA == commit.Parents[0]);
                        if (parent != null && PopupHost.CanCreatePopup())
                            PopupHost.ShowPopup(new Squash(_repo, commit, parent));
                    }

                    e.Handled = true;
                };
                menu.Items.Add(squash);
            }

            if (!commit.IsMerged)
            {
                var rebase = new MenuItem();
                rebase.Header = new Views.NameHighlightedTextBlock("CommitCM.Rebase", current.Name);
                rebase.Icon = App.CreateMenuIcon("Icons.Rebase");
                rebase.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new Rebase(_repo, current, commit));
                    e.Handled = true;
                };
                menu.Items.Add(rebase);

                var cherryPick = new MenuItem();
                cherryPick.Header = App.Text("CommitCM.CherryPick");
                cherryPick.Icon = App.CreateMenuIcon("Icons.CherryPick");
                cherryPick.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new CherryPick(_repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(cherryPick);
            }
            else
            {
                var revert = new MenuItem();
                revert.Header = App.Text("CommitCM.Revert");
                revert.Icon = App.CreateMenuIcon("Icons.Undo");
                revert.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new Revert(_repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(revert);

                var interactiveRebase = new MenuItem();
                interactiveRebase.Header = new Views.NameHighlightedTextBlock("CommitCM.InteractiveRebase", current.Name);
                interactiveRebase.Icon = App.CreateMenuIcon("Icons.InteractiveRebase");
                interactiveRebase.IsVisible = current.Head != commit.SHA;
                interactiveRebase.Click += (_, e) =>
                {
                    if (_repo.WorkingCopyChangesCount > 0)
                    {
                        App.RaiseException(_repo.FullPath, "You have local changes. Please run stash or discard first.");
                        return;
                    }

                    var toplevel = datagrid.FindAncestorOfType<Views.Launcher>();
                    if (toplevel == null)
                        return;

                    var dialog = new Views.InteractiveRebase() { DataContext = new InteractiveRebase(_repo, current, commit) };
                    dialog.ShowDialog(toplevel);
                    e.Handled = true;
                };
                menu.Items.Add(interactiveRebase);
            }

            if (current.Head != commit.SHA)
            {
                var checkoutCommit = new MenuItem();
                checkoutCommit.Header = App.Text("CommitCM.Checkout");
                checkoutCommit.Icon = App.CreateMenuIcon("Icons.Detached");
                checkoutCommit.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new CheckoutCommit(_repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(checkoutCommit);
            }

            menu.Items.Add(new MenuItem() { Header = "-" });

            if (current.Head != commit.SHA)
            {
                var compareWithHead = new MenuItem();
                compareWithHead.Header = App.Text("CommitCM.CompareWithHead");
                compareWithHead.Icon = App.CreateMenuIcon("Icons.Compare");
                compareWithHead.Click += (_, e) =>
                {
                    var head = _commits.Find(x => x.SHA == current.Head);
                    if (head == null)
                    {
                        _repo.SearchResultSelectedCommit = null;
                        head = new Commands.QuerySingleCommit(_repo.FullPath, current.Head).Result();
                        if (head != null)
                            DetailContext = new RevisionCompare(_repo.FullPath, commit, head);
                    }
                    else
                    {
                        datagrid.SelectedItems.Add(head);
                    }

                    e.Handled = true;
                };
                menu.Items.Add(compareWithHead);

                if (_repo.WorkingCopyChangesCount > 0)
                {
                    var compareWithWorktree = new MenuItem();
                    compareWithWorktree.Header = App.Text("CommitCM.CompareWithWorktree");
                    compareWithWorktree.Icon = App.CreateMenuIcon("Icons.Compare");
                    compareWithWorktree.Click += (_, e) =>
                    {
                        DetailContext = new RevisionCompare(_repo.FullPath, commit, null);
                        e.Handled = true;
                    };
                    menu.Items.Add(compareWithWorktree);
                }

                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var createBranch = new MenuItem();
            createBranch.Icon = App.CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new CreateBranch(_repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(createBranch);

            var createTag = new MenuItem();
            createTag.Icon = App.CreateMenuIcon("Icons.Tag.Add");
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new CreateTag(_repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(createTag);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var saveToPatch = new MenuItem();
            saveToPatch.Icon = App.CreateMenuIcon("Icons.Diff");
            saveToPatch.Header = App.Text("CommitCM.SaveAsPatch");
            saveToPatch.Click += async (_, e) =>
            {
                var topLevel = App.GetTopLevel();
                if (topLevel == null)
                    return;

                var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                var selected = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    var succ = new Commands.FormatPatch(_repo.FullPath, commit.SHA, selected[0].Path.LocalPath).Exec();
                    if (succ)
                        App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                }

                e.Handled = true;
            };
            menu.Items.Add(saveToPatch);

            var archive = new MenuItem();
            archive.Icon = App.CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new Archive(_repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(archive);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var copySHA = new MenuItem();
            copySHA.Header = App.Text("CommitCM.CopySHA");
            copySHA.Icon = App.CreateMenuIcon("Icons.Copy");
            copySHA.Click += (_, e) =>
            {
                App.CopyText(commit.SHA);
                e.Handled = true;
            };
            menu.Items.Add(copySHA);

            var copyInfo = new MenuItem();
            copyInfo.Header = App.Text("CommitCM.CopyInfo");
            copyInfo.Icon = App.CreateMenuIcon("Icons.Copy");
            copyInfo.Click += (_, e) =>
            {
                App.CopyText($"{commit.SHA.Substring(0, 10)} - {commit.Subject}");
                e.Handled = true;
            };
            menu.Items.Add(copyInfo);

            return menu;
        }

        private void FillCurrentBranchMenu(ContextMenu menu, Models.Branch current)
        {
            var submenu = new MenuItem();
            submenu.Icon = App.CreateMenuIcon("Icons.Branch");
            submenu.Header = current.Name;

            if (!string.IsNullOrEmpty(current.Upstream))
            {
                var upstream = current.Upstream.Substring(13);

                var fastForward = new MenuItem();
                fastForward.Header = new Views.NameHighlightedTextBlock("BranchCM.FastForward", upstream);
                fastForward.Icon = App.CreateMenuIcon("Icons.FastForward");
                fastForward.IsEnabled = current.TrackStatus.Ahead.Count == 0;
                fastForward.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowAndStartPopup(new Merge(_repo, upstream, current.Name));
                    e.Handled = true;
                };
                submenu.Items.Add(fastForward);

                var pull = new MenuItem();
                pull.Header = new Views.NameHighlightedTextBlock("BranchCM.Pull", upstream);
                pull.Icon = App.CreateMenuIcon("Icons.Pull");
                pull.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new Pull(_repo, null));
                    e.Handled = true;
                };
                submenu.Items.Add(pull);
            }

            var push = new MenuItem();
            push.Header = new Views.NameHighlightedTextBlock("BranchCM.Push", current.Name);
            push.Icon = App.CreateMenuIcon("Icons.Push");
            push.IsEnabled = _repo.Remotes.Count > 0;
            push.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new Push(_repo, current));
                e.Handled = true;
            };
            submenu.Items.Add(push);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var detect = Commands.GitFlow.DetectType(_repo.FullPath, _repo.Branches, current.Name);
            if (detect.IsGitFlowBranch)
            {
                var finish = new MenuItem();
                finish.Header = new Views.NameHighlightedTextBlock("BranchCM.Finish", current.Name);
                finish.Icon = App.CreateMenuIcon("Icons.GitFlow");
                finish.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new GitFlowFinish(_repo, current, detect.Type, detect.Prefix));
                    e.Handled = true;
                };
                submenu.Items.Add(finish);
                submenu.Items.Add(new MenuItem() { Header = "-" });
            }

            var rename = new MenuItem();
            rename.Header = new Views.NameHighlightedTextBlock("BranchCM.Rename", current.Name);
            rename.Icon = App.CreateMenuIcon("Icons.Rename");
            rename.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new RenameBranch(_repo, current));
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            menu.Items.Add(submenu);
        }

        private void FillOtherLocalBranchMenu(ContextMenu menu, Models.Branch branch, Models.Branch current, bool merged)
        {
            var submenu = new MenuItem();
            submenu.Icon = App.CreateMenuIcon("Icons.Branch");
            submenu.Header = branch.Name;

            var checkout = new MenuItem();
            checkout.Header = new Views.NameHighlightedTextBlock("BranchCM.Checkout", branch.Name);
            checkout.Icon = App.CreateMenuIcon("Icons.Check");
            checkout.Click += (_, e) =>
            {
                _repo.CheckoutBranch(branch);
                e.Handled = true;
            };
            submenu.Items.Add(checkout);

            var merge = new MenuItem();
            merge.Header = new Views.NameHighlightedTextBlock("BranchCM.Merge", branch.Name, current.Name);
            merge.Icon = App.CreateMenuIcon("Icons.Merge");
            merge.IsEnabled = !merged;
            merge.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new Merge(_repo, branch.Name, current.Name));
                e.Handled = true;
            };
            submenu.Items.Add(merge);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var detect = Commands.GitFlow.DetectType(_repo.FullPath, _repo.Branches, branch.Name);
            if (detect.IsGitFlowBranch)
            {
                var finish = new MenuItem();
                finish.Header = new Views.NameHighlightedTextBlock("BranchCM.Finish", branch.Name);
                finish.Icon = App.CreateMenuIcon("Icons.GitFlow");
                finish.Click += (_, e) =>
                {
                    if (PopupHost.CanCreatePopup())
                        PopupHost.ShowPopup(new GitFlowFinish(_repo, branch, detect.Type, detect.Prefix));
                    e.Handled = true;
                };
                submenu.Items.Add(finish);
                submenu.Items.Add(new MenuItem() { Header = "-" });
            }

            var rename = new MenuItem();
            rename.Header = new Views.NameHighlightedTextBlock("BranchCM.Rename", branch.Name);
            rename.Icon = App.CreateMenuIcon("Icons.Rename");
            rename.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new RenameBranch(_repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            var delete = new MenuItem();
            delete.Header = new Views.NameHighlightedTextBlock("BranchCM.Delete", branch.Name);
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new DeleteBranch(_repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(delete);

            menu.Items.Add(submenu);
        }

        private void FillRemoteBranchMenu(ContextMenu menu, Models.Branch branch, Models.Branch current, bool merged)
        {
            var name = branch.FriendlyName;

            var submenu = new MenuItem();
            submenu.Icon = App.CreateMenuIcon("Icons.Branch");
            submenu.Header = name;

            var checkout = new MenuItem();
            checkout.Header = new Views.NameHighlightedTextBlock("BranchCM.Checkout", name);
            checkout.Icon = App.CreateMenuIcon("Icons.Check");
            checkout.Click += (_, e) =>
            {
                _repo.CheckoutBranch(branch);
                e.Handled = true;
            };
            submenu.Items.Add(checkout);

            var merge = new MenuItem();
            merge.Header = new Views.NameHighlightedTextBlock("BranchCM.Merge", name, current.Name);
            merge.Icon = App.CreateMenuIcon("Icons.Merge");
            merge.IsEnabled = !merged;
            merge.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new Merge(_repo, name, current.Name));
                e.Handled = true;
            };

            submenu.Items.Add(merge);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var delete = new MenuItem();
            delete.Header = new Views.NameHighlightedTextBlock("BranchCM.Delete", name);
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new DeleteBranch(_repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(delete);

            menu.Items.Add(submenu);
        }

        private void FillTagMenu(ContextMenu menu, Models.Tag tag)
        {
            var submenu = new MenuItem();
            submenu.Header = tag.Name;
            submenu.Icon = App.CreateMenuIcon("Icons.Tag");
            submenu.MinWidth = 200;

            var push = new MenuItem();
            push.Header = new Views.NameHighlightedTextBlock("TagCM.Push", tag.Name);
            push.Icon = App.CreateMenuIcon("Icons.Push");
            push.IsEnabled = _repo.Remotes.Count > 0;
            push.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new PushTag(_repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(push);

            var delete = new MenuItem();
            delete.Header = new Views.NameHighlightedTextBlock("TagCM.Delete", tag.Name);
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (PopupHost.CanCreatePopup())
                    PopupHost.ShowPopup(new DeleteTag(_repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(delete);

            menu.Items.Add(submenu);
        }

        private Repository _repo = null;
        private bool _isLoading = true;
        private List<Models.Commit> _commits = new List<Models.Commit>();
        private Models.CommitGraph _graph = null;
        private Models.Commit _autoSelectedCommit = null;
        private long _navigationId = 0;
        private object _detailContext = null;
    }
}
