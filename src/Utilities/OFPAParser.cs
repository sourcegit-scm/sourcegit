#nullable enable

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace SourceGit.Utilities
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
    /// <summary>
    /// Decodes human-readable names from Unreal Engine OFPA (One File Per Actor) .uasset files.
    /// These files have hashed names like "KCBX0GWLTFQT9RJ8M1LY8.uasset" in __ExternalActors__ folders.
    /// </summary>
    public static class OFPAParser
    {
        // Unreal Engine asset magic number (little-endian: 0x9E2A83C1)
        private static readonly byte[] UnrealMagic = { 0xC1, 0x83, 0x2A, 0x9E };

        private const int HeaderScanLimit = 1024;
        private const int MaxStringLength = 256;
        private const int PropertyTagWindow = 150;
        private const int MinimumHeaderSize = 20;
        private const int PatternLength = 16;

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
            // OFPA files are only .uasset entries.
            if (!path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
                return false;

            return path.Contains("__ExternalActors__", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("__ExternalObjects__", StringComparison.OrdinalIgnoreCase);
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
                if (!File.Exists(filePath))
                    return null;

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
            if (data == null || data.Length < MinimumHeaderSize)
                return null;

            // Check magic number
            if (data[0] != UnrealMagic[0] || data[1] != UnrealMagic[1] ||
                data[2] != UnrealMagic[2] || data[3] != UnrealMagic[3])
                return null;

            return ParseUAsset(data);
        }

        private static DecodeResult? ParseUAsset(byte[] buffer)
        {
            int fileSize = buffer.Length;

            // Fast path: find '/' to locate FolderName string
            int searchStart = MinimumHeaderSize;
            int slashOffset = FindByte(buffer, (byte)'/', MinimumHeaderSize, Math.Min(fileSize, HeaderScanLimit));
            if (slashOffset >= 24)
            {
                int length = ReadInt32(buffer, slashOffset - 4);
                if (length > 0 && length < MaxStringLength)
                    searchStart = slashOffset - 4;
            }

            // Scan for NameMap count and offset
            int headerLength = Math.Min(fileSize, HeaderScanLimit);
            int scanLimit = headerLength - MinimumHeaderSize;
            int nameCount = 0;
            int nameOffset = 0;

            for (int currentPos = searchStart; currentPos < scanLimit; currentPos++)
            {
                int stringLength = ReadInt32(buffer, currentPos);
                if (stringLength > 0 && stringLength < MaxStringLength)
                {
                    int stringEnd = currentPos + 4 + stringLength;
                    if (stringEnd > scanLimit)
                        break;

                    byte firstChar = buffer[currentPos + 4];
                    // Check for '/' or "None"
                    if (firstChar == 47 || (firstChar == 78 && MatchBytes(buffer, currentPos + 4, "None")))
                    {
                        if (stringEnd + 12 <= headerLength)
                        {
                            int count = ReadInt32(buffer, stringEnd + 4);
                            int offset = ReadInt32(buffer, stringEnd + 8);
                            if (count > 0 && count < 100000 && offset > 0 && offset < fileSize)
                            {
                                nameCount = count;
                                nameOffset = offset;
                                break;
                            }
                        }
                    }
                }
            }

            if (nameCount == 0)
                return null;

            // Parse Name Map - find target indices
            int labelIndex = -1;
            int propertyIndex = -1;
            string? labelType = null;

            int position = nameOffset;
            for (int i = 0; i < nameCount && position + 4 <= fileSize; i++)
            {
                int stringLength = ReadInt32(buffer, position);
                position += 4;

                if (stringLength > 0)
                {
                    int end = position + stringLength;
                    if (end > fileSize)
                        break;

                    // Check for target strings
                    if (stringLength == 11 && labelIndex < 0 && MatchBytes(buffer, position, "ActorLabel"))
                    {
                        labelIndex = i;
                        labelType = "ActorLabel";
                    }
                    else if (stringLength == 12)
                    {
                        if (MatchBytes(buffer, position, "StrProperty"))
                        {
                            propertyIndex = i;
                        }
                        else if (labelIndex < 0 && MatchBytes(buffer, position, "FolderLabel"))
                        {
                            labelIndex = i;
                            labelType = "FolderLabel";
                        }
                    }
                    else if (stringLength == 6 && labelIndex < 0 && MatchBytes(buffer, position, "Label"))
                    {
                        labelIndex = i;
                        labelType = "Label";
                    }

                    position = end;

                    if (labelIndex >= 0 && propertyIndex >= 0)
                        break;
                }
                else if (stringLength < 0)
                {
                    // UTF-16 string
                    position += (-stringLength) * 2;
                }

                // Skip hash value if present
                if (position + 4 <= fileSize)
                {
                    int hash = ReadInt32(buffer, position);
                    if (hash == 0 || hash < -512 || hash > 512)
                        position += 4;
                }
            }

            if (labelIndex < 0 || propertyIndex < 0 || labelType == null)
                return null;

            // Find property tag pattern: [labelIndex, 0, propertyIndex, 0]
            byte[] pattern = new byte[PatternLength];
            BinaryPrimitives.WriteInt32LittleEndian(pattern.AsSpan(0), labelIndex);
            BinaryPrimitives.WriteInt32LittleEndian(pattern.AsSpan(4), 0);
            BinaryPrimitives.WriteInt32LittleEndian(pattern.AsSpan(8), propertyIndex);
            BinaryPrimitives.WriteInt32LittleEndian(pattern.AsSpan(12), 0);

            int tagOffset = FindPattern(buffer, pattern);
            if (tagOffset == -1)
                return null;

            // Extract string value
            int valueSearchStart = tagOffset + PatternLength;
            int valueSearchEnd = Math.Min(valueSearchStart + PropertyTagWindow, fileSize);

            for (int i = valueSearchStart; i < valueSearchEnd - 4; i++)
            {
                int stringLength = ReadInt32(buffer, i);

                if (stringLength > 0 && stringLength < 128)
                {
                    int stringEnd = i + 4 + stringLength - 1; // -1 for null terminator
                    if (stringEnd <= valueSearchEnd)
                    {
                        // Check if it's printable ASCII
                        bool valid = true;
                        for (int j = i + 4; j < stringEnd && valid; j++)
                        {
                            byte b = buffer[j];
                            if (b < 32 || b > 126)
                                valid = false;
                        }

                        if (valid && stringEnd > i + 4)
                        {
                            string value = Encoding.ASCII.GetString(buffer, i + 4, stringEnd - i - 4);
                            return new DecodeResult(labelType, value);
                        }
                    }
                }
                else if (stringLength < 0 && stringLength > -128)
                {
                    // UTF-16 string
                    int stringEnd = i + 4 + ((-stringLength) * 2) - 2; // -2 for null terminator
                    if (stringEnd <= valueSearchEnd && stringEnd > i + 4)
                    {
                        try
                        {
                            string value = Encoding.Unicode.GetString(buffer, i + 4, stringEnd - i - 4);
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
