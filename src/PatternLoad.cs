/////////////////////////////////////////////////////////////////////////////////
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
using PatternFileTypePlugin.Properties;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace PatternFileTypePlugin
{
    internal static class PatternLoad
    {
        public static Document Load(Stream stream)
        {
            using (BigEndianBinaryReader reader = new(stream))
            {
                uint sig = reader.ReadUInt32();
                if (sig != PatternConstants.FileSignature)
                {
                    throw new FormatException(Resources.InvalidPatternFile);
                }

                ushort version = reader.ReadUInt16();
                if (version != PatternConstants.FileVersion)
                {
                    throw new FormatException(string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedPatternFileVersion, version));
                }

                uint count = reader.ReadUInt32();

                List<PatternData> patterns = LoadPatterns(reader, count);

                if (patterns.Count == 0)
                {
                    throw new FormatException(Resources.EmptyPatternFile);
                }

                int maxWidth = 0;
                int maxHeight = 0;
                foreach (PatternData item in patterns)
                {
                    if (item.pattern.Width > maxWidth)
                    {
                        maxWidth = item.pattern.Width;
                    }

                    if (item.pattern.Height > maxHeight)
                    {
                        maxHeight = item.pattern.Height;
                    }
                }

                Document doc = null;
                Document tempDoc = null;

                try
                {
                    tempDoc = new Document(maxWidth, maxHeight);

                    for (int i = 0; i < patterns.Count; i++)
                    {
                        PatternData data = patterns[i];

                        string name = !string.IsNullOrEmpty(data.name) ? data.name : string.Format(CultureInfo.CurrentCulture, Resources.PatternFormat, i + 1);

                        BitmapLayer layer = null;
                        BitmapLayer tempLayer = null;

                        try
                        {
                            tempLayer = new BitmapLayer(maxWidth, maxHeight);
                            tempLayer.IsBackground = i == 0;
                            tempLayer.Name = name;
                            tempLayer.Surface.CopySurface(data.pattern);

                            layer = tempLayer;
                            tempLayer = null;
                        }
                        finally
                        {
                            tempLayer?.Dispose();
                        }

                        tempDoc.Layers.Add(layer);

                        data.pattern.Dispose();
                        data.pattern = null;
                    }

                    doc = tempDoc;
                    tempDoc = null;
                }
                finally
                {
                    tempDoc?.Dispose();
                }

                return doc;
            }
        }

        private static unsafe List<PatternData> LoadPatterns(BigEndianBinaryReader reader, uint count)
        {
            List<PatternData> patterns = new((int)count);

            for (int i = 0; i < count; i++)
            {
                uint version = reader.ReadUInt32();
                if (version != PatternConstants.RecordVersion)
                {
                    throw new FormatException(string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedPatternRecordVersion, version));
                }

                ImageType imageMode = (ImageType)reader.ReadUInt32();
                ushort sHeight = reader.ReadUInt16();
                ushort sWidth = reader.ReadUInt16();

                string name = reader.ReadUnicodeString();

                string tag = reader.ReadPascalString();

                byte[] indexedColorTable = null;

                if (imageMode == ImageType.Indexed)
                {
                    indexedColorTable = reader.ReadBytes(768);

                    // Skip the 2 unknown UInt16 values, colors used and colors important?
                    reader.Position += 4L;
                }

                uint subVersion = reader.ReadUInt32();
                if (subVersion != PatternConstants.RecordSubVersion)
                {
                    throw new FormatException(string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedPatternRecordSubVersion, version, subVersion));
                }

                uint patternSize = reader.ReadUInt32();

                if (imageMode != ImageType.Grayscale && imageMode != ImageType.Indexed && imageMode != ImageType.RGB)
                {
                    // Skip the unsupported image mode.
                    reader.Position += patternSize;
                }

#if DEBUG
                System.Diagnostics.Debug.WriteLine(string.Format(
                    CultureInfo.CurrentCulture,
                    "pattern {0}({1}): {2}x{3} mode: {4}",
                    new object[] { i, name, sWidth, sHeight, imageMode }));
#endif

                if (patternSize > 0)
                {
                    long nextPatternOffset = reader.Position + patternSize;

                    Rectangle bounds = reader.ReadInt32Rectangle();
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        // Ignore any patterns with invalid dimensions.
                        reader.Position += (nextPatternOffset - reader.Position);
                        continue;
                    }

                    uint unknown = reader.ReadUInt32();

                    Surface surface = new(bounds.Width, bounds.Height);

                    if (imageMode == ImageType.RGB)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            PatternChannel channel = new(reader);
                            SetRgbImagePlane(surface, channel, j);
                        }
                    }
                    else if (imageMode == ImageType.Grayscale)
                    {
                        PatternChannel channel = new(reader);
                        SetGrayscaleImageData(surface, channel);
                    }
                    else if (imageMode == ImageType.Indexed)
                    {
                        PatternChannel channel = new(reader);
                        SetIndexedImageData(surface, channel, indexedColorTable);
                    }
                    else
                    {
                        throw new FormatException(string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedImageTypeFormat, imageMode));
                    }

                    PatternChannel alphaChannel = GetAlphaChannel(reader, imageMode, nextPatternOffset);
                    if (alphaChannel != null)
                    {
                        SetAlphaChannel(surface, alphaChannel);
                    }
                    else
                    {
                        RegionPtr<ColorBgra32> region = new(surface,
                                                            (ColorBgra32*)surface.Scan0.VoidStar,
                                                            surface.Width,
                                                            surface.Height,
                                                            surface.Stride);

                        PixelKernels.SetAlphaChannel(region, ColorAlpha8.Opaque);
                    }

                    long remainder = nextPatternOffset - reader.Position;

                    if (remainder > 0)
                    {
                        reader.Position += remainder;
                    }

                    patterns.Add(new PatternData(name, surface));

