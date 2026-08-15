using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ImageDiffView : UserControl
    {
        public ImageDiffView()
        {
            InitializeComponent();
        }

        private void OnZoomOut(object sender, RoutedEventArgs e)
        {
            ImageZoomSlider.Value = Math.Max(0.5, Math.Round(ImageZoomSlider.Value - 0.25, 2));
        }

        private void OnZoomIn(object sender, RoutedEventArgs e)
        {
            ImageZoomSlider.Value = Math.Min(8.0, Math.Round(ImageZoomSlider.Value + 0.25, 2));
        }

        private void OnZoomReset(object sender, RoutedEventArgs e)
        {
            ImageZoomSlider.Value = 1.0;
        }

        private void OnImagePointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (e.Delta.Y > 0)
                    ImageZoomSlider.Value = Math.Min(8.0, Math.Round(ImageZoomSlider.Value + 0.25, 2));
                else if (e.Delta.Y < 0)
                    ImageZoomSlider.Value = Math.Max(0.5, Math.Round(ImageZoomSlider.Value - 0.25, 2));

                e.Handled = true;
            }
        }

        private bool _isDraggingImage = false;
        private Point _dragStartPoint;
        private Vector _dragStartOffset;
        private ScrollViewer _activeDragScrollViewer;

        private void OnImagePointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is ScrollViewer sv && e.GetCurrentPoint(sv).Properties.IsLeftButtonPressed)
            {
                if (sv.Extent.Width > sv.Viewport.Width || sv.Extent.Height > sv.Viewport.Height)
                {
                    _isDraggingImage = true;
                    _activeDragScrollViewer = sv;
                    _dragStartPoint = e.GetPosition(sv);
                    _dragStartOffset = sv.Offset;
                    sv.Cursor = new Cursor(StandardCursorType.Hand);
                    e.Pointer.Capture(sv);
                }
            }
        }

        private void OnImagePointerMoved(object sender, PointerEventArgs e)
        {
            if (_isDraggingImage && _activeDragScrollViewer != null && sender is ScrollViewer sv && sv == _activeDragScrollViewer)
            {
                var point = e.GetPosition(sv);
                var deltaX = _dragStartPoint.X - point.X;
                var deltaY = _dragStartPoint.Y - point.Y;

                var newX = Math.Clamp(_dragStartOffset.X + deltaX, 0, Math.Max(0, sv.Extent.Width - sv.Viewport.Width));
                var newY = Math.Clamp(_dragStartOffset.Y + deltaY, 0, Math.Max(0, sv.Extent.Height - sv.Viewport.Height));

                sv.Offset = new Vector(newX, newY);
            }
        }

        private void OnImagePointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_isDraggingImage && sender is ScrollViewer sv)
            {
                _isDraggingImage = false;
                _activeDragScrollViewer = null;
                sv.Cursor = null;
                e.Pointer.Capture(null);
            }
        }

        private void OnImagePointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            if (_isDraggingImage && sender is ScrollViewer sv)
            {
                _isDraggingImage = false;
                _activeDragScrollViewer = null;
                sv.Cursor = null;
            }
        }

        private bool _isSyncingScroll = false;

        private void OnOldImageScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll || OldImageScrollViewer == null || NewImageScrollViewer == null)
                return;

            _isSyncingScroll = true;
            NewImageScrollViewer.Offset = OldImageScrollViewer.Offset;
            _isSyncingScroll = false;
        }

        private void OnNewImageScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll || OldImageScrollViewer == null || NewImageScrollViewer == null)
                return;

            _isSyncingScroll = true;
            OldImageScrollViewer.Offset = NewImageScrollViewer.Offset;
            _isSyncingScroll = false;
        }
    }
}
