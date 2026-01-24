#nullable enable

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace SourceGit.Commands
{
    /// <summary>
    /// Decodes human-readable names from Unreal Engine OFPA (One File Per Actor) .uasset files.
    /// These files have hashed names like "KCBX0GWLTFQT9RJ8M1LY8.uasset" in __ExternalActors__ folders.
    ///
    /// Algorithm:
    /// 1. Heuristic Header Scan - locates Name Map bypassing UE version differences
    /// 2. Index-based Search - finds ActorLabel/FolderLabel and StrProperty indices
    /// 3. Pattern Matching - finds 16-byte tag [Label_Index, 0, StrProperty_Index, 0]
    /// 4. Value Extraction - extracts the string value following the pattern
    ///
    /// Compatibility: UE 4.26 - 5.7+
    /// Performance: ~0.1 ms/file
    /// </summary>
    public static class DecodeOFPAPath
    {
        // Unreal Engine asset magic number (little-endian: 0x9E2A83C1)
        private static readonly byte[] UnrealMagic = { 0xC1, 0x83, 0x2A, 0x9E };

        private const int HeaderScanLimit = 1024;
        private const int MaxStringLength = 256;
        private const int PropertyTagWindow = 150;

        /// <summary>
        /// Result of decoding an OFPA file.
        /// </summary>
        public readonly struct DecodeResult : IEquatable<DecodeResult>
        {
            public string LabelType { get; }
            public string LabelValue { get; }

            public DecodeResult(string labelType, string labelValue)
            {
                LabelType = labelType;
                LabelValue = labelValue;
            }

            public bool Equals(DecodeResult other) =>
                LabelType == other.LabelType && LabelValue == other.LabelValue;

            public override bool Equals(object? obj) =>
                obj is DecodeResult other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(LabelType, LabelValue);

            public static bool operator ==(DecodeResult left, DecodeResult right) =>
                left.Equals(right);

            public static bool operator !=(DecodeResult left, DecodeResult right) =>
                !left.Equals(right);
        }

        /// <summary>
        /// Checks if the given path is an OFPA file (inside __ExternalActors__ or __ExternalObjects__ folder).
        /// </summary>
        public static bool IsOFPAFile(string path)
        {
            return path.Contains("__ExternalActors__", StringComparison.Ordinal) ||
                   path.Contains("__ExternalObjects__", StringComparison.Ordinal);
        }

        /// <summary>
        /// Decodes the actor/folder label from a .uasset file.
        /// </summary>
        /// <param name="filePath">Path to the .uasset file</param>
        /// <returns>Decoded label or null if file is invalid or not an OFPA file</returns>
        public static DecodeResult? Decode(string filePath)
        {
            try
            {
                var data = File.ReadAllBytes(filePath);
                return DecodeFromData(data);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Decodes the actor/folder label from raw .uasset file data.
        /// </summary>
        /// <param name="data">Raw bytes of the .uasset file</param>
        /// <returns>Decoded label or null if data is invalid</returns>
        public static DecodeResult? DecodeFromData(byte[] data)
        {
            if (data == null || data.Length < 20)
                return null;

            // Check magic number
            if (data[0] != UnrealMagic[0] || data[1] != UnrealMagic[1] ||
                data[2] != UnrealMagic[2] || data[3] != UnrealMagic[3])
                return null;

            return ParseUAsset(data);
        }

        private static DecodeResult? ParseUAsset(byte[] data)
        {
            int size = data.Length;

            // Fast path: find '/' to locate FolderName string
            int start = 20;
            int slashOff = FindByte(data, (byte)'/', 20, Math.Min(size, HeaderScanLimit));
            if (slashOff >= 24)
            {
                int pLen = ReadInt32(data, slashOff - 4);
                if (pLen > 0 && pLen < MaxStringLength)
                    start = slashOff - 4;
            }

            // Scan for name_count and name_offset
            int headerLen = Math.Min(size, HeaderScanLimit);
            int limit = headerLen - 20;
            int nameCount = 0;
            int nameOffset = 0;

            for (int off = start; off < limit; off++)
            {
                int pLen = ReadInt32(data, off);
                if (pLen > 0 && pLen < MaxStringLength)
                {
                    int strEnd = off + 4 + pLen;
                    if (strEnd > limit)
                        break;

                    byte ch = data[off + 4];
                    // Check for '/' or "None"
                    if (ch == 47 || (ch == 78 && MatchBytes(data, off + 4, "None")))
                    {
                        int baseOff = strEnd;
                        if (baseOff + 12 <= headerLen)
                        {
                            int nc = ReadInt32(data, baseOff + 4);
                            int no = ReadInt32(data, baseOff + 8);
                            if (nc > 0 && nc < 100000 && no > 0 && no < size)
                            {
                                nameCount = nc;
                                nameOffset = no;
                                break;
                            }
                        }
                    }
                }
            }

            if (nameCount == 0)
                return null;

            // Parse Name Map - find target indices
            int labelIdx = -1;
            int strIdx = -1;
            string? labelType = null;

            int pos = nameOffset;
            for (int i = 0; i < nameCount && pos + 4 <= size; i++)
            {
                int sLen = ReadInt32(data, pos);
                pos += 4;

                if (sLen > 0)
                {
                    int end = pos + sLen;
                    if (end > size)
                        break;

                    // Check for target strings
                    if (sLen == 11 && labelIdx < 0 && MatchBytes(data, pos, "ActorLabel"))
                    {
                        labelIdx = i;
                        labelType = "ActorLabel";
                    }
                    else if (sLen == 12)
                    {
                        if (MatchBytes(data, pos, "StrProperty"))
                        {
                            strIdx = i;
                        }
                        else if (labelIdx < 0 && MatchBytes(data, pos, "FolderLabel"))
                        {
                            labelIdx = i;
                            labelType = "FolderLabel";
                        }
                    }
                    else if (sLen == 6 && labelIdx < 0 && MatchBytes(data, pos, "Label"))
                    {
                        labelIdx = i;
                        labelType = "Label";
                    }

                    pos = end;

                    if (labelIdx >= 0 && strIdx >= 0)
                        break;
                }
                else if (sLen < 0)
                {
                    // UTF-16 string
                    pos += (-sLen) * 2;
                }

                // Skip hash value if present
                if (pos + 4 <= size)
                {
                    int nv = ReadInt32(data, pos);
                    if (nv == 0 || nv < -512 || nv > 512)
                        pos += 4;
                }
            }

            if (labelIdx < 0 || strIdx < 0 || labelType == null)
                return null;

            // Find property tag pattern: [labelIdx, 0, strIdx, 0]
            byte[] pattern = new byte[16];
            BinaryPrimitives.WriteInt32LittleEndian(pattern.AsSpan(0), labelIdx);
            BinaryPrimitives.WriteInt32LittleEndian(pattern.AsSpan(4), 0);
            BinaryPrimitives.WriteInt32LittleEndian(pattern.AsSpan(8), strIdx);
            BinaryPrimitives.WriteInt32LittleEndian(pattern.AsSpan(12), 0);

            int tagOff = FindPattern(data, pattern);
            if (tagOff == -1)
                return null;

            // Extract string value
            int searchStart = tagOff + 16;
            int searchEnd = Math.Min(searchStart + PropertyTagWindow, size);

            for (int i = searchStart; i < searchEnd - 4; i++)
            {
                int pLen = ReadInt32(data, i);

                if (pLen > 0 && pLen < 128)
                {
                    int strEnd = i + 4 + pLen - 1; // -1 for null terminator
                    if (strEnd <= searchEnd)
                    {
                        // Check if it's printable ASCII
                        bool valid = true;
                        for (int j = i + 4; j < strEnd && valid; j++)
                        {
                            byte b = data[j];
                            if (b < 32 || b > 126)
                                valid = false;
                        }

                        if (valid && strEnd > i + 4)
                        {
                            string value = Encoding.ASCII.GetString(data, i + 4, strEnd - i - 4);
                            return new DecodeResult(labelType, value);
                        }
                    }
                }
                else if (pLen < 0 && pLen > -128)
                {
                    // UTF-16 string
                    int strEnd = i + 4 + ((-pLen) * 2) - 2; // -2 for null terminator
                    if (strEnd <= searchEnd && strEnd > i + 4)
                    {
                        try
                        {
                            string value = Encoding.Unicode.GetString(data, i + 4, strEnd - i - 4);
                            return new DecodeResult(labelType, value);
                        }
                        catch
                        {
                            // Invalid UTF-16, continue searching
                        }
                    }
                }
            }

            return null;
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        }

        private static int FindByte(byte[] data, byte value, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (data[i] == value)
                    return i;
            }
            return -1;
        }

        private static bool MatchBytes(byte[] data, int offset, string str)
        {
            if (offset + str.Length > data.Length)
                return false;

            for (int i = 0; i < str.Length; i++)
            {
                if (data[offset + i] != (byte)str[i])
                    return false;
            }
            return true;
        }

        private static int FindPattern(byte[] data, byte[] pattern)
        {
            int end = data.Length - pattern.Length;
            for (int i = 0; i <= end; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length && match; j++)
                {
                    if (data[i + j] != pattern[j])
                        match = false;
                }
                if (match)
                    return i;
            }
            return -1;
        }
    }
}
