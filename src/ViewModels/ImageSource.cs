using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

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
            var ext = (Path.GetExtension(file) ?? ".invalid_img").ToLower(CultureInfo.CurrentCulture);

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
                    return DecodeWithAvalonia(stream, size);
                else if (decoder == Models.ImageDecoder.Pfim)
                    return DecodeWithPfim(stream, size);
            }

            return new ImageSource(null, 0);
        }

        private static ImageSource DecodeWithAvalonia(Stream stream, long size)
        {
            try
            {
                var bitmap = new Bitmap(stream);
                return new ImageSource(bitmap, size);
            }
            catch
            {
                return new ImageSource(null, 0);
            }
        }

        private static ImageSource DecodeWithPfim(Stream stream, long size)
        {
            try
            {
                using (var pfiImage = Pfimage.FromStream(stream))
                {
                    var data = pfiImage.Data;
                    var stride = pfiImage.Stride;

                    var pixelFormat = PixelFormats.Bgra8888;
                    var alphaFormat = AlphaFormat.Opaque;
                    switch (pfiImage.Format)
                    {
                        case ImageFormat.Rgb8:
                            pixelFormat = PixelFormats.Gray8;
                            break;
                        case ImageFormat.R5g5b5:
                        case ImageFormat.R5g5b5a1:
                            pixelFormat = PixelFormats.Bgr555;
                            break;
                        case ImageFormat.R5g6b5:
                            pixelFormat = PixelFormats.Bgr565;
                            break;
                        case ImageFormat.Rgb24:
                            pixelFormat = PixelFormats.Bgr24;
                            break;
                        case ImageFormat.Rgba32:
                            alphaFormat = AlphaFormat.Premul;
                            break;
                        default:
                            return new ImageSource(null, 0);
                    }

                    var ptr = Marshal.UnsafeAddrOfPinnedArrayElement(data, 0);
                    var pixelSize = new PixelSize(pfiImage.Width, pfiImage.Height);
                    var dpi = new Vector(96, 96);
                    var bitmap = new Bitmap(pixelFormat, alphaFormat, ptr, pixelSize, dpi, stride);
                    return new ImageSource(bitmap, size);
                }
            }
            catch (Exception e)
            {
                return new ImageSource(null, 0);
            }
        }
    }
}
