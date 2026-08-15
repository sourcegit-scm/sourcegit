using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using StbImageSharp;

namespace SourceGit.Models
{
    public static class ImageDifferenceDetector
    {
        public static List<Rect> DetectChangeBoxes(Bitmap oldBmp, Bitmap newBmp)
        {
            if (oldBmp == null && newBmp == null)
                return [];

            if (oldBmp == null)
                return [new Rect(0, 0, newBmp.PixelSize.Width, newBmp.PixelSize.Height)];

            if (newBmp == null)
                return [new Rect(0, 0, oldBmp.PixelSize.Width, oldBmp.PixelSize.Height)];

            var oldBytes = GetRgbaBytes(oldBmp, out var w1, out var h1);
            var newBytes = GetRgbaBytes(newBmp, out var w2, out var h2);

            if (oldBytes == null && newBytes == null)
                return [];
            if (oldBytes == null)
                return [new Rect(0, 0, w2, h2)];
            if (newBytes == null)
                return [new Rect(0, 0, w1, h1)];

            int maxW = Math.Max(w1, w2);
            int maxH = Math.Max(h1, h2);

            if (maxW <= 0 || maxH <= 0)
                return [];

            const int cellSize = 16;
            int gridW = (maxW + cellSize - 1) / cellSize;
            int gridH = (maxH + cellSize - 1) / cellSize;

            var cellBounds = new Rect[gridW, gridH];
            var hasDiff = new bool[gridW, gridH];
            bool anyDiff = false;

            for (int cy = 0; cy < gridH; cy++)
            {
                int yStart = cy * cellSize;
                int yEnd = Math.Min(yStart + cellSize, maxH);

                for (int cx = 0; cx < gridW; cx++)
                {
                    int xStart = cx * cellSize;
                    int xEnd = Math.Min(xStart + cellSize, maxW);

                    int minX = int.MaxValue, minY = int.MaxValue;
                    int maxX = int.MinValue, maxY = int.MinValue;
                    bool cellHasDiff = false;

                    for (int y = yStart; y < yEnd; y++)
                    {
                        for (int x = xStart; x < xEnd; x++)
                        {
                            bool diffPixel = false;
                            if (x >= w1 || y >= h1)
                            {
                                int idx2 = (y * w2 + x) * 4;
                                if (idx2 + 3 < newBytes.Length && newBytes[idx2 + 3] > 0)
                                    diffPixel = true;
                            }
                            else if (x >= w2 || y >= h2)
                            {
                                int idx1 = (y * w1 + x) * 4;
                                if (idx1 + 3 < oldBytes.Length && oldBytes[idx1 + 3] > 0)
                                    diffPixel = true;
                            }
                            else
                            {
                                int idx1 = (y * w1 + x) * 4;
                                int idx2 = (y * w2 + x) * 4;

                                if (idx1 + 3 < oldBytes.Length && idx2 + 3 < newBytes.Length)
                                {
                                    byte r1 = oldBytes[idx1];
                                    byte g1 = oldBytes[idx1 + 1];
                                    byte b1 = oldBytes[idx1 + 2];
                                    byte a1 = oldBytes[idx1 + 3];

                                    byte r2 = newBytes[idx2];
                                    byte g2 = newBytes[idx2 + 1];
                                    byte b2 = newBytes[idx2 + 2];
                                    byte a2 = newBytes[idx2 + 3];

                                    if (a1 != 0 || a2 != 0)
                                    {
                                        if (Math.Abs(r1 - r2) > 2 ||
                                            Math.Abs(g1 - g2) > 2 ||
                                            Math.Abs(b1 - b2) > 2 ||
                                            Math.Abs(a1 - a2) > 2)
                                        {
                                            diffPixel = true;
                                        }
                                    }
                                }
                            }

                            if (diffPixel)
                            {
                                cellHasDiff = true;
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;
                            }
                        }
                    }

                    if (cellHasDiff)
                    {
                        hasDiff[cx, cy] = true;
                        cellBounds[cx, cy] = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                        anyDiff = true;
                    }
                }
            }

            if (!anyDiff)
                return [];

            var visited = new bool[gridW, gridH];
            var clusters = new List<Rect>();

            for (int cy = 0; cy < gridH; cy++)
            {
                for (int cx = 0; cx < gridW; cx++)
                {
                    if (hasDiff[cx, cy] && !visited[cx, cy])
                    {
                        var queue = new Queue<(int x, int y)>();
                        queue.Enqueue((cx, cy));
                        visited[cx, cy] = true;

                        double minBoxX = cellBounds[cx, cy].X;
                        double minBoxY = cellBounds[cx, cy].Y;
                        double maxBoxX = cellBounds[cx, cy].Right;
                        double maxBoxY = cellBounds[cx, cy].Bottom;

                        while (queue.Count > 0)
                        {
                            var (currX, currY) = queue.Dequeue();
                            var curR = cellBounds[currX, currY];
                            if (curR.X < minBoxX) minBoxX = curR.X;
                            if (curR.Y < minBoxY) minBoxY = curR.Y;
                            if (curR.Right > maxBoxX) maxBoxX = curR.Right;
                            if (curR.Bottom > maxBoxY) maxBoxY = curR.Bottom;

                            for (int ny = Math.Max(0, currY - 2); ny <= Math.Min(gridH - 1, currY + 2); ny++)
                            {
                                for (int nx = Math.Max(0, currX - 2); nx <= Math.Min(gridW - 1, currX + 2); nx++)
                                {
                                    if (hasDiff[nx, ny] && !visited[nx, ny])
                                    {
                                        visited[nx, ny] = true;
                                        queue.Enqueue((nx, ny));
                                    }
                                }
                            }
                        }

                        const double padding = 4;
                        double bx = Math.Max(0, minBoxX - padding);
                        double by = Math.Max(0, minBoxY - padding);
                        double bw = Math.Min(maxW - bx, (maxBoxX - minBoxX) + padding * 2);
                        double bh = Math.Min(maxH - by, (maxBoxY - minBoxY) + padding * 2);

                        const double minSize = 14;
                        if (bw < minSize)
                        {
                            double diff = minSize - bw;
                            bx = Math.Max(0, bx - diff / 2);
                            bw = Math.Min(maxW - bx, minSize);
                        }
                        if (bh < minSize)
                        {
                            double diff = minSize - bh;
                            by = Math.Max(0, by - diff / 2);
                            bh = Math.Min(maxH - by, minSize);
                        }

                        clusters.Add(new Rect(bx, by, bw, bh));
                    }
                }
            }

            return clusters;
        }

        private static byte[] GetRgbaBytes(Bitmap bitmap, out int width, out int height)
        {
            width = bitmap.PixelSize.Width;
            height = bitmap.PixelSize.Height;
            if (width <= 0 || height <= 0)
                return null;

            try
            {
                using var ms = new MemoryStream();
                bitmap.Save(ms);
                ms.Position = 0;
                var result = ImageResult.FromStream(ms, ColorComponents.RedGreenBlueAlpha);
                if (result != null && result.Data != null)
                {
                    width = result.Width;
                    height = result.Height;
                    return result.Data;
                }
            }
            catch
            {
                // In case saving fails
            }

            return null;
        }
    }
}
