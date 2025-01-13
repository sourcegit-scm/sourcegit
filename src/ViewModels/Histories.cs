using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class Histories : ObservableObject
    {
        public Repository Repo
        {
            get => _repo;
        }

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

        public GridLength LeftArea
        {
            get => _leftArea;
            set => SetProperty(ref _leftArea, value);
        }

        public GridLength RightArea
        {
            get => _rightArea;
            set => SetProperty(ref _rightArea, value);
        }

        public GridLength TopArea
        {
            get => _topArea;
            set => SetProperty(ref _topArea, value);
        }

        public GridLength BottomArea
        {
            get => _bottomArea;
            set => SetProperty(ref _bottomArea, value);
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
            if (commit == null)
            {
                AutoSelectedCommit = null;
                commit = new Commands.QuerySingleCommit(_repo.FullPath, commitSHA).Result();
            }
            else
            {
                AutoSelectedCommit = commit;
                NavigationId = _navigationId + 1;
            }

            if (commit != null)
            {
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
            else
            {
                DetailContext = null;
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
                var commit = (commits[0] as Models.Commit)!;
                if (_repo.SearchResultSelectedCommit == null || _repo.SearchResultSelectedCommit.SHA != commit.SHA)
                    _repo.SearchResultSelectedCommit = _repo.SearchedCommits.Find(x => x.SHA == commit.SHA);

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

            if (_repo.CanCreatePopup())
            {
                if (firstRemoteBranch != null)
                    _repo.ShowPopup(new CreateBranch(_repo, firstRemoteBranch));
                else
                    _repo.ShowPopup(new CheckoutCommit(_repo, commit));
            }
        }

        public ContextMenu MakeContextMenu(ListBox list)
        {
            var current = _repo.CurrentBranch;
            if (current == null || list.SelectedItems == null)
                return null;

            if (list.SelectedItems.Count > 1)
            {
                var selected = new List<Models.Commit>();
                var canCherryPick = true;
                var canMerge = true;

                foreach (var item in list.SelectedItems)
                {
                    if (item is Models.Commit c)
                    {
                        selected.Add(c);

                        if (c.IsMerged)
                        {
                            canMerge = false;
                            canCherryPick = false;
                        }
                        else if (c.Parents.Count > 1)
                        {
                            canCherryPick = false;
                        }
                    }
                }

                // Sort selected commits in order.
                selected.Sort((l, r) => _commits.IndexOf(r) - _commits.IndexOf(l));

                var multipleMenu = new ContextMenu();

                if (canCherryPick)
                {
                    var cherryPickMultiple = new MenuItem();
                    cherryPickMultiple.Header = App.Text("CommitCM.CherryPickMultiple");
                    cherryPickMultiple.Icon = App.CreateMenuIcon("Icons.CherryPick");
                    cherryPickMultiple.Click += (_, e) =>
                    {
                        if (_repo.CanCreatePopup())
                            _repo.ShowPopup(new CherryPick(_repo, selected));
                        e.Handled = true;
                    };
                    multipleMenu.Items.Add(cherryPickMultiple);
                }

                if (canMerge)
                {
                    var mergeMultiple = new MenuItem();
                    mergeMultiple.Header = App.Text("CommitCM.MergeMultiple");
                    mergeMultiple.Icon = App.CreateMenuIcon("Icons.Merge");
                    mergeMultiple.Click += (_, e) =>
                    {
                        if (_repo.CanCreatePopup())
                            _repo.ShowPopup(new MergeMultiple(_repo, selected));
                        e.Handled = true;
                    };
                    multipleMenu.Items.Add(mergeMultiple);
                }

                if (canCherryPick || canMerge)
                    multipleMenu.Items.Add(new MenuItem() { Header = "-" });

                var saveToPatchMultiple = new MenuItem();
                saveToPatchMultiple.Icon = App.CreateMenuIcon("Icons.Diff");
                saveToPatchMultiple.Header = App.Text("CommitCM.SaveAsPatch");
                saveToPatchMultiple.Click += async (_, e) =>
                {
                    var storageProvider = App.GetStorageProvider();
                    if (storageProvider == null)
                        return;

                    var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                    try
                    {
                        var picker = await storageProvider.OpenFolderPickerAsync(options);
                        if (picker.Count == 1)
                        {
                            var succ = false;
                            for (var i = 0; i < selected.Count; i++)
                            {
                                var saveTo = GetPatchFileName(picker[0].Path.LocalPath, selected[i], i);
                                succ = await Task.Run(() => new Commands.FormatPatch(_repo.FullPath, selected[i].SHA, saveTo).Exec());
                                if (!succ)
                                    break;
                            }

                            if (succ)
                                App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                        }
                    }
                    catch (Exception exception)
                    {
                        App.RaiseException(_repo.FullPath, $"Failed to save as patch: {exception.Message}");
                    }

                    e.Handled = true;
                };
                multipleMenu.Items.Add(saveToPatchMultiple);
                multipleMenu.Items.Add(new MenuItem() { Header = "-" });

                var copyMultipleSHAs = new MenuItem();
                copyMultipleSHAs.Header = App.Text("CommitCM.CopySHA");
                copyMultipleSHAs.Icon = App.CreateMenuIcon("Icons.Copy");
                copyMultipleSHAs.Click += (_, e) =>
                {
                    var builder = new StringBuilder();
                    foreach (var c in selected)
                        builder.AppendLine(c.SHA);

                    App.CopyText(builder.ToString());
                    e.Handled = true;
                };
                multipleMenu.Items.Add(copyMultipleSHAs);

                var copyMultipleInfo = new MenuItem();
                copyMultipleInfo.Header = App.Text("CommitCM.CopyInfo");
                copyMultipleInfo.Icon = App.CreateMenuIcon("Icons.Copy");
                copyMultipleInfo.Click += (_, e) =>
                {
                    var builder = new StringBuilder();
                    foreach (var c in selected)
                        builder.AppendLine($"{c.SHA.Substring(0, 10)} - {c.Subject}");

                    App.CopyText(builder.ToString());
                    e.Handled = true;
                };
                multipleMenu.Items.Add(copyMultipleInfo);

                return multipleMenu;
            }

            var commit = (list.SelectedItem as Models.Commit)!;
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
                    FillTagMenu(menu, tag, current, commit.IsMerged);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            if (current.Head != commit.SHA)
            {
                var reset = new MenuItem();
                reset.Header = new Views.NameHighlightedTextBlock("CommitCM.Reset", current.Name);
                reset.Icon = App.CreateMenuIcon("Icons.Reset");
                reset.Click += (_, e) =>
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new Reset(_repo, current, commit));
                    e.Handled = true;
                };
                menu.Items.Add(reset);

                if (commit.IsMerged)
                {
                    var squash = new MenuItem();
                    squash.Header = App.Text("CommitCM.SquashCommitsSinceThis");
                    squash.Icon = App.CreateMenuIcon("Icons.SquashIntoParent");
                    squash.Click += (_, e) =>
                    {
                        if (_repo.LocalChangesCount > 0)
                        {
                            App.RaiseException(_repo.FullPath, "You have local changes. Please run stash or discard first.");
                            return;
                        }

                        if (_repo.CanCreatePopup())
                            _repo.ShowPopup(new Squash(_repo, commit, commit.SHA));

                        e.Handled = true;
                    };
                    menu.Items.Add(squash);
                }
            }
            else
            {
                var reword = new MenuItem();
                reword.Header = App.Text("CommitCM.Reword");
                reword.Icon = App.CreateMenuIcon("Icons.Edit");
                reword.Click += (_, e) =>
                {
                    if (_repo.LocalChangesCount > 0)
                    {
                        App.RaiseException(_repo.FullPath, "You have local changes. Please run stash or discard first.");
                        return;
                    }

                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new Reword(_repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(reword);

                var squash = new MenuItem();
                squash.Header = App.Text("CommitCM.Squash");
                squash.Icon = App.CreateMenuIcon("Icons.SquashIntoParent");
                squash.IsEnabled = commit.Parents.Count == 1;
                squash.Click += (_, e) =>
                {
                    if (_repo.LocalChangesCount > 0)
                    {
                        App.RaiseException(_repo.FullPath, "You have local changes. Please run stash or discard first.");
                        return;
                    }

                    if (commit.Parents.Count == 1)
                    {
                        var parent = _commits.Find(x => x.SHA == commit.Parents[0]);
                        if (parent != null && _repo.CanCreatePopup())
                            _repo.ShowPopup(new Squash(_repo, parent, commit.SHA));
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
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new Rebase(_repo, current, commit));
                    e.Handled = true;
                };
                menu.Items.Add(rebase);

                if (!commit.HasDecorators)
                {
                    var merge = new MenuItem();
                    merge.Header = new Views.NameHighlightedTextBlock("CommitCM.Merge", current.Name);
                    merge.Icon = App.CreateMenuIcon("Icons.Merge");
                    merge.Click += (_, e) =>
                    {
                        if (_repo.CanCreatePopup())
                            _repo.ShowPopup(new Merge(_repo, commit, current.Name));

                        e.Handled = true;
                    };
                    menu.Items.Add(merge);
                }

                var cherryPick = new MenuItem();
                cherryPick.Header = App.Text("CommitCM.CherryPick");
                cherryPick.Icon = App.CreateMenuIcon("Icons.CherryPick");
                cherryPick.Click += (_, e) =>
                {
                    if (_repo.CanCreatePopup())
                    {
                        if (commit.Parents.Count <= 1)
                        {
                            _repo.ShowPopup(new CherryPick(_repo, [commit]));
                        }
                        else
                        {
                            var parents = new List<Models.Commit>();
                            foreach (var sha in commit.Parents)
                            {
                                var parent = _commits.Find(x => x.SHA == sha);
                                if (parent == null)
                                    parent = new Commands.QuerySingleCommit(_repo.FullPath, sha).Result();

                                if (parent != null)
                                    parents.Add(parent);
                            }

                            _repo.ShowPopup(new CherryPick(_repo, commit, parents));
                        }
                    }

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
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new Revert(_repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(revert);
            }

            if (current.Head != commit.SHA)
            {
                var checkoutCommit = new MenuItem();
                checkoutCommit.Header = App.Text("CommitCM.Checkout");
                checkoutCommit.Icon = App.CreateMenuIcon("Icons.Detached");
                checkoutCommit.Click += (_, e) =>
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new CheckoutCommit(_repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(checkoutCommit);
            }

            menu.Items.Add(new MenuItem() { Header = "-" });

            if (commit.IsMerged && current.Head != commit.SHA)
            {
                var interactiveRebase = new MenuItem();
                interactiveRebase.Header = new Views.NameHighlightedTextBlock("CommitCM.InteractiveRebase", current.Name);
                interactiveRebase.Icon = App.CreateMenuIcon("Icons.InteractiveRebase");
                interactiveRebase.Click += (_, e) =>
                {
                    if (_repo.LocalChangesCount > 0)
                    {
                        App.RaiseException(_repo.FullPath, "You have local changes. Please run stash or discard first.");
                        return;
                    }

                    App.OpenDialog(new Views.InteractiveRebase()
                    {
                        DataContext = new InteractiveRebase(_repo, current, commit)
                    });

                    e.Handled = true;
                };
                menu.Items.Add(interactiveRebase);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

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
                        list.SelectedItems.Add(head);
                    }

                    e.Handled = true;
                };
                menu.Items.Add(compareWithHead);

                if (_repo.LocalChangesCount > 0)
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
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new CreateBranch(_repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(createBranch);

            var createTag = new MenuItem();
            createTag.Icon = App.CreateMenuIcon("Icons.Tag.Add");
            createTag.Header = App.Text("CreateTag");
            createTag.Click += (_, e) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new CreateTag(_repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(createTag);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var saveToPatch = new MenuItem();
            saveToPatch.Icon = App.CreateMenuIcon("Icons.Diff");
            saveToPatch.Header = App.Text("CommitCM.SaveAsPatch");
            saveToPatch.Click += async (_, e) =>
            {
                var storageProvider = App.GetStorageProvider();
                if (storageProvider == null)
                    return;

                var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                try
                {
                    var selected = await storageProvider.OpenFolderPickerAsync(options);
                    if (selected.Count == 1)
                    {
                        var saveTo = GetPatchFileName(selected[0].Path.LocalPath, commit);
                        var succ = new Commands.FormatPatch(_repo.FullPath, commit.SHA, saveTo).Exec();
                        if (succ)
                            App.SendNotification(_repo.FullPath, App.Text("SaveAsPatchSuccess"));
                    }
                }
                catch (Exception exception)
                {
                    App.RaiseException(_repo.FullPath, $"Failed to save as patch: {exception.Message}");
                }

                e.Handled = true;
            };
            menu.Items.Add(saveToPatch);

            var archive = new MenuItem();
            archive.Icon = App.CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (_, e) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new Archive(_repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(archive);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var actions = new List<Models.CustomAction>();
            foreach (var action in _repo.Settings.CustomActions)
            {
                if (action.Scope == Models.CustomActionScope.Commit)
                    actions.Add(action);
            }
            if (actions.Count > 0)
            {
                var custom = new MenuItem();
                custom.Header = App.Text("CommitCM.CustomAction");
                custom.Icon = App.CreateMenuIcon("Icons.Action");

                foreach (var action in actions)
                {
                    var dup = action;
                    var item = new MenuItem();
                    item.Icon = App.CreateMenuIcon("Icons.Action");
                    item.Header = dup.Name;
                    item.Click += (_, e) =>
                    {
                        if (_repo.CanCreatePopup())
                            _repo.ShowAndStartPopup(new ExecuteCustomAction(_repo, dup, commit));

                        e.Handled = true;
                    };

                    custom.Items.Add(item);
                }

                menu.Items.Add(custom);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

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

        private Models.FilterMode GetFilterMode(string pattern)
        {
            foreach (var filter in _repo.Settings.HistoriesFilters)
            {
                if (filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                    return filter.Mode;
            }

            return Models.FilterMode.None;
        }

        private void FillBranchVisibilityMenu(MenuItem submenu, Models.Branch branch)
        {
            var visibility = new MenuItem();
            visibility.Icon = App.CreateMenuIcon("Icons.Eye");
            visibility.Header = App.Text("Repository.FilterCommits");

            var exclude = new MenuItem();
            exclude.Icon = App.CreateMenuIcon("Icons.EyeClose");
            exclude.Header = App.Text("Repository.FilterCommits.Exclude");
            exclude.Click += (_, e) =>
            {
                _repo.SetBranchFilterMode(branch, Models.FilterMode.Excluded, false, true);
                e.Handled = true;
            };

            var filterMode = GetFilterMode(branch.FullName);
            if (filterMode == Models.FilterMode.None)
            {
                var include = new MenuItem();
                include.Icon = App.CreateMenuIcon("Icons.Filter");
                include.Header = App.Text("Repository.FilterCommits.Include");
                include.Click += (_, e) =>
                {
                    _repo.SetBranchFilterMode(branch, Models.FilterMode.Included, false, true);
                    e.Handled = true;
                };
                visibility.Items.Add(include);
                visibility.Items.Add(exclude);
            }
            else
            {
                var unset = new MenuItem();
                unset.Header = App.Text("Repository.FilterCommits.Default");
                unset.Click += (_, e) =>
                {
                    _repo.SetBranchFilterMode(branch, Models.FilterMode.None, false, true);
                    e.Handled = true;
                };
                visibility.Items.Add(exclude);
                visibility.Items.Add(unset);
            }

            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });
        }

        private void FillTagVisibilityMenu(MenuItem submenu, Models.Tag tag)
        {
            var visibility = new MenuItem();
            visibility.Icon = App.CreateMenuIcon("Icons.Eye");
            visibility.Header = App.Text("Repository.FilterCommits");

            var exclude = new MenuItem();
            exclude.Icon = App.CreateMenuIcon("Icons.EyeClose");
            exclude.Header = App.Text("Repository.FilterCommits.Exclude");
            exclude.Click += (_, e) =>
            {
                _repo.SetTagFilterMode(tag, Models.FilterMode.Excluded);
                e.Handled = true;
            };

            var filterMode = GetFilterMode(tag.Name);
            if (filterMode == Models.FilterMode.None)
            {
                var include = new MenuItem();
                include.Icon = App.CreateMenuIcon("Icons.Filter");
                include.Header = App.Text("Repository.FilterCommits.Include");
                include.Click += (_, e) =>
                {
                    _repo.SetTagFilterMode(tag, Models.FilterMode.Included);
                    e.Handled = true;
                };
                visibility.Items.Add(include);
                visibility.Items.Add(exclude);
            }
            else
            {
                var unset = new MenuItem();
                unset.Header = App.Text("Repository.FilterCommits.Default");
                unset.Click += (_, e) =>
                {
                    _repo.SetTagFilterMode(tag, Models.FilterMode.None);
                    e.Handled = true;
                };
                visibility.Items.Add(exclude);
                visibility.Items.Add(unset);
            }

            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });
        }

        private void FillCurrentBranchMenu(ContextMenu menu, Models.Branch current)
        {
            var submenu = new MenuItem();
            submenu.Icon = App.CreateMenuIcon("Icons.Branch");
            submenu.Header = current.Name;

            FillBranchVisibilityMenu(submenu, current);

            if (!string.IsNullOrEmpty(current.Upstream))
            {
                var upstream = current.Upstream.Substring(13);

                var fastForward = new MenuItem();
                fastForward.Header = new Views.NameHighlightedTextBlock("BranchCM.FastForward", upstream);
                fastForward.Icon = App.CreateMenuIcon("Icons.FastForward");
                fastForward.IsEnabled = current.TrackStatus.Ahead.Count == 0;
                fastForward.Click += (_, e) =>
                {
                    var b = _repo.Branches.Find(x => x.FriendlyName == upstream);
                    if (b == null)
                        return;

                    if (_repo.CanCreatePopup())
                        _repo.ShowAndStartPopup(new Merge(_repo, b, current.Name));

                    e.Handled = true;
                };
                submenu.Items.Add(fastForward);

                var pull = new MenuItem();
                pull.Header = new Views.NameHighlightedTextBlock("BranchCM.Pull", upstream);
                pull.Icon = App.CreateMenuIcon("Icons.Pull");
                pull.Click += (_, e) =>
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new Pull(_repo, null));
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
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new Push(_repo, current));
                e.Handled = true;
            };
            submenu.Items.Add(push);

            var rename = new MenuItem();
            rename.Header = new Views.NameHighlightedTextBlock("BranchCM.Rename", current.Name);
            rename.Icon = App.CreateMenuIcon("Icons.Rename");
            rename.Click += (_, e) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new RenameBranch(_repo, current));
                e.Handled = true;
            };
            submenu.Items.Add(rename);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var detect = Commands.GitFlow.DetectType(_repo.FullPath, _repo.Branches, current.Name);
            if (detect.IsGitFlowBranch)
            {
                var finish = new MenuItem();
                finish.Header = new Views.NameHighlightedTextBlock("BranchCM.Finish", current.Name);
                finish.Icon = App.CreateMenuIcon("Icons.GitFlow");
                finish.Click += (_, e) =>
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new GitFlowFinish(_repo, current, detect.Type, detect.Prefix));
                    e.Handled = true;
                };
                submenu.Items.Add(finish);
                submenu.Items.Add(new MenuItem() { Header = "-" });
            }

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, e) =>
            {
                App.CopyText(current.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillOtherLocalBranchMenu(ContextMenu menu, Models.Branch branch, Models.Branch current, bool merged)
        {
            var submenu = new MenuItem();
            submenu.Icon = App.CreateMenuIcon("Icons.Branch");
            submenu.Header = branch.Name;

            FillBranchVisibilityMenu(submenu, branch);

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
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new Merge(_repo, branch, current.Name));
                e.Handled = true;
            };
            submenu.Items.Add(merge);

            var rename = new MenuItem();
            rename.Header = new Views.NameHighlightedTextBlock("BranchCM.Rename", branch.Name);
            rename.Icon = App.CreateMenuIcon("Icons.Rename");
            rename.Click += (_, e) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new RenameBranch(_repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            var delete = new MenuItem();
            delete.Header = new Views.NameHighlightedTextBlock("BranchCM.Delete", branch.Name);
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new DeleteBranch(_repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var detect = Commands.GitFlow.DetectType(_repo.FullPath, _repo.Branches, branch.Name);
            if (detect.IsGitFlowBranch)
            {
                var finish = new MenuItem();
                finish.Header = new Views.NameHighlightedTextBlock("BranchCM.Finish", branch.Name);
                finish.Icon = App.CreateMenuIcon("Icons.GitFlow");
                finish.Click += (_, e) =>
                {
                    if (_repo.CanCreatePopup())
                        _repo.ShowPopup(new GitFlowFinish(_repo, branch, detect.Type, detect.Prefix));
                    e.Handled = true;
                };
                submenu.Items.Add(finish);
                submenu.Items.Add(new MenuItem() { Header = "-" });
            }

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, e) =>
            {
                App.CopyText(branch.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillRemoteBranchMenu(ContextMenu menu, Models.Branch branch, Models.Branch current, bool merged)
        {
            var name = branch.FriendlyName;

            var submenu = new MenuItem();
            submenu.Icon = App.CreateMenuIcon("Icons.Branch");
            submenu.Header = name;

            FillBranchVisibilityMenu(submenu, branch);

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
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new Merge(_repo, branch, current.Name));
                e.Handled = true;
            };

            submenu.Items.Add(merge);

            var delete = new MenuItem();
            delete.Header = new Views.NameHighlightedTextBlock("BranchCM.Delete", name);
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new DeleteBranch(_repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, e) =>
            {
                App.CopyText(name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillTagMenu(ContextMenu menu, Models.Tag tag, Models.Branch current, bool merged)
        {
            var submenu = new MenuItem();
            submenu.Header = tag.Name;
            submenu.Icon = App.CreateMenuIcon("Icons.Tag");
            submenu.MinWidth = 200;

            FillTagVisibilityMenu(submenu, tag);

            var push = new MenuItem();
            push.Header = new Views.NameHighlightedTextBlock("TagCM.Push", tag.Name);
            push.Icon = App.CreateMenuIcon("Icons.Push");
            push.IsEnabled = _repo.Remotes.Count > 0;
            push.Click += (_, e) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new PushTag(_repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(push);

            var merge = new MenuItem();
            merge.Header = new Views.NameHighlightedTextBlock("TagCM.Merge", tag.Name, current.Name);
            merge.Icon = App.CreateMenuIcon("Icons.Merge");
            merge.IsEnabled = !merged;
            merge.Click += (_, e) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new Merge(_repo, tag, current.Name));
                e.Handled = true;
            };
            submenu.Items.Add(merge);

            var delete = new MenuItem();
            delete.Header = new Views.NameHighlightedTextBlock("TagCM.Delete", tag.Name);
            delete.Icon = App.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (_repo.CanCreatePopup())
                    _repo.ShowPopup(new DeleteTag(_repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("TagCM.Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (_, e) =>
            {
                App.CopyText(tag.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private string GetPatchFileName(string dir, Models.Commit commit, int index = 0)
        {
            var ignore_chars = new HashSet<char> { '/', '\\', ':', ',', '*', '?', '\"', '<', '>', '|', '`', '$', '^', '%', '[', ']', '+', '-' };
            var builder = new StringBuilder();
            builder.Append(index.ToString("D4"));
            builder.Append('-');

            var chars = commit.Subject.ToCharArray();
            var len = 0;
            foreach (var c in chars)
            {
                if (!ignore_chars.Contains(c))
                {
                    if (c == ' ' || c == '\t')
                        builder.Append('-');
                    else
                        builder.Append(c);

                    len++;

                    if (len >= 48)
                        break;
                }
            }
            builder.Append(".patch");

            return System.IO.Path.Combine(dir, builder.ToString());
        }

        private Repository _repo = null;
        private bool _isLoading = true;
        private List<Models.Commit> _commits = new List<Models.Commit>();
        private Models.CommitGraph _graph = null;
        private Models.Commit _autoSelectedCommit = null;
        private long _navigationId = 0;
        private object _detailContext = null;

        private GridLength _leftArea = new GridLength(1, GridUnitType.Star);
        private GridLength _rightArea = new GridLength(1, GridUnitType.Star);
        private GridLength _topArea = new GridLength(1, GridUnitType.Star);
        private GridLength _bottomArea = new GridLength(1, GridUnitType.Star);
    }
}
