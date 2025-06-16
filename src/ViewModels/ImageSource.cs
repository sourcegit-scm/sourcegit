using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using BitMiracle.LibTiff.Classic;
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
                case ".gif":
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".webp":
                    return Models.ImageDecoder.Builtin;
                case ".tga":
                case ".dds":
                    return Models.ImageDecoder.Pfim;
                case ".tif":
                case ".tiff":
                    return Models.ImageDecoder.Tiff;
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
                try
                {
                    switch (decoder)
                    {
                        case Models.ImageDecoder.Builtin:
                            return DecodeWithAvalonia(stream, size);
                        case Models.ImageDecoder.Pfim:
                            return DecodeWithPfim(stream, size);
                        case Models.ImageDecoder.Tiff:
                            return DecodeWithTiff(stream, size);
                    }
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine(e.Message);
                }                
            }

            return new ImageSource(null, 0);
        }

        private static ImageSource DecodeWithAvalonia(Stream stream, long size)
        {
            var bitmap = new Bitmap(stream);
            return new ImageSource(bitmap, size);
        }

        private static ImageSource DecodeWithPfim(Stream stream, long size)
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
                    case ImageFormat.Rgba16:
                        var pixels2 = pfiImage.DataLen / 2;
                        data = new byte[pixels2 * 4];
                        stride = pfiImage.Width * 4;
                        for (var i = 0; i < pixels2; i++)
                        {
                            var src = BitConverter.ToUInt16(pfiImage.Data, i * 2);
                            data[i * 4 + 0] = (byte)Math.Round((src & 0x0F) / 15F * 255); // B
                            data[i * 4 + 1] = (byte)Math.Round(((src >> 4) & 0x0F) / 15F * 255); // G
                            data[i * 4 + 2] = (byte)Math.Round(((src >> 8) & 0x0F) / 15F * 255); // R
                            data[i * 4 + 3] = (byte)Math.Round(((src >> 12) & 0x0F) / 15F * 255); // A
                        }

                        alphaFormat = AlphaFormat.Premul;
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
    
        private static ImageSource DecodeWithTiff(Stream stream, long size)
        {
            using (var tiff = Tiff.ClientOpen($"{Guid.NewGuid()}.tif", "r", stream, new TiffStream()))
            {
                if (tiff == null)
                    return new ImageSource(null, 0);

                var width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
                var height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
                var pixels = new int[width * height];

                // Currently only supports image when its `BITSPERSAMPLE` is one in [1,2,4,8,16] 
                tiff.ReadRGBAImageOriented(width, height, pixels, Orientation.TOPLEFT);

                var ptr = Marshal.UnsafeAddrOfPinnedArrayElement(pixels, 0);
                var pixelSize = new PixelSize(width, height);
                var dpi = new Vector(96, 96);
                var bitmap = new Bitmap(PixelFormats.Rgba8888, AlphaFormat.Premul, ptr, pixelSize, dpi, width * 4);
                return new ImageSource(bitmap, size);
            }
        }
    }
}
