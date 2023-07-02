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

using CommunityToolkit.HighPerformance.Buffers;
using System;
using System.Drawing;

namespace PatternFileTypePlugin
{
    internal sealed class PatternChannel : IDisposable
    {
#pragma warning disable IDE0032 // Use auto property
        private readonly bool enabled;
        private readonly uint size;
        private readonly uint depth32;
        private readonly Rectangle bounds;
        private readonly ushort depth16;
        private readonly PatternImageCompression compression;
        private MemoryOwner<byte> channelData;
#pragma warning restore IDE0032 // Use auto property

        public PatternChannel(BigEndianBinaryReader reader)
        {
            enabled = reader.ReadUInt32() != 0;
            size = reader.ReadUInt32();
            depth32 = reader.ReadUInt32();
            bounds = reader.ReadInt32Rectangle();
            depth16 = reader.ReadUInt16();
            compression = (PatternImageCompression)reader.ReadByte();

            if (depth16 != 8 && depth16 != 16)
            {
                throw new FormatException(Properties.Resources.UnsupportedChannelDepth);
            }

            int height = bounds.Height;

            int channelDataStride = bounds.Width;
            if (depth16 == 16)
            {
                channelDataStride *= 2;
            }

            channelData = MemoryOwner<byte>.Allocate(channelDataStride * height);

            try
            {
                Span<byte> channelDataSpan = channelData.Span;

                if (compression == PatternImageCompression.RLE)
                {
                    // Skip the row byte counts
                    reader.Position += (long)height * sizeof(short);

                    for (int y = 0; y < height; y++)
                    {
                        RLEHelper.DecodedRow(reader, channelDataSpan.Slice(y * channelDataStride, channelDataStride));
                    }
                }
                else
                {
                    reader.ProperRead(channelDataSpan);
                }
            }
            catch (Exception)
            {
                channelData.Dispose();
                throw;
            }
        }

        public PatternChannel(Rectangle bounds, PatternImageCompression compression)
        {
            enabled = true;
            size = 0; // Placeholder for the size written in Write.
            depth32 = 8;
            this.bounds = bounds;
            depth16 = 8;
            this.compression = compression;
            channelData = MemoryOwner<byte>.Allocate(bounds.Width * bounds.Height);
        }

        public bool Enabled => enabled;

        public uint Size => size;

        public Rectangle Bounds => bounds;

        public ushort Depth => depth16;

        public int Stride { get; }

        public void Dispose()
        {
            if (channelData != null)
            {
                channelData.Dispose();
                channelData = null;
            }
        }

        public Span<byte> GetChannelData()
        {
            return channelData.Span;
        }

        public void Write(BigEndianBinaryWriter writer)
        {
            writer.Write(enabled ? 1U : 0U);

            using (new LengthWriter(writer))
            {
                writer.Write(depth32);
                writer.WriteInt32Rectangle(bounds);
                writer.Write(depth16);
                writer.Write((byte)compression);

                ReadOnlySpan<byte> channelDataSpan = channelData.Span;

                if (compression == PatternImageCompression.RLE)
                {
                    int width = bounds.Width;
                    int height = bounds.Height;

                    long rowCountPosition = writer.Position;

                    for (int i = 0; i < height; i++)
                    {
                        // Placeholder for the row byte length.
                        writer.Write(short.MaxValue);
                    }

                    using (SpanOwner<short> rowCountOwner = SpanOwner<short>.Allocate(height))
                    {
                        Span<short> rowCount = rowCountOwner.Span;

                        for (int y = 0; y < height; y++)
                        {
                            rowCount[y] = (short)RLEHelper.EncodedRow(writer, channelDataSpan.Slice(y * width, width));
                        }

                        long current = writer.Position;

                        writer.Position = rowCountPosition;
                        for (int i = 0; i < height; i++)
                        {
                            writer.Write(rowCount[i]);
                        }

                        writer.Position = current;
                    }
                }
                else
                {
                    writer.Write(channelDataSpan);
                }
            }
        }
    }
}
