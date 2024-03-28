#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Native.WindowsNative
{
    [SupportedOSPlatform("windows")]
    internal static class SystemParameters
    {
        public static string? GetSystemDefaultFontName()
        {
            var ncm = new NONCLIENTMETRICS
            {
                cbSize = Marshal.SizeOf(typeof(NONCLIENTMETRICS))
            };

            return SystemParametersInfo(SPI_GETNONCLIENTMETRICS, ncm.cbSize, ref ncm, 0)
                ? ncm.lfMessageFont.lfFaceName
                : null;
        }

        private const int SPI_GETNONCLIENTMETRICS = 0x0029;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(int uAction, int uParam, ref NONCLIENTMETRICS lpvParam, int fuWinIni);

        [StructLayout(LayoutKind.Sequential)]
        internal struct NONCLIENTMETRICS
        {
            public int cbSize;
            public int iBorderWidth;
            public int iScrollWidth;
            public int iScrollHeight;
            public int iCaptionWidth;
            public int iCaptionHeight;
            public LOGFONT lfCaptionFont;
            public int iSmCaptionWidth;
            public int iSmCaptionHeight;
            public LOGFONT lfSmCaptionFont;
            public int iMenuWidth;
            public int iMenuHeight;
            public LOGFONT lfMenuFont;
            public LOGFONT lfStatusFont;
            public LOGFONT lfMessageFont;
            [SupportedOSPlatform("windows6.0.6000" /* Vista */)]
            public int iPaddedBorderWidth;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct LOGFONT
        {
            public int lfHeight;
            public int lfWidth;
            public int lfEscapement;
            public int lfOrientation;
            public int lfWeight;
            public byte lfItalic;
            public byte lfUnderline;
            public byte lfStrikeOut;
            public byte lfCharSet;
            public byte lfOutPrecision;
            public byte lfClipPrecision;
            public byte lfQuality;
            public byte lfPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string lfFaceName;
        }
    }
}
