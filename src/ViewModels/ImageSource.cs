using System;
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
            using (var pfiImage = Pfimage.FromStream(stream))
            {
                try
                {
                    var data = pfiImage.Data;
                    var stride = pfiImage.Stride;

                    var pixelFormat = PixelFormats.Rgba8888;
                    var alphaFormat = AlphaFormat.Opaque;
                    switch (pfiImage.Format)
                    {
                        case ImageFormat.Rgb8:
                            pixelFormat = PixelFormats.Gray8;
                            break;
                        case ImageFormat.R5g6b5:
                            pixelFormat = PixelFormats.Rgb565;
                            break;
                        case ImageFormat.Rgba16:
                            var pixels = pfiImage.DataLen / 2;
                            var newSize = pfiImage.DataLen * 2;
                            data = new byte[newSize];
                            stride = 4 * pfiImage.Width;
                            for (int i = 0; i < pixels; i++)
                            {
                                var rg = pfiImage.Data[i * 2];
                                var ba = pfiImage.Data[i * 2 + 1];
                                data[i * 4 + 0] = (byte)Math.Round((rg >> 4) / 15.0 * 255);
                                data[i * 4 + 1] = (byte)Math.Round((rg & 0xF) / 15.0 * 255);
                                data[i * 4 + 2] = (byte)Math.Round((ba >> 4) / 15.0 * 255);
                                data[i * 4 + 3] = (byte)Math.Round((ba & 0xF) / 15.0 * 255);
                            }
                            alphaFormat = AlphaFormat.Premul;
                            break;
                        case ImageFormat.R5g5b5a1:
                            var pixels2 = pfiImage.DataLen / 2;
                            var newSize2 = pfiImage.DataLen * 2;
                            data = new byte[newSize2];
                            stride = 4 * pfiImage.Width;
                            for (int i = 0; i < pixels2; i++)
                            {
                                var v = (int)pfiImage.Data[i * 2] << 8 + pfiImage.Data[i * 2 + 1];
                                data[i * 4 + 0] = (byte)Math.Round(((v & 0b1111100000000000) >> 11) / 31.0 * 255);
                                data[i * 4 + 1] = (byte)Math.Round(((v & 0b11111000000) >> 6) / 31.0 * 255);
                                data[i * 4 + 2] = (byte)Math.Round(((v & 0b111110) >> 1) / 31.0 * 255);
                                data[i * 4 + 3] = (byte)((v & 1) == 1 ? 255 : 0); 
                            }
                            alphaFormat = AlphaFormat.Premul;
                            break;
                        case ImageFormat.Rgb24:
                            pixelFormat = PixelFormats.Rgb24;
                            break;
                        case ImageFormat.Rgba32:
                            pixelFormat = PixelFormat.Rgba8888;
                            alphaFormat = AlphaFormat.Premul;
                            break;
                        default:
                            return new ImageSource(null, 0);
                    }

                    var ptr = Marshal.UnsafeAddrOfPinnedArrayElement(pfiImage.Data, 0);
                    var pixelSize = new PixelSize(pfiImage.Width, pfiImage.Height);
                    var dpi = new Vector(96, 96);
                    var bitmap = new Bitmap(pixelFormat, alphaFormat, ptr, pixelSize, dpi, stride);
                    return new ImageSource(bitmap, size);
                }
                catch
                {
                    return new ImageSource(null, 0);
                }
            }
        }
    }
}
