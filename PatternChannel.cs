/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop Pattern FileType Plugin for Paint.NET
// 
// This software is provided under the MIT License:
//   Copyright (c) 2017 Nicholas Hayes
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
        private readonly bool enabled;
        private readonly uint size;
        private readonly uint depth32;
        private readonly Rectangle bounds;
        private readonly ushort depth16;
        private readonly PatternImageCompression compression;
        private byte[] channelData;
       
        public PatternChannel(BinaryReverseReader reader)
        {
            this.enabled = reader.ReadUInt32() != 0;
            this.size = reader.ReadUInt32();
            this.depth32 = reader.ReadUInt32();
            this.bounds = reader.ReadInt32Rectangle();
            this.depth16 = reader.ReadUInt16();
            this.compression = (PatternImageCompression)reader.ReadByte();

            if (this.depth16 != 8 && this.depth16 != 16)
            {
                throw new FormatException(Properties.Resources.UnsupportedChannelDepth);
            }

            int height = bounds.Height;

            int channelDataStride = bounds.Width;
            if (this.depth16 == 16)
            {
                channelDataStride *= 2;
            }

            this.channelData = new byte[channelDataStride * height];

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
            this.enabled = true;
            this.size = 0; // Placeholder for the size written in WriteChannelData.
            this.depth32 = depth;
            this.bounds = bounds;
            this.depth16 = depth;
            this.compression = compression;
            this.channelData = data;
        }

        public bool Enabled
        {
            get
            {
                return this.enabled;
            }
        }

        public uint Size
        {
            get
            {
                return this.size;
            }
        }

        public Rectangle Bounds
        {
            get
            {
                return this.bounds;
            }
        }

        public ushort Depth
        {
            get
            {
                return this.depth16;
            }
        }

        public byte[] GetChannelData()
        {
            return this.channelData;
        }

        public void WriteChannelData(BinaryReverseWriter writer)
        {
            writer.Write(this.enabled ? 1U : 0U);

            using (new LengthWriter(writer))
            {
                writer.Write(this.depth32);
                writer.WriteInt32Rectangle(this.bounds);
                writer.Write(this.depth16);
                writer.Write((byte)this.compression);

                if (this.compression == PatternImageCompression.RLE)
                {
                    int width = this.bounds.Width;
                    int height = this.bounds.Height;

                    long rowCountPosition = writer.BaseStream.Position;

                    short[] rowCount = new short[height];
                    for (int i = 0; i < height; i++)
                    {
                        // Placeholder for the row byte length.
                        writer.Write(short.MaxValue);
                    }

                    for (int y = 0; y < height; y++)
                    {
                        rowCount[y] = (short)RLEHelper.EncodedRow(writer.BaseStream, this.channelData, y * width, width);
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
                    writer.Write(this.channelData);
                }
            }
        }
    }
}
