using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SourceGit.Commands
{
    public static class SaveDiscardOnTrashWindows
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        private const uint FO_DELETE = 0x0003;    // Delete operation
        private const uint FOF_ALLOWUNDO = 0x0040; // Put in Trash
        private const uint FOF_NOCONFIRMATION = 0x0010; // No confirmation

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        public static bool MoveFileToTrash(string file)
        {
            SHFILEOPSTRUCT fileOp = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = file + '\0',
                pTo = null,
                fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION),
                fAnyOperationsAborted = false,
                hNameMappings = IntPtr.Zero,
                lpszProgressTitle = null
            };

            bool result = SHFileOperation(ref fileOp) == 0;

            if (result)
                File.Delete(file);

            return result;
        }
    }
}
