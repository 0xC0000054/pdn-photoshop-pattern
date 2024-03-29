﻿/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop Pattern FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2020, 2023 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace PatternFileTypePlugin
{
    internal static class PatternSave
    {
        public static unsafe void Save(Document input, Stream output, PropertyBasedSaveConfigToken token, ProgressEventHandler progressCallback)
        {
            if (input.Width > ushort.MaxValue || input.Height > ushort.MaxValue)
            {
                throw new FormatException($"The document dimensions must be 65535x65535 or less.");
            }

            bool rle = token.GetProperty<PaintDotNet.PropertySystem.BooleanProperty>(PropertyNames.RLE).Value;

            using (BigEndianBinaryWriter writer = new(output, true))
            {
                List<(int index, Rectangle saveBounds)> nonEmptyLayers = GetNonEmptyLayers(input);

                writer.Write(PatternConstants.FileSignature);
                writer.Write(PatternConstants.FileVersion);
                writer.Write(nonEmptyLayers.Count);

                double progressPercentage = 0.0;
                double progressDelta = (1.0 / nonEmptyLayers.Count) * 100.0;

                LayerList layers = input.Layers;

                foreach ((int index, Rectangle visibleBounds) in nonEmptyLayers)
                {
                    BitmapLayer layer = (BitmapLayer)layers[index];

                    SaveLayer(writer, layer, visibleBounds, rle);

                    progressPercentage += progressDelta;
                    progressCallback?.Invoke(null, new ProgressEventArgs(progressPercentage));
                }
            }
        }

        private static List<(int index, Rectangle saveBounds)> GetNonEmptyLayers(Document input)
        {
            LayerList layers = input.Layers;

            // Assume that the document does not contain any empty layers.
            List<(int, Rectangle)> nonEmptyLayers = new(layers.Count);

            for (int i = 0; i < layers.Count; i++)
            {
                BitmapLayer layer = (BitmapLayer)layers[i];

                Rectangle saveBounds = GetVisibleBounds(layer.Surface);

                if (!saveBounds.IsEmpty)
                {
                    nonEmptyLayers.Add((i, saveBounds));
                }
            }

            return nonEmptyLayers;

            static unsafe Rectangle GetVisibleBounds(Surface surface)
            {
                int left = surface.Width;
                int top = surface.Height;
                int right = 0;
                int bottom = 0;

                for (int y = 0; y < surface.Height; y++)
                {
                    ColorBgra* p = surface.GetRowPointerUnchecked(y);

                    for (int x = 0; x < surface.Width; x++)
                    {
                        if (p->A > 0)
                        {
                            if (x < left)
                            {
                                left = x;
                            }
                            if (x > right)
                            {
                                right = x;
                            }
                            if (y < top)
                            {
                                top = y;
                            }
                            if (y > bottom)
                            {
                                bottom = y;
                            }
                        }
                        p++;
                    }
                }

                if (left < surface.Width && top < surface.Height)
                {
                    return new Rectangle(left, top, right + 1, bottom + 1);
                }
                else
                {
                    return Rectangle.Empty;
                }
            }
        }

        private static void SaveLayer(BigEndianBinaryWriter writer, BitmapLayer layer, Rectangle visibleBounds, bool rle)
        {
            Surface surface = layer.Surface;

            Analyze(surface, visibleBounds, out bool hasAlpha, out bool grayScale);

            // Write the pattern header
            writer.Write(PatternConstants.RecordVersion);

            if (grayScale)
            {
                writer.Write((uint)ImageType.Grayscale);
            }
            else
            {
                writer.Write((uint)ImageType.RGB);
            }

            writer.Write((ushort)visibleBounds.Height);
            writer.Write((ushort)visibleBounds.Width);

            writer.WriteUnicodeString(layer.Name);

            string tag = Guid.NewGuid().ToString();
            writer.WritePascalString(tag);

            writer.Write(PatternConstants.RecordSubVersion);

            using (new LengthWriter(writer))
            {
                writer.WriteInt32Rectangle(visibleBounds);
                // Write the unknown field, always 24.
                writer.Write((uint)24);

                if (grayScale)
                {
                    WriteGrayscalePattern(writer, surface, visibleBounds, hasAlpha, rle);
                }
                else
                {
                    WriteRGBPattern(writer, surface, visibleBounds, hasAlpha, rle);
                }
            }
        }

        private static unsafe void Analyze(Surface surface, Rectangle bounds, out bool hasAlpha, out bool grayScale)
        {
            hasAlpha = false;
            grayScale = true;

            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                ColorBgra* p = surface.GetPointPointerUnchecked(bounds.Left, y);
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    if (p->A < 255)
                    {
                        hasAlpha = true;
                    }
                    if (!(p->R == p->G && p->G == p->B))
                    {
                        grayScale = false;
                    }

                    p++;
                }
            }
        }

        private static unsafe void ExtractChannelData(Surface source, Span<byte> destination, int bgraChannelIndex)
        {
            fixed (byte* ptr = destination)
            {
                int width = source.Width;
                int height = source.Height;

                RegionPtr<byte> target = new(ptr, width, height, width);
                RegionPtr<ColorBgra32> sourceRegion = new(source,
                                                          (ColorBgra32*)source.Scan0.VoidStar,
                                                          width,
                                                          height,
                                                          source.Stride);

                PixelKernels.ExtractChannel(target, sourceRegion, bgraChannelIndex);
            }
        }

        private static unsafe void WriteRGBPattern(BigEndianBinaryWriter writer, Surface surface, Rectangle visibleBounds, bool hasAlpha, bool rle)
        {
            PatternImageCompression compression = rle ? PatternImageCompression.RLE : PatternImageCompression.Raw;

            using (PatternChannel red = new(visibleBounds, compression))
            {
                ExtractChannelData(surface, red.GetChannelData(), 2);
                red.Write(writer);
            }

            using (PatternChannel green = new(visibleBounds, compression))
            {
                ExtractChannelData(surface, green.GetChannelData(), 1);
                green.Write(writer);
            }

            using (PatternChannel blue = new(visibleBounds, compression))
            {
                ExtractChannelData(surface, blue.GetChannelData(), 0);
                blue.Write(writer);
            }

            if (hasAlpha)
            {
                // Write the padding that comes before the channel header.
                writer.Write(new byte[PatternConstants.AlphaChannelPadding.RGBModeSize]);

                using (PatternChannel alpha = new(visibleBounds, compression))
                {
                    ExtractChannelData(surface, alpha.GetChannelData(), 3);
                    alpha.Write(writer);
                }
            }
        }

        private static unsafe void WriteGrayscalePattern(BigEndianBinaryWriter writer, Surface surface, Rectangle visibleBounds, bool hasAlpha, bool rle)
        {
            PatternImageCompression compression = rle ? PatternImageCompression.RLE : PatternImageCompression.Raw;

            using (PatternChannel gray = new(visibleBounds, compression))
            {
                ExtractChannelData(surface, gray.GetChannelData(), 0);
                gray.Write(writer);
            }

            if (hasAlpha)
            {
                // Write the padding that comes before the channel header.
                writer.Write(new byte[PatternConstants.AlphaChannelPadding.GrayscaleModeSize]);

                using (PatternChannel alpha = new(visibleBounds, compression))
                {
                    ExtractChannelData(surface, alpha.GetChannelData(), 3);
                    alpha.Write(writer);
                }
            }
        }
    }
}
