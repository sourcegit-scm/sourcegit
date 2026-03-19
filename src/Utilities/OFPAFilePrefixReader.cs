using System;
using System.IO;

namespace SourceGit.Utilities
{
    internal static class OFPAFilePrefixReader
    {
        public static byte[] Read(string filePath, int maxBytes)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || maxBytes <= 0 || !File.Exists(filePath))
                    return null;

                using var stream = File.OpenRead(filePath);
                var length = (int)Math.Min(stream.Length, maxBytes);
                if (length <= 0)
                    return [];

                var buffer = new byte[length];
                var offset = 0;
                while (offset < length)
                {
                    var read = stream.Read(buffer, offset, length - offset);
                    if (read <= 0)
                        break;

                    offset += read;
                }

                if (offset == length)
                    return buffer;

                if (offset == 0)
                    return [];

                var resized = new byte[offset];
                Array.Copy(buffer, resized, offset);
                return resized;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