#if DEBUG
                    using (Bitmap bmp = patterns[i].pattern.CreateAliasedBitmap())
                    {
                    }
#endif
                }
            }

            return patterns;
        }

        private static PatternChannel GetAlphaChannel(BigEndianBinaryReader reader, ImageType imageType, long nextPatternOffset)
        {
            long paddingSize = -1;
            switch (imageType)
            {
                case ImageType.Grayscale:
                    paddingSize = PatternConstants.AlphaChannelPadding.GrayscaleModeSize;
                    break;
                case ImageType.RGB:
                    paddingSize = PatternConstants.AlphaChannelPadding.RGBModeSize;
                    break;
            }

            PatternChannel alphaChannel = null;

            if (paddingSize >= 0)
            {
                long dataStartOffset = reader.Position + paddingSize + PatternConstants.ChannelHeaderSize;

                if (dataStartOffset < nextPatternOffset)
                {
                    long offset = reader.Position;

                    // Skip the padding bytes.
                    reader.Position += paddingSize;

                    PatternChannel channel = new(reader);

                    if (channel.Enabled && channel.Size > 0)
                    {
                        alphaChannel = channel;
                    }
                    else
                    {
                        reader.Position = offset;
                    }
                }
            }

            return alphaChannel;
        }

        private static unsafe void SetAlphaChannel(Surface surface, PatternChannel channel)
        {
            byte[] pixels = channel.GetChannelData();

            fixed (byte* ptr = pixels)
            {
                if (channel.Depth == 16)
                {
                    int width = surface.Width;
                    int height = surface.Height;
                    int srcStride = width * 2;

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = ptr + (y * srcStride);
                        ColorBgra* dst = surface.GetRowPointerUnchecked(y);

                        for (int x = 0; x < width; x++)
                        {
                            dst->A = SixteenBitConversion.GetEightBitValue(src);

                            src += 2;
                            dst++;
                        }
                    }
                }
                else
                {
                    RegionPtr<byte> source = new(pixels, ptr, surface.Width, surface.Height, surface.Width);
                    RegionPtr<ColorBgra32> target = new(surface,
                                                        (ColorBgra32*)surface.Scan0.VoidStar,
                                                        surface.Width,
                                                        surface.Height,
                                                        surface.Stride);

                    PixelKernels.ReplaceChannel(target, source, 3);
                }
            }
        }

        private static unsafe void SetGrayscaleImageData(Surface surface, PatternChannel channel)
        {
            byte[] pixels = channel.GetChannelData();

            fixed (byte* ptr = pixels)
            {
                int width = surface.Width;
                int height = surface.Height;

                if (channel.Depth == 16)
                {
                    int srcStride = width * 2;

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = ptr + (y * srcStride);
                        ColorBgra* dst = surface.GetRowPointerUnchecked(y);

                        for (int x = 0; x < width; x++)
                        {
                            dst->R = dst->G = dst->B = SixteenBitConversion.GetEightBitValue(src);

                            src += 2;
                            dst++;
                        }
                    }
                }
                else
                {
                    int srcStride = width;

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = ptr + (y * srcStride);
                        ColorBgra* dst = surface.GetRowPointerUnchecked(y);

                        for (int x = 0; x < width; x++)
                        {
                            dst->R = dst->G = dst->B = *src;

                            src++;
                            dst++;
                        }
                    }
                }
            }
        }

        private static unsafe void SetIndexedImageData(Surface surface, PatternChannel channel, byte[] indexedColorTable)
        {
            byte[] pixels = channel.GetChannelData();

            fixed (byte* ptr = pixels)
            {
                int width = surface.Width;
                int height = surface.Height;
                int srcStride = width;

                for (int y = 0; y < height; y++)
                {
                    byte* src = ptr + (y * srcStride);
                    ColorBgra* dst = surface.GetRowPointerUnchecked(y);

                    for (int x = 0; x < width; x++)
                    {
                        int index = src[0] * 3;
                        dst->R = indexedColorTable[index];
                        dst->G = indexedColorTable[index + 1];
                        dst->B = indexedColorTable[index + 2];

                        src++;
                        dst++;
                    }
                }
            }
        }

        private static unsafe void SetRgbImagePlane(Surface surface, PatternChannel channel, int rgbChannelIndex)
        {
            byte[] pixels = channel.GetChannelData();

            fixed (byte* ptr = pixels)
            {
                if (channel.Depth == 16)
                {
                    int width = surface.Width;
                    int height = surface.Height;
                    int srcStride = width * 2;

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = ptr + (y * srcStride);
                        ColorBgra* dst = surface.GetRowPointerUnchecked(y);

                        for (int x = 0; x < width; x++)
                        {
                            byte value = SixteenBitConversion.GetEightBitValue(src);

                            switch (rgbChannelIndex)
                            {
                                case 0:
                                    dst->R = value;
                                    break;
                                case 1:
                                    dst->G = value;
                                    break;
                                case 2:
                                    dst->B = value;
                                    break;
                            }

                            src += 2;
                            dst++;
                        }
                    }
                }
                else
                {
                    var bgrChannelIndex = rgbChannelIndex switch
                    {
                        0 => 2,
                        2 => 0,
                        _ => rgbChannelIndex,
                    };
                    RegionPtr<byte> source = new(pixels, ptr, surface.Width, surface.Height, surface.Width);
                    RegionPtr<ColorBgra32> target = new(surface,
                                                        (ColorBgra32*)surface.Scan0.VoidStar,
                                                        surface.Width,
                                                        surface.Height,
                                                        surface.Stride);

                    PixelKernels.ReplaceChannel(target, source, bgrChannelIndex);
                }
            }
        }

        private sealed class PatternData : IDisposable
        {
            public string name;
            public Surface pattern;

            public PatternData(string name, Surface pattern)
            {
                this.name = name;
                this.pattern = pattern;
            }

            public void Dispose()
            {
                if (pattern != null)
                {
                    pattern.Dispose();
                    pattern = null;
                }
            }
        }

        private static class SixteenBitConversion
        {
            private static readonly ImmutableArray<byte> EightBitLookupTable = CreateEightBitLookupTable();

            public static unsafe byte GetEightBitValue(byte* data)
            {
                // The 16-bit brush data is stored as a big-endian integer in the range of [0, 32768].
                ushort value = Unsafe.ReadUnaligned<ushort>(data);

                if (BitConverter.IsLittleEndian)
                {
                    value = BinaryPrimitives.ReverseEndianness(value);
                }

                // Because an unsigned value can never be negative we only need to clamp
                // to the upper bound of the lookup table.
                return EightBitLookupTable[Math.Min((int)value, 32768)];
            }

            private static ImmutableArray<byte> CreateEightBitLookupTable()
            {
                ImmutableArray<byte>.Builder builder = ImmutableArray.CreateBuilder<byte>(32769);

                for (int i = 0; i < builder.Capacity; i++)
                {
                    // The 16-bit brush data is stored in the range of [0, 32768].
                    builder.Add((byte)((i * 10) / 1285));
                }

                return builder.MoveToImmutable();
            }
        }
    }
}
