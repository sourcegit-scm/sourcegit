using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SourceGit.Views;

// Linux.GetXdgDirectory only honors XDG roots that already exist.
// Create both app directories before setup to prevent fallback or legacy migration.
var outputRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("TEST_OUTPUT") ?? throw new Exception("Run this fixture using tests/TabDragFeedback/run.py"));
var configRoot = Path.Combine(outputRoot, "config");
var cacheRoot = Path.Combine(outputRoot, "cache");
Directory.CreateDirectory(Path.Combine(configRoot, "SourceGit"));
Directory.CreateDirectory(Path.Combine(cacheRoot, "SourceGit"));
Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configRoot);
Environment.SetEnvironmentVariable("XDG_CACHE_HOME", cacheRoot);
Environment.SetEnvironmentVariable("APPIMAGE", null);

Console.WriteLine("stage: directories");
SourceGit.Native.OS.SetupBasicDirectories();
if (SourceGit.Native.OS.BasicDirectories.ConfigDir != Path.Combine(configRoot, "SourceGit") || SourceGit.Native.OS.BasicDirectories.CacheDir != Path.Combine(cacheRoot, "SourceGit")) throw new Exception("Test directories are not isolated");
Console.WriteLine("stage: setup");
SourceGit.App.BuildAvaloniaApp().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
Console.WriteLine("stage: launcher");
var vm = new SourceGit.ViewModels.Launcher(null);
vm.AddNewTab();
vm.AddNewTab();
if (vm.Pages.Count != 3) throw new Exception("Expected exactly three isolated test pages");
var testCase = args.FirstOrDefault() ?? "long";
Console.WriteLine("case: " + testCase);
SourceGit.ViewModels.Preferences.Instance.UseFixedTabWidth = testCase != "variable";
if (testCase != "welcome")
{
    vm.Pages[0].Node.IsRepository = true;
    vm.Pages[0].Node.Name = testCase == "short"
        ? "SourceGit"
        : "sourcegit-repository-with-a-very-long-name-for-preview-validation";
}
var bar = new LauncherTabBar { DataContext = vm };
var window = new Window { Width = 850, Height = 180, Content = bar };
Console.WriteLine("stage: show");
window.Show();
Flush();
var list = bar.FindControl<ListBox>("LauncherTabsList");
var tabs = Enumerable.Range(0, vm.Pages.Count).Select(i => list.ContainerFromIndex(i).GetVisualDescendants().OfType<LauncherTabSizeBox>().First()).ToArray();
var layer = AdornerLayer.GetAdornerLayer(bar);
var initialAdorners = layer.Children.Count;
var first = vm.Pages[0];
var order = vm.Pages.Select(p => p.Node.Id).ToArray();
var format = Field<DataFormat<string>>("_dndMainTabFormat");
var data = new DataTransfer();
data.Add(DataTransferItem.Create(format, first.Node.Id));

Console.WriteLine("stage: begin feedback");
Call("BeginDragFeedback", tabs[0], first);
Flush();
Check(tabs[0].Classes.Contains("tab_drag_source"), "source is dimmed");
Check(Math.Abs(tabs[0].Opacity - 0.45) < 0.01, "source dimming is rendered");
Check(layer.Children.Count == initialAdorners + 1, "preview adorner attached");
var over = Drag(DragDrop.DragOverEvent, tabs[1]);
Call("DragOverTabs", bar, over);
Flush();
var previewForReview = Field<Border>("_dragPreview");
var titleForReview = previewForReview.GetVisualDescendants().OfType<TextBlock>().Single();
Console.WriteLine($"REVIEW preview width={previewForReview.Bounds.Width}, title width={titleForReview.Bounds.Width}, title desired width={titleForReview.DesiredSize.Width}, preview clips={previewForReview.ClipToBounds}");
var titlePosition = titleForReview.TranslatePoint(new Point(0, 0), previewForReview)!.Value;
Check(titlePosition.X >= 0 && titlePosition.X + titleForReview.Bounds.Width <= previewForReview.Bounds.Width + 0.01, "title stays within preview width");
Check(titleForReview.TextLayout.TextLines.Any(line => line.HasCollapsed) == (testCase == "long"), "long fixed-width title has ellipsis and fitting titles stay complete");
var bookmark = previewForReview.GetVisualDescendants().OfType<Bookmark>().FirstOrDefault();
var welcomeIcon = previewForReview.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().FirstOrDefault();
Check(testCase == "welcome" ? welcomeIcon != null : bookmark != null, "preview retains the correct repository or welcome icon");
Check(over.DragEffects == DragDropEffects.Move, "valid target accepts move");
Check(tabs[1].Classes.Contains("tab_drop_target"), "target highlighted");
Check(tabs[1].Background is ISolidColorBrush targetBrush &&
      bar.FindResource("Brush.AccentHovered") is ISolidColorBrush expectedBrush &&
      targetBrush.Color == expectedBrush.Color, "destination highlight is rendered");
