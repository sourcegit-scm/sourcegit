using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Avalonia.Media.Imaging;

namespace SourceGit.Native
{
    [SupportedOSPlatform("windows")]
    public static class SystemFileIcon
    {
        private const int MAX_PATH = 260;
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        private const int DI_NORMAL = 0x0003;

        private static readonly Dictionary<string, Bitmap> _cache = new(StringComparer.OrdinalIgnoreCase);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFOW
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfoW(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFOW psfi,
            uint cbSizeFileInfo,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DrawIconEx(
            IntPtr hdc,
            int xLeft,
            int yTop,
            IntPtr hIcon,
            int cxWidth,
            int cyHeight,
            int istepIfAniCur,
            IntPtr hbrFlickerFreeDraw,
            int diFlags);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetDIBits(
            IntPtr hdc,
            IntPtr hbmp,
            uint uStartScan,
            uint cScanLines,
            byte[] lpvBits,
            ref BITMAPINFO lpbi,
            uint uUsage);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RGBQUAD
        {
            public byte rgbBlue;
            public byte rgbGreen;
            public byte rgbRed;
            public byte rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public RGBQUAD[] bmiColors;
        }

        private const uint BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr GetStockObject(int fnObject);

        private const int WHITE_BRUSH = 0;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        /// <summary>
        /// Gets the system file icon for a given file path, cached by extension.
        /// Returns null if not on Windows or icon cannot be retrieved.
        /// </summary>
        public static Bitmap GetIcon(string filePath)
        {
            if (!OperatingSystem.IsWindows())
                return null;

            if (string.IsNullOrEmpty(filePath))
                return null;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                ext = ".*"; // fallback for files without extension

            lock (_cache)
            {
                if (_cache.TryGetValue(ext, out var cached))
                    return cached;
            }

            var icon = LoadIconForExtension(ext);

            lock (_cache)
            {
                _cache[ext] = icon;
            }

            return icon;
        }

        /// <summary>
        /// Clears the icon cache. Useful when system theme changes.
        /// </summary>
        public static void ClearCache()
        {
            lock (_cache)
            {
                foreach (var bmp in _cache.Values)
                    bmp?.Dispose();
                _cache.Clear();
            }
        }

        private static Bitmap LoadIconForExtension(string ext)
        {
            var shInfo = new SHFILEINFOW();
            var shInfoSize = (uint)Marshal.SizeOf(shInfo);

            // Use a dummy path with the target extension - SHGFI_USEFILEATTRIBUTES means
            // the file doesn't need to actually exist
            var dummyPath = "dummy" + ext;

            var result = SHGetFileInfoW(
                dummyPath,
                FILE_ATTRIBUTE_NORMAL,
                ref shInfo,
                shInfoSize,
                SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | SHGFI_SMALLICON);

            if (result == IntPtr.Zero || shInfo.hIcon == IntPtr.Zero)
                return null;

            try
            {
                return ConvertHIconToBitmap(shInfo.hIcon, 16, 16);
            }
            finally
            {
                DestroyIcon(shInfo.hIcon);
            }
        }

        private static Bitmap ConvertHIconToBitmap(IntPtr hIcon, int width, int height)
        {
            // Create a compatible DC and bitmap for drawing
            IntPtr screenDC = GetDC(IntPtr.Zero);
            if (screenDC == IntPtr.Zero)
                return null;

            IntPtr memDC = CreateCompatibleDC(screenDC);
            if (memDC == IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, screenDC);
                return null;
            }

            IntPtr hBitmap = CreateCompatibleBitmap(screenDC, width, height);
            if (hBitmap == IntPtr.Zero)
            {
                DeleteDC(memDC);
                ReleaseDC(IntPtr.Zero, screenDC);
                return null;
            }

            IntPtr oldBmp = SelectObject(memDC, hBitmap);

            // Fill background with white (for transparency handling)
            IntPtr hBrush = GetStockObject(WHITE_BRUSH);
            // We skip filling - DrawIconEx handles transparency

            // Draw the icon onto our bitmap
            bool drawn = DrawIconEx(memDC, 0, 0, hIcon, width, height, 0, IntPtr.Zero, DI_NORMAL);

            SelectObject(memDC, oldBmp);
            DeleteDC(memDC);
            ReleaseDC(IntPtr.Zero, screenDC);

            if (!drawn)
            {
                DeleteObject(hBitmap);
                return null;
            }

            // Extract pixel data using GetDIBits
            try
            {
                return CreateBitmapFromHBitmap(hBitmap, width, height);
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        private static Bitmap CreateBitmapFromHBitmap(IntPtr hBitmap, int width, int height)
        {
            // Set up BITMAPINFO for 32-bit RGBA
            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // Negative = top-down DIB
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB,
                    biSizeImage = (uint)(width * height * 4),
                },
                bmiColors = new RGBQUAD[256],
            };

            // GetDIBits needs a DC
            IntPtr screenDC = GetDC(IntPtr.Zero);
            if (screenDC == IntPtr.Zero)
                return null;

            var pixels = new byte[width * height * 4];

            int lines = GetDIBits(screenDC, hBitmap, 0, (uint)height, pixels, ref bmi, DIB_RGB_COLORS);
            ReleaseDC(IntPtr.Zero, screenDC);

            if (lines <= 0)
                return null;

            // GetDIBits returns BGRA format, convert to RGBA for Avalonia
            var rgbaPixels = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                int srcIdx = i * 4;
                int dstIdx = i * 4;
                // BGRA -> RGBA
                rgbaPixels[dstIdx + 0] = pixels[srcIdx + 2]; // R
                rgbaPixels[dstIdx + 1] = pixels[srcIdx + 1]; // G
                rgbaPixels[dstIdx + 2] = pixels[srcIdx + 0]; // B
                rgbaPixels[dstIdx + 3] = pixels[srcIdx + 3]; // A
            }

            var handle = GCHandle.Alloc(rgbaPixels, GCHandleType.Pinned);
            try
            {
                return new Bitmap(
                    Avalonia.Platform.PixelFormat.Rgba8888,
                    Avalonia.Platform.AlphaFormat.Premul,
                    handle.AddrOfPinnedObject(),
                    new Avalonia.PixelSize(width, height),
                    new Avalonia.Vector(96, 96),
                    width * 4);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
