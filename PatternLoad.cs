/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop Pattern FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2018 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using PatternFileTypePlugin.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace PatternFileTypePlugin
{
    internal static class PatternLoad
    {
        public static Document Load(Stream stream)
        {
            using (BigEndianBinaryReader reader = new BigEndianBinaryReader(stream))
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
                foreach (var item in patterns)
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
                            if (tempLayer != null)
                            {
                                tempLayer.Dispose();
                                tempLayer = null;
                            }
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
                    if (tempDoc != null)
                    {
                        tempDoc.Dispose();
                        tempDoc = null;
                    }
                }

                return doc;
            }
        }

        private static unsafe List<PatternData> LoadPatterns(BigEndianBinaryReader reader, uint count)
        {
            List<PatternData> patterns = new List<PatternData>((int)count);

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

                int channelCount = 0;
                byte[] indexedColorTable = null;

                if (imageMode == ImageType.Indexed)
                {
                    channelCount = 1;
                    indexedColorTable = reader.ReadBytes(768);

                    // Skip the 2 unknown UInt16 values, colors used and colors important?
                    reader.Position += 4L;
                }
                else if (imageMode == ImageType.RGB)
                {
                    channelCount = 3;
                }
                else if (imageMode == ImageType.Grayscale)
                {
                    channelCount = 1;
                }

                uint subVersion = reader.ReadUInt32();
                if (subVersion != PatternConstants.RecordSubVersion)
                {
                    throw new FormatException(string.Format(CultureInfo.CurrentCulture, Resources.UnsupportedPatternRecordSubVersion, version, subVersion));
                }

                uint patternSize = reader.ReadUInt32();

                if (channelCount == 0)
                {
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

                    byte[] pixels = new byte[bounds.Width * bounds.Height * channelCount];

                    for (int j = 0; j < channelCount; j++)
                    {
                        PatternChannel channel = new PatternChannel(reader);
                        DecodeChannelData(channel, pixels, j, channelCount);
                    }

                    byte[] alpha = null;

                    PatternChannel alphaChannel = GetAlphaChannel(reader, imageMode, nextPatternOffset);
                    if (alphaChannel != null)
                    {
                        alpha = new byte[bounds.Width * bounds.Height];
                        DecodeChannelData(alphaChannel, alpha, 0, 1);
                    }

                    long remainder = nextPatternOffset - reader.Position;

                    if (remainder > 0)
                    {
                        reader.Position += remainder;
                    }

                    patterns.Add(new PatternData(name, RenderPattern(bounds.Width, bounds.Height, channelCount, imageMode, pixels, indexedColorTable, alpha)));

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

                    PatternChannel channel = new PatternChannel(reader);

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

        private static unsafe void DecodeChannelData(PatternChannel channel, byte[] buffer, int offset, int channelCount)
        {
            int width = channel.Bounds.Width;
            int height = channel.Bounds.Height;
            byte[] channelData = channel.GetChannelData();

            fixed (byte* srcPtr = channelData, buf = buffer)
            {
                int srcStride = width;
                byte* dstPtr = buf + offset;
                int dstStride = width * channelCount;
                if (channel.Depth == 16)
                {
                    srcStride *= 2;

                    for (int y = 0; y < height; y++)
                    {
                        byte* src = srcPtr + (y * srcStride);
                        byte* dst = dstPtr + (y * dstStride);

                        for (int x = 0; x < width; x++)
                        {
                            // The 16-bit values are stored as big endian in the range of [0, 32768].
                            ushort val = (ushort)((src[0] << 8) | src[1]);
                            *dst = (byte)((val * 10) / 1285);

                            src += 2;
                            dst += channelCount;
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        byte* src = srcPtr + (y * srcStride);
                        byte* dst = dstPtr + (y * dstStride);

                        for (int x = 0; x < width; x++)
                        {
                            *dst = *src;

                            src++;
                            dst += channelCount;
                        }
                    }
                }
            }
        }

        private static unsafe Surface RenderPattern(int width, int height, int channelCount, ImageType imageMode, byte[] pixels, byte[] indexedColorTable, byte[] alpha)
        {
            Surface surface = null;
            Surface temp = null;

            try
            {
                temp = new Surface(width, height);

                fixed (byte* ptr = pixels)
                {
                    int srcStride = width * channelCount;

                    if (alpha == null)
                    {
                        new UnaryPixelOps.SetAlphaChannelTo255().Apply(temp, temp.Bounds);

                        for (int y = 0; y < height; y++)
                        {
                            byte* src = ptr + (y * srcStride);
                            ColorBgra* dst = temp.GetRowAddressUnchecked(y);

                            for (int x = 0; x < width; x++)
                            {
                                switch (imageMode)
                                {
                                    case ImageType.Grayscale:
                                        dst->R = dst->G = dst->B = *src;
                                        break;
                                    case ImageType.Indexed:
                                        int index = src[0] * 3;
                                        dst->R = indexedColorTable[index];
                                        dst->G = indexedColorTable[index + 1];
                                        dst->B = indexedColorTable[index + 2];
                                        break;
                                    case ImageType.RGB:
                                        dst->R = src[0];
                                        dst->G = src[1];
                                        dst->B = src[2];
                                        break;
                                }

                                src += channelCount;
                                dst++;
                            }
                        }
                    }
                    else
                    {
                        fixed (byte* alPtr = alpha)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                byte* src = ptr + (y * srcStride);
                                byte* al = alPtr + (y * width);
                                ColorBgra* dst = temp.GetRowAddressUnchecked(y);

                                for (int x = 0; x < width; x++)
                                {
                                    switch (imageMode)
                                    {
                                        case ImageType.Grayscale:
                                            dst->R = dst->G = dst->B = *src;
                                            break;
                                        case ImageType.Indexed:
                                            int index = src[0] * 3;
                                            dst->R = indexedColorTable[index];
                                            dst->G = indexedColorTable[index + 1];
                                            dst->B = indexedColorTable[index + 2];
                                            break;
                                        case ImageType.RGB:
                                            dst->R = src[0];
                                            dst->G = src[1];
                                            dst->B = src[2];
                                            break;
                                    }
                                    dst->A = *al;

                                    src += channelCount;
                                    al++;
                                    dst++;
                                }
                            }
                        }
                    }
                }

                surface = temp;
                temp = null;
            }
            finally
            {
                if (temp != null)
                {
                    temp.Dispose();
                    temp = null;
                }
            }

            return surface;
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
    }
}
