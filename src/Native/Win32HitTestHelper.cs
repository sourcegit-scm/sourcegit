using Avalonia;

namespace SourceGit.Native
{
    /// <summary>
    /// Helper for Windows 11 Snap Layouts support with custom caption buttons.
    /// Based on Avalonia PR #17380.
    /// </summary>
    internal static class Win32HitTestHelper
    {
        /// <summary>
        /// Attached property to mark UI elements with their non-client hit test result.
        /// This enables Windows 11 Snap Layouts on custom caption buttons.
        /// </summary>
        public static readonly AttachedProperty<HitTestValue> HitTestResultProperty =
            AvaloniaProperty.RegisterAttached<Visual, HitTestValue>(
                "HitTestResult",
                typeof(Win32HitTestHelper),
                inherits: true,
                defaultValue: HitTestValue.Client);

        public static void SetHitTestResult(Visual element, HitTestValue value)
            => element.SetValue(HitTestResultProperty, value);

        public static HitTestValue GetHitTestResult(Visual element)
            => element.GetValue(HitTestResultProperty);

        /// <summary>
        /// Hit test values matching Windows WM_NCHITTEST return codes.
        /// </summary>
        public enum HitTestValue
        {
            Client = 1,
            Caption = 2,
            MinButton = 8,
            MaxButton = 9,
            Close = 20,
        }
    }
}
