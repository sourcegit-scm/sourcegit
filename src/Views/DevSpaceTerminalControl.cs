using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Rendering;

using Iciclecreek.Terminal;

namespace SourceGit.Views
{
    public sealed class DevSpaceTerminalView : TerminalView, ICustomHitTest
    {
        public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);
    }

    public sealed class DevSpaceTerminalControl : TerminalControl
    {
        public bool HasSelection => _view?.Terminal.Selection.HasSelection == true;

        public bool IsMouseReportingActive =>
            _view?.Terminal.MouseTrackingMode != XTerm.Input.MouseTrackingMode.None;

        public Task<bool> CopyAsync() =>
            _view?.CopyAsync() ?? Task.FromResult(false);

        public Task PasteAsync() =>
            _view?.PasteAsync() ?? Task.CompletedTask;

        public void SelectAll()
        {
            if (_view == null)
                return;

            _view.Terminal.Selection.SelectAll();
            _view.InvalidateVisual();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _view = e.NameScope.Find<DevSpaceTerminalView>("PART_TerminalView");
        }

        private DevSpaceTerminalView? _view;
    }
}
