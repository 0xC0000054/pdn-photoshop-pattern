/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop Pattern FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012-2020 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Drawing;

namespace PatternFileTypePlugin
{
    internal sealed class PatternChannel
    {
#pragma warning disable IDE0032 // Use auto property
        private readonly bool enabled;
        private readonly uint size;
        private readonly uint depth32;
        private readonly Rectangle bounds;
        private readonly ushort depth16;
        private readonly PatternImageCompression compression;
        private byte[] channelData;
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

            channelData = new byte[channelDataStride * height];

            if (compression == PatternImageCompression.RLE)
            {
                short[] rowByteCount = new short[height];
                for (int i = 0; i < height; i++)
                {
                    rowByteCount[i] = reader.ReadInt16();
                }

                for (int y = 0; y < height; y++)
                {
                    RLEHelper.DecodedRow(reader, channelData, y * channelDataStride, channelDataStride);
                }
            }
            else
            {
                int numBytesToRead = channelData.Length;
                int numBytesRead = 0;
                while (numBytesToRead > 0)
                {
                    // Read may return anything from 0 to numBytesToRead.
                    int n = reader.Read(channelData, numBytesRead, numBytesToRead);
                    // The end of the file is reached.
                    if (n == 0)
                    {
                        break;
                    }
                    numBytesRead += n;
                    numBytesToRead -= n;
                }
            }
        }

        public PatternChannel(ushort depth, Rectangle bounds, PatternImageCompression compression, byte[] data)
        {
            enabled = true;
            size = 0; // Placeholder for the size written in WriteChannelData.
            depth32 = depth;
            this.bounds = bounds;
            depth16 = depth;
            this.compression = compression;
            channelData = data;
        }

        public bool Enabled => enabled;

        public uint Size => size;

        public Rectangle Bounds => bounds;

        public ushort Depth => depth16;

        public byte[] GetChannelData()
        {
            return channelData;
        }

        public void WriteChannelData(BigEndianBinaryWriter writer)
        {
            writer.Write(enabled ? 1U : 0U);

            using (new LengthWriter(writer))
            {
                writer.Write(depth32);
                writer.WriteInt32Rectangle(bounds);
                writer.Write(depth16);
                writer.Write((byte)compression);

                if (compression == PatternImageCompression.RLE)
                {
                    int width = bounds.Width;
                    int height = bounds.Height;

                    long rowCountPosition = writer.BaseStream.Position;

                    short[] rowCount = new short[height];
                    for (int i = 0; i < height; i++)
                    {
                        // Placeholder for the row byte length.
                        writer.Write(short.MaxValue);
                    }

                    for (int y = 0; y < height; y++)
                    {
                        rowCount[y] = (short)RLEHelper.EncodedRow(writer.BaseStream, channelData, y * width, width);
                    }

                    long current = writer.BaseStream.Position;

                    writer.BaseStream.Position = rowCountPosition;
                    for (int i = 0; i < height; i++)
                    {
                        writer.Write(rowCount[i]);
                    }

                    writer.BaseStream.Position = current;
                }
                else
                {
                    writer.Write(channelData);
                }
            }
        }
    }
}
