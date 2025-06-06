using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Pfim;

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
                case ".tga":
                case ".dds":
                    return Models.ImageDecoder.Pfim;
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
                else if (decoder == Models.ImageDecoder.Pfim)
                {
                    return new ImageSource(LoadWithPfim(stream), size);
                }
            }

            return new ImageSource(null, 0);
        }

        private static Bitmap LoadWithPfim(Stream stream)
        {
            var image = Pfim.Pfimage.FromStream(stream);
            byte[] data;
            int stride;
            if (image.Format == ImageFormat.Rgba32)
            {
                data = image.Data;
                stride = image.Stride;
            }
            else
            {
                int pixels = image.Width * image.Height;
                data = new byte[pixels * 4];
                stride = image.Width * 4;

                switch (image.Format)
                {
                    case ImageFormat.Rgba16:
                    case ImageFormat.R5g5b5a1:
                        {
                            for (int i = 0; i < pixels; i++)
                            {
                                data[i * 4 + 0] = image.Data[i * 4 + 2]; // B
                                data[i * 4 + 1] = image.Data[i * 4 + 1]; // G
                                data[i * 4 + 2] = image.Data[i * 4 + 0]; // R
                                data[i * 4 + 3] = image.Data[i * 4 + 3]; // A
                            }
                        }
                        break;
                    case ImageFormat.R5g5b5:
                    case ImageFormat.R5g6b5:
                    case ImageFormat.Rgb24:
                        {
                            for (int i = 0; i < pixels; i++)
                            {
                                data[i * 4 + 0] = image.Data[i * 3 + 2]; // B
                                data[i * 4 + 1] = image.Data[i * 3 + 1]; // G
                                data[i * 4 + 2] = image.Data[i * 3 + 0]; // R
                                data[i * 4 + 3] = 255;                   // A
                            }
                        }
                        break;
                    case ImageFormat.Rgb8:
                        {
                            for (int i = 0; i < pixels; i++)
                            {
                                var color = image.Data[i];
                                data[i * 4 + 0] = color;
                                data[i * 4 + 1] = color;
                                data[i * 4 + 2] = color;
                                data[i * 4 + 3] = 255;
                            }
                        }
                        break;
                    default:
                        return null;
                }
            }

            // Pin the array and pass the pointer to Bitmap
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                var ptr = Marshal.UnsafeAddrOfPinnedArrayElement(data, 0);
                var bitmap = new Bitmap(
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    Avalonia.Platform.AlphaFormat.Unpremul,
                    ptr,
                    new Avalonia.PixelSize(image.Width, image.Height),
                    new Avalonia.Vector(96, 96),
                    stride);
                return bitmap;
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
