using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SourceGit.Views;
using Preferences = SourceGit.ViewModels.Preferences;
using SourceGit.Models;

SourceGit.Native.OS.SetupBasicDirectories();
AppBuilder.Configure<SourceGit.App>().WithInterFont().UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var window = new Window { Width = 600, Height = 350 };
window.Show();
MenuChecks.Run(window);
window.Close();

static class MenuChecks
{
    static void Pump() { using var cancellation = new CancellationTokenSource(650); Dispatcher.UIThread.MainLoop(cancellation.Token); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS menu: " + message); }
    static void Click(Control control)
    {
        var top = TopLevel.GetTopLevel(control);
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), top).Value;
        top.MouseMove(point);
        top.MouseDown(point, MouseButton.Left);
        top.MouseUp(point, MouseButton.Left);
        Pump();
    }
    public static void Run(Window window)
    {
        var prior = Preferences.Instance.EnableCompactFoldersInChangesTree;
        var view = new ChangeViewModeSwitcher { ViewMode = ChangeViewMode.Grid, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        window.Content = view;
        Pump();
        var button = view.GetVisualDescendants().OfType<Button>().Single();
        var flyout = (MenuFlyout)button.Flyout;
        Check(flyout.Items.Count == 3, "exactly three root modes");
        var list = (MenuItem)flyout.Items[0];
        var grid = (MenuItem)flyout.Items[1];
        var tree = (MenuItem)flyout.Items[2];
        Check(tree.Items.Count == 1, "Tree has only the requested compact-folders child");
        foreach (var item in new[] { list, grid, tree })
            Check(item.Icon is Avalonia.Controls.Shapes.Path && item.ToggleType == MenuItemToggleType.None, "original mode icon, no radio toggle");
        var compact = (MenuItem)tree.Items[0];
        Preferences.Instance.EnableCompactFoldersInChangesTree = false;
        Pump();
        void OpenTree()
        {
            flyout.ShowAt(button);
            Pump();
            var top = TopLevel.GetTopLevel(tree);
            top.MouseMove(new Point(0, 0));
            Pump();
            top.MouseMove(tree.TranslatePoint(new Point(40, 14), top).Value);
            Pump();
            Check(tree.IsSubMenuOpen, "hover opens Tree submenu");
        }
        OpenTree();
        Check(view.ViewMode == ChangeViewMode.Grid, "hover does not select Tree");

        Check(compact.IsChecked == false && ((CheckBox)compact.Icon).IsChecked == false, "unchecked state visible");
        Click(compact);
        Check(Preferences.Instance.EnableCompactFoldersInChangesTree && compact.IsChecked && ((CheckBox)compact.Icon).IsChecked == true, "pointer toggles preference and visible checkbox on");
        Check(view.ViewMode == ChangeViewMode.Grid, "checkbox does not change mode");
        OpenTree();
        Check(compact.IsChecked && ((CheckBox)compact.Icon).IsChecked == true, "reopening retains checked state");
        if (Environment.GetEnvironmentVariable("SOURCEGIT_MENU_SCREENSHOT") is { Length: > 0 } screenshot)
            window.CaptureRenderedFrame().Save(screenshot);
        compact.Focus();
        var keyTop = TopLevel.GetTopLevel(compact);
        keyTop.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        keyTop.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        Check(!Preferences.Instance.EnableCompactFoldersInChangesTree && !compact.IsChecked && ((CheckBox)compact.Icon).IsChecked == false, "keyboard Enter toggles checkbox off");
        Check(view.ViewMode == ChangeViewMode.Grid, "keyboard toggle does not change mode");
        OpenTree();
        Click(tree);
        Check(view.ViewMode == ChangeViewMode.Tree, "Tree parent pointer selects mode");
        Check(!flyout.IsOpen, "Tree parent activation closes menu");
        flyout.ShowAt(button);
        Pump();
        Click(list);
        Check(view.ViewMode == ChangeViewMode.List, "List selection remains functional");
        flyout.ShowAt(button);
        Pump();
        Click(grid);
        Check(view.ViewMode == ChangeViewMode.Grid, "Grid remains selectable after Tree");
        foreach (var key in new[] { PhysicalKey.Enter, PhysicalKey.Space })
        {
            view.ViewMode = ChangeViewMode.Grid;
            flyout.ShowAt(button);
            Pump();
            tree.Focus();
            var top = TopLevel.GetTopLevel(tree);
            top.KeyPressQwerty(key, RawInputModifiers.None);
            top.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
            Check(view.ViewMode == ChangeViewMode.Tree && !flyout.IsOpen, "Tree parent keyboard " + key + " selects and closes");
        }
        view.ViewMode = ChangeViewMode.List;
        flyout.ShowAt(button);
        Pump();
        tree.Focus();
        var treeTop = TopLevel.GetTopLevel(tree);
        treeTop.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        treeTop.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Pump();
        Check(tree.IsSubMenuOpen && view.ViewMode == ChangeViewMode.List, "Right Arrow opens submenu without changing mode");
        compact.Focus();
        treeTop.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        treeTop.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();
        Check(view.ViewMode == ChangeViewMode.List && !Preferences.Instance.EnableCompactFoldersInChangesTree, "Escape does not change mode or preference");
        OpenTree();
        Click(compact);
        OpenTree();
        Click(compact);
        Check(view.ViewMode == ChangeViewMode.List && !Preferences.Instance.EnableCompactFoldersInChangesTree, "repeated checkbox activation changes only preference");
        flyout.Hide();
        Preferences.Instance.EnableCompactFoldersInChangesTree = prior;
        Pump();
    }
}
