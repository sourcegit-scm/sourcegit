using System.IO;
using Avalonia.Media.Imaging;

namespace SourceGit.ViewModels
{
    public class ImageSource
    {
        public Bitmap Bitmap { get; }
        public long Size { get; }

        public ImageSource(Bitmap bitmap, long size)
        {
            Bitmap = bitmap;
            Size = size;
        }

        public static Models.ImageDecoder GetDecoder(string file)
        {
            var ext = Path.GetExtension(file) ?? ".invalid_img";

            switch (ext)
            {
                case ".ico":
                case ".bmp":
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".webp":
                    return Models.ImageDecoder.Builtin;
                default:
                    return Models.ImageDecoder.None;
            }
        }

        public static ImageSource FromFile(string fullpath, Models.ImageDecoder decoder)
        {
            using (var stream = File.OpenRead(fullpath))
                return LoadFromStream(stream, decoder);
        }

        public static ImageSource FromRevision(string repo, string revision, string file, Models.ImageDecoder decoder)
        {
            var stream = Commands.QueryFileContent.Run(repo, revision, file);
            return LoadFromStream(stream, decoder);
        }

        public static ImageSource FromLFSObject(string repo, Models.LFSObject lfs, Models.ImageDecoder decoder)
        {
            if (string.IsNullOrEmpty(lfs.Oid) || lfs.Size == 0)
                return new ImageSource(null, 0);

            var stream = Commands.QueryFileContent.FromLFS(repo, lfs.Oid, lfs.Size);
            return LoadFromStream(stream, decoder);
        }

        private static ImageSource LoadFromStream(Stream stream, Models.ImageDecoder decoder)
        {
            var size = stream.Length;
            if (size > 0)
            {
                if (decoder == Models.ImageDecoder.Builtin)
                {
                    try
                    {
                        var bitmap = new Bitmap(stream);
                        return new ImageSource(bitmap, size);
                    }
                    catch
                    {
                        // Just ignore.
                    }
                }
            }

            return new ImageSource(null, 0);
        }
    }
}