Check(Field<Border>("_dragPreview").IsVisible && Field<Border>("_dropIndicator").IsVisible, "preview and insertion marker visible");
var expectedEdge = tabs[1].TranslatePoint(new Point(tabs[1].Bounds.Width, 2), bar)!.Value;
Check(Math.Abs(Canvas.GetLeft(Field<Border>("_dropIndicator")) - expectedEdge.X + 1.5) < 0.01, "rightward move marker matches insertion edge");
var screenshot = new RenderTargetBitmap(new PixelSize(850, 180));
screenshot.Render(window);
screenshot.Save(Path.Combine(Environment.GetEnvironmentVariable("TEST_OUTPUT")!, "feedback.png"));
screenshot.Dispose();

Call("DragLeaveTabs", bar, new DragEventArgs(DragDrop.DragLeaveEvent, data, bar, new Point(0, 0), KeyModifiers.None));
Check(!Field<Border>("_dragPreview").IsVisible && !tabs[1].Classes.Contains("tab_drop_target"), "leave clears feedback");
Call("DragOverTabs", bar, Drag(DragDrop.DragOverEvent, tabs[2]));
Check(tabs[2].Classes.Contains("tab_drop_target"), "enter another target restores feedback");
Call("ClearDragFeedback");
Check(order.SequenceEqual(vm.Pages.Select(p => p.Node.Id)), "cancel leaves order unchanged");
Check(layer.Children.Count == initialAdorners && Field<Border>("_dragPreview") == null, "cancel removes preview");
Check(Math.Abs(tabs[0].Opacity - 1) < 0.01 &&
      tabs[2].Background is ISolidColorBrush clearedTargetBrush &&
      clearedTargetBrush.Color == Colors.Transparent, "cancel restores rendered tab styles");

Console.WriteLine("stage: begin feedback");
Call("BeginDragFeedback", tabs[0], first);
Call("DragOverTabs", bar, Drag(DragDrop.DragOverEvent, tabs[1]));
Call("DropTab", tabs[1], Drag(DragDrop.DropEvent, tabs[1]));
Check(vm.Pages[1] == first && vm.Pages[0].Node.Id == order[1], "drop preserves existing reorder behavior");
Check(layer.Children.Count == initialAdorners && !tabs[0].Classes.Contains("tab_drag_source") && !tabs[1].Classes.Contains("tab_drop_target"), "drop clears all feedback");
Flush();
var source = list.ContainerFromIndex(1).GetVisualDescendants().OfType<LauncherTabSizeBox>().First();
var target = list.ContainerFromIndex(0).GetVisualDescendants().OfType<LauncherTabSizeBox>().First();
Call("BeginDragFeedback", source, first);
Call("DragOverTabs", bar, Drag(DragDrop.DragOverEvent, target));
expectedEdge = target.TranslatePoint(new Point(0, 2), bar)!.Value;
Check(Math.Abs(Canvas.GetLeft(Field<Border>("_dropIndicator")) - expectedEdge.X + 1.5) < 0.01, "leftward move marker matches insertion edge");
window.Close();
Check(Field<Border>("_dragPreview") == null, "detach clears preview");
Console.WriteLine("PASS all feedback lifecycle and reorder checks");

void Flush() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
T Field<T>(string name) => (T)typeof(LauncherTabBar).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bar)!;
void Call(string name, params object[] args) => typeof(LauncherTabBar).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(bar, args);
void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
DragEventArgs Drag(Avalonia.Interactivity.RoutedEvent<DragEventArgs> type, LauncherTabSizeBox target) => new(type, data, target, new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), KeyModifiers.None);
