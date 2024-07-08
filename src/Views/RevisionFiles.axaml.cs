using System;
using System.Collections.Generic;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace SourceGit.Views
{
    public class RevisionFileTreeNode
    {
        public Models.Object Backend { get; set; } = null;
        public bool IsExpanded { get; set; } = false;
        public List<RevisionFileTreeNode> Children { get; set; } = new List<RevisionFileTreeNode>();

        public bool IsFolder => Backend != null && Backend.Type == Models.ObjectType.Tree;
        public string Name => Backend != null ? Path.GetFileName(Backend.Path) : string.Empty;
    }

    public class RevisionFileTreeView : UserControl
    {
        public static readonly StyledProperty<string> RevisionProperty =
            AvaloniaProperty.Register<RevisionFileTreeView, string>(nameof(Revision), null);

        public string Revision
        {
            get => GetValue(RevisionProperty);
            set => SetValue(RevisionProperty, value);
        }

        public Models.Object SelectedObject
        {
            get;
            private set;
        } = null;

        protected override Type StyleKeyOverride => typeof(UserControl);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == RevisionProperty)
            {
                SelectedObject = null;

                if (Content is TreeDataGrid tree && tree.Source is IDisposable disposable)
                    disposable.Dispose();

                var vm = DataContext as ViewModels.CommitDetail;
                if (vm == null || vm.Commit == null)
                {
                    Content = null;
                    GC.Collect();
                    return;
                }

                var objects = vm.GetRevisionFilesUnderFolder(null);
                if (objects == null || objects.Count == 0)
                {
                    Content = null;
                    GC.Collect();
                    return;
                }

                var toplevelObjects = new List<RevisionFileTreeNode>();
                foreach (var obj in objects)
                    toplevelObjects.Add(new RevisionFileTreeNode() { Backend = obj });

                toplevelObjects.Sort((l, r) =>
                {
                    if (l.IsFolder == r.IsFolder)
                        return l.Name.CompareTo(r.Name);
                    return l.IsFolder ? -1 : 1;
                });

                var template = this.FindResource("RevisionFileTreeNodeTemplate") as IDataTemplate;
                var source = new HierarchicalTreeDataGridSource<RevisionFileTreeNode>(toplevelObjects)
                {
                    Columns =
                    {
                        new HierarchicalExpanderColumn<RevisionFileTreeNode>(
                            new TemplateColumn<RevisionFileTreeNode>(null, template, null, GridLength.Auto),
                            GetChildrenOfTreeNode,
                            x => x.IsFolder,
                            x => x.IsExpanded)
                    }
                };

                var selection = new Models.TreeDataGridSelectionModel<RevisionFileTreeNode>(source, GetChildrenOfTreeNode);
                selection.SingleSelect = true;
                selection.SelectionChanged += (s, _) =>
                {
                    if (s is Models.TreeDataGridSelectionModel<RevisionFileTreeNode> model)
                    {
                        var node = model.SelectedItem;
                        var detail = DataContext as ViewModels.CommitDetail;

                        if (node != null && !node.IsFolder)
                        {
                            SelectedObject = node.Backend;
                            detail.ViewRevisionFile(node.Backend);
                        }
                        else
                        {
                            SelectedObject = null;
                            detail.ViewRevisionFile(null);
                        }
                    }
                };

                source.Selection = selection;
                Content = new TreeDataGrid()
                {
                    AutoDragDropRows = false,
                    ShowColumnHeaders = false,
                    CanUserResizeColumns = false,
                    CanUserSortColumns = false,
                    Source = source,
                };

                GC.Collect();
            }
        }

        private List<RevisionFileTreeNode> GetChildrenOfTreeNode(RevisionFileTreeNode node)
        {
            if (!node.IsFolder)
                return null;

            if (node.Children.Count > 0)
                return node.Children;

            var vm = DataContext as ViewModels.CommitDetail;
            if (vm == null)
                return null;

            var objects = vm.GetRevisionFilesUnderFolder(node.Backend.Path + "/");
            if (objects == null || objects.Count == 0)
                return null;

            foreach (var obj in objects)
                node.Children.Add(new RevisionFileTreeNode() { Backend = obj });

            node.Children.Sort((l, r) =>
            {
                if (l.IsFolder == r.IsFolder)
                    return l.Name.CompareTo(r.Name);
                return l.IsFolder ? -1 : 1;
            });

            return node.Children;
        }
    }

    public class RevisionTextFileView : TextEditor
    {
        protected override Type StyleKeyOverride => typeof(TextEditor);

        public RevisionTextFileView() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = true;
            WordWrap = false;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            TextArea.LeftMargins[0].Margin = new Thickness(8, 0);
            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
            GC.Collect();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            var source = DataContext as Models.RevisionTextFile;
            if (source != null)
                Text = source.Content;
            else
                Text = string.Empty;
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected))
                return;

            var icon = new Avalonia.Controls.Shapes.Path();
            icon.Width = 10;
            icon.Height = 10;
            icon.Stretch = Stretch.Uniform;
            icon.Data = App.Current?.FindResource("Icons.Copy") as StreamGeometry;

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = icon;
            copy.Click += (o, ev) =>
            {
                App.CopyText(selected);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);

            TextArea.TextView.OpenContextMenu(menu);
            e.Handled = true;
        }
    }

    public partial class RevisionFiles : UserControl
    {
        public RevisionFiles()
        {
            InitializeComponent();
        }

        private void OnRevisionFileTreeViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail vm && sender is RevisionFileTreeView view)
            {
                if (view.SelectedObject != null && view.SelectedObject.Type != Models.ObjectType.Tree)
                {
                    var menu = vm.CreateRevisionFileContextMenu(view.SelectedObject);
                    view.OpenContextMenu(menu);
                }
            }

            e.Handled = true;
        }
    }
}
