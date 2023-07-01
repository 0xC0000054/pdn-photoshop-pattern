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

// Portions of this file has been adapted from:
/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop PSD FileType Plugin for Paint.NET
// http://psdplugin.codeplex.com/
//
// This software is provided under the MIT License:
//   Copyright (c) 2006-2007 Frank Blumenberg
//   Copyright (c) 2010-2011 Tao Yue
//
// Portions of this file are provided under the BSD 3-clause License:
//   Copyright (c) 2006, Jonas Beckeman
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;

namespace PatternFileTypePlugin
{
    static class RLEHelper
    {
        ////////////////////////////////////////////////////////////////////////

        private class RlePacketStateMachine
        {
            private const int maxPacketLength = 128;

            private bool rlePacket = false;
            private byte lastValue;
            private int idxPacketData;
            private int packetLength;
            private readonly BigEndianBinaryWriter writer;

            internal RlePacketStateMachine(BigEndianBinaryWriter writer)
            {
                this.writer = writer;
            }

            internal void Flush(ReadOnlySpan<byte> data)
            {
                byte header;
                if (rlePacket)
                {
                    header = (byte)(-(packetLength - 1));
                    writer.Write(header);
                    writer.Write(lastValue);
                }
                else
                {
                    header = (byte)(packetLength - 1);
                    writer.Write(header);
                    writer.Write(data.Slice(idxPacketData, packetLength));
                }

                packetLength = 0;
            }

            internal void PushRow(ReadOnlySpan<byte> row)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    byte color = row[i];
                    if (packetLength == 0)
                    {
                        // Starting a fresh packet.
                        rlePacket = false;
                        lastValue = color;
                        idxPacketData = i;
                        packetLength = 1;
                    }
                    else if (packetLength == 1)
                    {
                        // 2nd byte of this packet... decide RLE or non-RLE.
                        rlePacket = (color == lastValue);
                        lastValue = color;
                        packetLength = 2;
                    }
                    else if (packetLength == maxPacketLength)
                    {
                        // Packet is full. Start a new one.
                        Flush(row);
                        rlePacket = false;
                        lastValue = color;
                        idxPacketData = i;
                        packetLength = 1;
                    }
                    else if (packetLength >= 2 && rlePacket && color != lastValue)
                    {
                        // We were filling in an RLE packet, and we got a non-repeated color.
                        // Emit the current packet and start a new one.
                        Flush(row);
                        rlePacket = false;
                        lastValue = color;
                        idxPacketData = i;
                        packetLength = 1;
                    }
                    else if (packetLength >= 2 && rlePacket && color == lastValue)
                    {
                        // We are filling in an RLE packet, and we got another repeated color.
                        // Add the new color to the current packet.
                        ++packetLength;
                    }
                    else if (packetLength >= 2 && !rlePacket && color != lastValue)
                    {
                        // We are filling in a raw packet, and we got another random color.
                        // Add the new color to the current packet.
                        lastValue = color;
                        ++packetLength;
                    }
                    else if (packetLength >= 2 && !rlePacket && color == lastValue)
                    {
                        // We were filling in a raw packet, but we got a repeated color.
                        // Emit the current packet without its last color, and start a
                        // new RLE packet that starts with a length of 2.
                        --packetLength;
                        Flush(row);
                        rlePacket = true;
                        packetLength = 2;
                        lastValue = color;
                    }
                }

                Flush(row);
            }
        }

        ////////////////////////////////////////////////////////////////////////

        public static int EncodedRow(BigEndianBinaryWriter writer, ReadOnlySpan<byte> row)
        {
            long startPosition = writer.Position;

            RlePacketStateMachine machine = new(writer);
            machine.PushRow(row);

            return (int)(writer.Position - startPosition);
        }

        ////////////////////////////////////////////////////////////////////////

        public static void DecodedRow(BigEndianBinaryReader reader, Span<byte> destination)
        {
            int count = 0;
            while (count < destination.Length)
            {
                byte byteValue = reader.ReadByte();

                int len = byteValue;
                if (len < 128)
                {
                    len++;

                    reader.ProperRead(destination.Slice(count, len));

                    count += len;
                }
                else if (len > 128)
                {
                    // Next -len+1 bytes in the dest are replicated from next source byte.
                    // (Interpret len as a negative 8-bit int.)
                    len ^= 0x0FF;
                    len += 2;

                    byteValue = reader.ReadByte();

                    destination.Slice(count, len).Fill(byteValue);

                    count += len;
                }
            }
        }
    }
}