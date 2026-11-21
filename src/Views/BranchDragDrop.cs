using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace SourceGit.Views
{
    /// <summary>
    ///     Dragging a branch onto another branch (in the branch trees or in the commit graph) opens a context menu with
    ///     the actions between those two branches. This class holds the state of the ongoing drag and the shared visuals:
    ///     a label following the pointer and highlights for the dragged branch and the branch under the pointer.
    /// </summary>
    public static class BranchDragDrop
    {
        public static readonly DataFormat<string> Format = DataFormat.CreateStringApplicationFormat("sourcegit-dnd-branch");

        /// <summary>
        ///     Raised when a drag starts or ends, or when the drop target changes.
        /// </summary>
        public static event Action StateChanged;

        public static Models.Branch Source => s_state?.Branch;
        public static Models.Branch Target => s_state?.Target;

        public static async Task StartAsync(Control owner, PointerPressedEventArgs pressed, Point pointer, ViewModels.Repository repo, Models.Branch branch, Action<bool> highlightSource)
        {
            if (s_state != null)
                return;

            var topLevel = TopLevel.GetTopLevel(owner);
            if (topLevel == null)
                return;

            var state = new State(repo, branch, topLevel, highlightSource);
            s_state = state;
            state.Begin(owner, owner.TranslatePoint(pointer, state.Overlay) ?? pointer);
            StateChanged?.Invoke();

            try
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.Create(Format, branch.FullName));
                await DragDrop.DoDragDropAsync(pressed, data, DragDropEffects.Move);
            }
            finally
            {
                state.End();
                if (s_state == state)
                    s_state = null;
                StateChanged?.Invoke();
            }
        }

        /// <summary>
        ///     Updates the drop target from a drag-over event. `key` identifies the visual element showing `target` and
        ///     `highlight` toggles its highlight. Returns false if this drag is not a branch drag.
        /// </summary>
        public static bool HandleDragOver(DragEventArgs e, ViewModels.Repository repo, Models.Branch target, object key, Action<bool> highlight)
        {
            if (s_state == null || !e.DataTransfer.Contains(Format))
                return false;

            if (!CanDrop(repo, target))
            {
                target = null;
                key = null;
                highlight = null;
            }

            s_state.SetDropTarget(target, key, highlight);
            e.DragEffects = target != null ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
            return true;
        }

        public static void HandleDragLeave()
        {
            s_state?.SetDropTarget(null, null, null);
        }

        /// <summary>
        ///     Opens the context menu for the dragged branch and `target` at `point` (relative to `owner`).
        /// </summary>
        public static void HandleDrop(DragEventArgs e, Control owner, ViewModels.Repository repo, Models.Branch target)
        {
            if (s_state == null || e.DataTransfer.TryGetValue(Format) is not { Length: > 0 } sourceFullName)
                return;

            e.Handled = true;
            s_state.SetDropTarget(null, null, null);

            if (!CanDrop(repo, target))
                return;

            var source = repo.Branches.Find(x => x.FullName.Equals(sourceFullName, StringComparison.Ordinal));
            if (source == null)
                return;

            var point = e.GetPosition(owner);
            Dispatcher.UIThread.Post(() =>
            {
                var menu = CreateContextMenu(owner, repo, source, target);
                menu.Placement = PlacementMode.AnchorAndGravity;
                menu.PlacementAnchor = PopupAnchor.TopLeft;
                menu.PlacementGravity = PopupGravity.BottomRight;
                menu.PlacementRect = new Rect(point, new Size(1, 1));
                menu.Open(owner);
            });
        }

        /// <summary>
        ///     Finds the branch shown by a decorator in the commit graph. Returns null for tags and detached HEADs.
        /// </summary>
        public static Models.Branch FindBranch(ViewModels.Repository repo, Models.Decorator decorator)
        {
            if (repo == null || decorator == null)
                return null;

            return decorator.Type switch
            {
                Models.DecoratorType.CurrentBranchHead or Models.DecoratorType.LocalBranchHead =>
                    repo.Branches.Find(x => x.IsLocal && x.Name.Equals(decorator.Name, StringComparison.Ordinal)),
                Models.DecoratorType.RemoteBranchHead =>
                    repo.Branches.Find(x => !x.IsLocal && x.FriendlyName.Equals(decorator.Name, StringComparison.Ordinal)),
                _ => null,
            };
        }

        /// <summary>
        ///     Whether a decorator in the commit graph shows `branch`.
        /// </summary>
        public static bool IsDecoratorOf(Models.Decorator decorator, Models.Branch branch)
        {
            if (decorator == null || branch == null)
                return false;

            return decorator.Type switch
            {
                Models.DecoratorType.CurrentBranchHead or Models.DecoratorType.LocalBranchHead =>
                    branch.IsLocal && branch.Name.Equals(decorator.Name, StringComparison.Ordinal),
                Models.DecoratorType.RemoteBranchHead =>
                    !branch.IsLocal && branch.FriendlyName.Equals(decorator.Name, StringComparison.Ordinal),
                _ => false,
            };
        }

        private static bool CanDrop(ViewModels.Repository repo, Models.Branch target)
        {
            return s_state != null &&
                target != null &&
                repo == s_state.Repo &&
                !target.FullName.Equals(s_state.Branch.FullName, StringComparison.Ordinal);
        }

        private static ContextMenu CreateContextMenu(Control owner, ViewModels.Repository repo, Models.Branch source, Models.Branch target)
        {
            var menu = new ContextMenu();

            var compare = new MenuItem();
            compare.Header = App.Text("BranchCM.CompareBranches", source.FriendlyName, target.FriendlyName);
            compare.Icon = owner.CreateMenuIcon("Icons.Compare");
            compare.Click += (_, e) =>
            {
                owner.ShowWindow(new ViewModels.Compare(repo, target, source));
                e.Handled = true;
            };
            menu.Items.Add(compare);

            if (!repo.IsBare)
            {
                var merge = new MenuItem();
                merge.Header = App.Text("BranchCM.Merge", source.FriendlyName, target.FriendlyName);
                merge.Icon = owner.CreateMenuIcon("Icons.Merge");
                merge.IsEnabled = target.IsCurrent;
                merge.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Merge(repo, source, target.Name, false));
                    e.Handled = true;
                };

                var rebase = new MenuItem();
                rebase.Header = App.Text("BranchCM.Rebase", source.FriendlyName, target.FriendlyName);
                rebase.Icon = owner.CreateMenuIcon("Icons.Rebase");
                rebase.IsEnabled = source.IsCurrent;
                rebase.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Rebase(repo, source, target));
                    e.Handled = true;
                };

                var interactiveRebase = new MenuItem();
                interactiveRebase.Header = App.Text("BranchCM.InteractiveRebase.Manually", source.FriendlyName, target.FriendlyName);
                interactiveRebase.Icon = owner.CreateMenuIcon("Icons.InteractiveRebase");
                interactiveRebase.IsEnabled = source.IsCurrent && !source.Head.Equals(target.Head, StringComparison.Ordinal);
                interactiveRebase.Click += async (_, e) =>
                {
                    var commit = await new Commands.QuerySingleCommit(repo.FullPath, target.Head).GetResultAsync();
                    await owner.ShowDialogAsync(new ViewModels.InteractiveRebase(repo, commit));
                    e.Handled = true;
                };

                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(merge);
                menu.Items.Add(rebase);
                menu.Items.Add(interactiveRebase);
            }

            // A pull request needs both branches on a remote. Local branches are resolved to their upstream.
            var remoteTarget = target.IsLocal ? FindUpstream(repo, target) : target;
            var targetRemote = remoteTarget != null ? repo.Remotes.Find(x => x.Name.Equals(remoteTarget.Remote, StringComparison.Ordinal)) : null;
            if (targetRemote != null)
            {
                var remoteSource = source.IsLocal ? FindUpstream(repo, source) : source;
                var sourceRemote = remoteSource != null ? repo.Remotes.Find(x => x.Name.Equals(remoteSource.Remote, StringComparison.Ordinal)) : null;

                string prURL = null;
                var canCreatePR = sourceRemote != null &&
                    !remoteSource.FullName.Equals(remoteTarget.FullName, StringComparison.Ordinal) &&
                    targetRemote.TryGetCreatePullRequestURL(out prURL, remoteSource.Name, remoteTarget.Name, sourceRemote);

                // Only offer the action when the target's host supports it. If the source is not pushed yet, show it disabled.
                if (canCreatePR || (remoteSource == null && targetRemote.TryGetCreatePullRequestURL(out _, source.Name, remoteTarget.Name, null)))
                {
                    var createPR = new MenuItem();
                    createPR.Header = App.Text("BranchCM.CreatePRFromTo", remoteSource?.FriendlyName ?? source.FriendlyName, remoteTarget.FriendlyName);
                    createPR.Icon = owner.CreateMenuIcon("Icons.CreatePR");
                    createPR.IsEnabled = canCreatePR;
                    createPR.Click += (_, e) =>
                    {
                        Native.OS.OpenBrowser(prURL);
                        e.Handled = true;
                    };

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(createPR);
                }
            }

            return menu;
        }

        private static Models.Branch FindUpstream(ViewModels.Repository repo, Models.Branch branch)
        {
            if (string.IsNullOrEmpty(branch.Upstream))
                return null;

            return repo.Branches.Find(x => x.FullName.Equals(branch.Upstream, StringComparison.Ordinal));
        }

        private class State
        {
            public ViewModels.Repository Repo { get; }
            public Models.Branch Branch { get; }
            public Models.Branch Target { get; private set; }
            public OverlayLayer Overlay { get; }

            public State(ViewModels.Repository repo, Models.Branch branch, TopLevel topLevel, Action<bool> highlightSource)
            {
                Repo = repo;
                Branch = branch;
                Overlay = OverlayLayer.GetOverlayLayer(topLevel);
                _topLevel = topLevel;
                _highlightSource = highlightSource;
            }

            public void Begin(Control owner, Point pointer)
            {
                _highlightSource?.Invoke(true);

                _label = new TextBlock()
                {
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                };

                var icon = new Path()
                {
                    Width = 12,
                    Height = 12,
                    Fill = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    Data = owner.FindResource("Icons.Branch") as StreamGeometry,
                };

                var panel = new StackPanel() { Orientation = Orientation.Horizontal };
                panel.Children.Add(icon);
                panel.Children.Add(_label);

                _ghost = new Border()
                {
                    Background = owner.FindResource("Brush.Accent") as IBrush ?? Brushes.DodgerBlue,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4),
                    BoxShadow = BoxShadows.Parse("0 2 8 0 #60000000"),
                    IsHitTestVisible = false,
                    Child = panel,
                };

                UpdateLabel();
                MoveGhost(pointer);
                Overlay?.Children.Add(_ghost);

                // The drop targets only receive drag events while the pointer is above them. Temporarily accept
                // drags on the whole window so the label keeps following the pointer everywhere.
                _topLevelAllowDrop = DragDrop.GetAllowDrop(_topLevel);
                DragDrop.SetAllowDrop(_topLevel, true);
                _topLevel.AddHandler(DragDrop.DragOverEvent, OnTopLevelDragOver, RoutingStrategies.Bubble, true);
                _topLevel.AddHandler(DragDrop.DragLeaveEvent, OnTopLevelDragLeave, RoutingStrategies.Bubble, true);
                _topLevel.AddHandler(DragDrop.DropEvent, OnTopLevelDrop, RoutingStrategies.Bubble, true);
            }

            public void End()
            {
                _topLevel.RemoveHandler(DragDrop.DragOverEvent, OnTopLevelDragOver);
                _topLevel.RemoveHandler(DragDrop.DragLeaveEvent, OnTopLevelDragLeave);
                _topLevel.RemoveHandler(DragDrop.DropEvent, OnTopLevelDrop);
                DragDrop.SetAllowDrop(_topLevel, _topLevelAllowDrop);

                if (_ghost != null)
                    Overlay?.Children.Remove(_ghost);

                SetDropTarget(null, null, null);
                _highlightSource?.Invoke(false);
            }

            public void SetDropTarget(Models.Branch target, object key, Action<bool> highlight)
            {
                if (_targetKey == key && Target == target)
                    return;

                _highlightTarget?.Invoke(false);

                Target = target;
                _targetKey = key;
                _highlightTarget = highlight;

                _highlightTarget?.Invoke(true);

                UpdateLabel();
                StateChanged?.Invoke();
            }

            private void UpdateLabel()
            {
                if (_label != null)
                    _label.Text = Target != null ? $"{Branch.FriendlyName}  →  {Target.FriendlyName}" : Branch.FriendlyName;
            }

            private void MoveGhost(Point pointer)
            {
                if (_ghost == null)
                    return;

                Canvas.SetLeft(_ghost, pointer.X + 14);
                Canvas.SetTop(_ghost, pointer.Y + 10);
            }

            private void OnTopLevelDragOver(object sender, DragEventArgs e)
            {
                if (!e.DataTransfer.Contains(Format))
                    return;

                if (_ghost != null && Overlay != null)
                {
                    _ghost.IsVisible = true;
                    MoveGhost(e.GetPosition(Overlay));
                }

                if (!e.Handled)
                {
                    // Not above a branch of the same repository.
                    SetDropTarget(null, null, null);
                    e.DragEffects = DragDropEffects.None;
                    e.Handled = true;
                }
            }

            private void OnTopLevelDragLeave(object sender, DragEventArgs e)
            {
                if (_ghost != null)
                    _ghost.IsVisible = false;
            }

            private void OnTopLevelDrop(object sender, DragEventArgs e)
            {
                if (e.DataTransfer.Contains(Format))
                    e.Handled = true;
            }

            private readonly TopLevel _topLevel;
            private readonly Action<bool> _highlightSource;
            private bool _topLevelAllowDrop = false;
            private object _targetKey = null;
            private Action<bool> _highlightTarget = null;
            private Border _ghost = null;
            private TextBlock _label = null;
        }

        private static State s_state = null;
    }
}
