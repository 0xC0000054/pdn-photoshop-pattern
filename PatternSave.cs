/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop Pattern FileType Plugin for Paint.NET
// 
// This software is provided under the MIT License:
//   Copyright (c) 2012-2017 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using System;
using System.Drawing;
using System.IO;

namespace PatternFileTypePlugin
{
    internal static class PatternSave
    {
        public static unsafe void Save(Document input, Stream output, PropertyBasedSaveConfigToken token, ProgressEventHandler progressCallback)
        {
            bool rle = token.GetProperty<PaintDotNet.PropertySystem.BooleanProperty>(PropertyNames.RLE).Value;

            using (BinaryReverseWriter writer = new BinaryReverseWriter(output, true))
            {
                writer.Write(PatternConstants.FileSignature);
                writer.Write(PatternConstants.FileVersion);
                writer.Write(input.Layers.Count);

                double progressPercentage = 0.0;
                double progressDelta = (1.0 / input.Layers.Count) * 100.0;

                foreach (Layer item in input.Layers)
                {
                    BitmapLayer layer = (BitmapLayer)item;

                    SaveLayer(writer, layer, rle);

                    progressPercentage += progressDelta;
                    progressCallback(null, new ProgressEventArgs(progressPercentage));
                }
            }
        }

        private static void SaveLayer(BinaryReverseWriter writer, BitmapLayer layer, bool rle)
        {
            Surface surface = layer.Surface;
            Rectangle visibleBounds = GetVisibleBounds(surface);

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

        private static unsafe Rectangle GetVisibleBounds(Surface surface)
        {
            int left = surface.Width;
            int top = surface.Height;
            int right = 0;
            int bottom = 0;

            for (int y = 0; y < surface.Height; y++)
            {
                ColorBgra* p = surface.GetRowAddress(y);

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

        private static unsafe void Analyze(Surface surface, Rectangle bounds, out bool hasAlpha, out bool grayScale)
        {
            hasAlpha = false;
            grayScale = true;

            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                ColorBgra* p = surface.GetPointAddressUnchecked(bounds.Left, y);
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

        private static unsafe void WriteRGBPattern(BinaryReverseWriter writer, Surface surface, Rectangle visibleBounds, bool hasAlpha, bool rle)
        {
            int size = visibleBounds.Width * visibleBounds.Height;

            byte[] red = new byte[size];
            byte[] green = new byte[size];
            byte[] blue = new byte[size];
            byte[] alpha = hasAlpha ? new byte[size] : null;

            int index = 0;
            for (int y = visibleBounds.Top; y < visibleBounds.Bottom; y++)
            {
                ColorBgra* p = surface.GetPointAddressUnchecked(visibleBounds.Left, y);

                for (int x = visibleBounds.Left; x < visibleBounds.Right; x++)
                {
                    red[index] = p->R;
                    green[index] = p->G;
                    blue[index] = p->B;

                    if (hasAlpha)
                    {
                        alpha[index] = p->A;
                    }

                    p++;
                    index++;
                }
            }

            PatternImageCompression compression = rle ? PatternImageCompression.RLE : PatternImageCompression.Raw;

            WriteChannelData(writer, visibleBounds, compression, red);
            WriteChannelData(writer, visibleBounds, compression, green);
            WriteChannelData(writer, visibleBounds, compression, blue);

            if (hasAlpha)
            {
                // Write the padding that comes before the channel header.
                writer.Write(new byte[PatternConstants.AlphaChannelPadding.RGBModeSize]);

                WriteChannelData(writer, visibleBounds, compression, alpha);
            }
        }

        private static unsafe void WriteGrayscalePattern(BinaryReverseWriter writer, Surface surface, Rectangle visibleBounds, bool hasAlpha, bool rle)
        {
            int size = visibleBounds.Width * visibleBounds.Height;

            byte[] gray = new byte[size];
            byte[] alpha = hasAlpha ? new byte[size] : null;

            int index = 0;
            for (int y = visibleBounds.Top; y < visibleBounds.Bottom; y++)
            {
                ColorBgra* p = surface.GetPointAddressUnchecked(visibleBounds.Left, y);

                for (int x = visibleBounds.Left; x < visibleBounds.Right; x++)
                {
                    gray[index] = p->R;

                    if (hasAlpha)
                    {
                        alpha[index] = p->A;
                    }

                    p++;
                    index++;
                }
            }

            PatternImageCompression compression = rle ? PatternImageCompression.RLE : PatternImageCompression.Raw;

            WriteChannelData(writer, visibleBounds, compression, gray);

            if (hasAlpha)
            {
                // Write the padding that comes before the channel header.
                writer.Write(new byte[PatternConstants.AlphaChannelPadding.GrayscaleModeSize]);

                WriteChannelData(writer, visibleBounds, compression, alpha);
            }
        }

        private static unsafe void WriteChannelData(BinaryReverseWriter writer, Rectangle visibleBounds, PatternImageCompression compression, byte[] channelData)
        {
            const ushort ChannelDepth = 8;

            PatternChannel channel = new PatternChannel(ChannelDepth, visibleBounds, compression, channelData);
            channel.WriteChannelData(writer);
        }
    }
}
