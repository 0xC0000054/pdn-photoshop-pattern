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
using System.Buffers;
using System.Buffers.Binary;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace PatternFileTypePlugin
{
    // Adapted from 'Problem and Solution: The Terrible Inefficiency of FileStream and BinaryReader'
    // https://jacksondunstan.com/articles/3568

    internal sealed class BigEndianBinaryReader : IDisposable
    {
        private Stream stream;
        private int readOffset;
        private int readLength;

        private readonly byte[] buffer;
        private readonly int bufferSize;

        private const int MaxBufferSize = 4096;

        /// <summary>
        /// Initializes a new instance of the <see cref="BigEndianBinaryReader"/> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public BigEndianBinaryReader(Stream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            bufferSize = (int)Math.Min(stream.Length, MaxBufferSize);
            buffer = new byte[bufferSize];
            readOffset = 0;
            readLength = 0;
        }

        /// <summary>
        /// Gets the length of the stream.
        /// </summary>
        /// <value>
        /// The length of the stream.
        /// </value>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long Length
        {
            get
            {
                VerifyNotDisposed();

                return stream.Length;
            }
        }

        /// <summary>
        /// Gets or sets the position in the stream.
        /// </summary>
        /// <value>
        /// The position in the stream.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">value is negative.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long Position
        {
            get
            {
                VerifyNotDisposed();

                return stream.Position - readLength + readOffset;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                VerifyNotDisposed();

                long current = Position;

                if (value != current)
                {
                    long bufferStartOffset = current - readOffset;
                    long bufferEndOffset = bufferStartOffset + readLength;

                    // Avoid reading from the stream if the offset is within the current buffer.
                    if (value >= bufferStartOffset && value <= bufferEndOffset)
                    {
                        readOffset = (int)(value - bufferStartOffset);
                    }
                    else
                    {
                        // Invalidate the existing buffer.
                        readOffset = 0;
                        readLength = 0;
                        stream.Seek(value, SeekOrigin.Begin);
                    }
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream, starting from a specified point in the byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="offset">The starting offset in the array.</param>
        /// <param name="count">The count.</param>
        /// <returns>The number of bytes read from the stream.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int Read(byte[] bytes, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(bytes);

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            VerifyNotDisposed();

            return ReadInternal(new Span<byte>(bytes, offset, count));
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream.
        /// </summary>
        /// <param name="destination">The destination.</param>
        /// <returns>The number of bytes read from the stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int Read(Span<byte> destination)
        {
            VerifyNotDisposed();

            return ReadInternal(destination);
        }


        /// <summary>
        /// Reads the specified number of bytes from the stream, starting from a specified point in the byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="offset">The starting offset in the array.</param>
        /// <param name="count">The count.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void ProperRead(byte[] bytes, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(bytes);

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            ProperRead(new Span<byte>(bytes, offset, count));
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream.
        /// </summary>
        /// <param name="span">The span.</param>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public void ProperRead(Span<byte> span)
        {
            VerifyNotDisposed();

            int count = span.Length;

            if (count == 0)
            {
                return;
            }

            int totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                int bytesRead = ReadInternal(span.Slice(totalBytesRead, count - totalBytesRead));

                if (bytesRead == 0)
                {
                    throw new EndOfStreamException();
                }

                totalBytesRead += bytesRead;
            }
        }

        /// <summary>
        /// Reads the next byte from the current stream.
        /// </summary>
        /// <returns>The next byte read from the current stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public byte ReadByte()
        {
            return readOffset < readLength ? buffer[readOffset++] : ReadByteSlow();

            byte ReadByteSlow()
            {
                VerifyNotDisposed();

                FillBuffer(sizeof(byte));

                byte val = buffer[readOffset];
                readOffset += sizeof(byte);

                return val;
            }
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream.
        /// </summary>
        /// <param name="count">The number of bytes to read..</param>
        /// <returns>An array containing the specified bytes.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public byte[] ReadBytes(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            VerifyNotDisposed();

            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] bytes = new byte[count];

            if ((readOffset + count) <= readLength)
            {
                Buffer.BlockCopy(buffer, readOffset, bytes, 0, count);
                readOffset += count;
            }
            else
            {
                // Ensure that any bytes at the end of the current buffer are included.
                int bytesUnread = readLength - readOffset;

                if (bytesUnread > 0)
                {
                    Buffer.BlockCopy(buffer, readOffset, bytes, 0, bytesUnread);
                }

                int numBytesToRead = count - bytesUnread;
                int numBytesRead = bytesUnread;
                do
                {
                    int n = stream.Read(bytes, numBytesRead, numBytesToRead);

                    if (n == 0)
                    {
                        throw new EndOfStreamException();
                    }

                    numBytesRead += n;
                    numBytesToRead -= n;

                } while (numBytesToRead > 0);

                // Invalidate the existing buffer.
                readOffset = 0;
                readLength = 0;
            }

            return bytes;
        }

        /// <summary>
        /// Reads a 8-byte floating point value in big endian byte order.
        /// </summary>
        /// <returns>The 8-byte floating point value.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public unsafe double ReadDouble()
        {
            ulong temp = ReadUInt64();

            return *(double*)&temp;
        }

        /// <summary>
        /// Reads a 2-byte signed integer in big endian byte order.
        /// </summary>
        /// <returns>The 2-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public short ReadInt16()
        {
            return (short)ReadUInt16();
        }

        /// <summary>
        /// Reads a 2-byte unsigned integer in big endian byte order.
        /// </summary>
        /// <returns>The 2-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public ushort ReadUInt16()
        {
            VerifyNotDisposed();

            EnsureBuffer(sizeof(ushort));

            ushort value = Unsafe.ReadUnaligned<ushort>(ref buffer[readOffset]);
            readOffset += sizeof(ushort);

            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        /// <summary>
        /// Reads a 4-byte signed integer in big endian byte order.
        /// </summary>
        /// <returns>The 4-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer in big endian byte order.
        /// </summary>
        /// <returns>The 4-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public uint ReadUInt32()
        {
            VerifyNotDisposed();

            EnsureBuffer(sizeof(uint));

            uint value = Unsafe.ReadUnaligned<uint>(ref buffer[readOffset]);
            readOffset += sizeof(uint);

            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        /// <summary>
        /// Reads a 4-byte floating point value in big endian byte order.
        /// </summary>
        /// <returns>The 4-byte floating point value.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public unsafe float ReadSingle()
        {
            uint temp = ReadUInt32();

            return *(float*)&temp;
        }

        /// <summary>
        /// Reads a 8-byte signed integer in big endian byte order.
        /// </summary>
        /// <returns>The 8-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long ReadInt64()
        {
            return (long)ReadUInt64();
        }

        /// <summary>
        /// Reads a 8-byte unsigned integer in big endian byte order.
        /// </summary>
        /// <returns>The 8-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public ulong ReadUInt64()
        {
            VerifyNotDisposed();

            EnsureBuffer(sizeof(ulong));

            ulong value = Unsafe.ReadUnaligned<ulong>(ref buffer[readOffset]);
            readOffset += sizeof(ulong);

            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads the pascal string.
        /// </summary>
        /// <returns>A string containing the characters of the Pascal string.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public string ReadPascalString()
        {
            VerifyNotDisposed();

            byte stringLength = ReadByte();

            if (stringLength == 0)
            {
                return string.Empty;
            }

            EnsureBuffer(stringLength);

            string result = Encoding.ASCII.GetString(buffer, readOffset, stringLength);

            readOffset += stringLength;

            return result;
        }

        /// <summary>
        /// Reads a rectangle comprised of 4-byte signed integers.
        /// </summary>
        /// <returns>A rectangle comprised of 4-byte signed integers.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public Rectangle ReadInt32Rectangle()
        {
            VerifyNotDisposed();

#pragma warning disable IDE0017 // Simplify object initialization
            Rectangle rect = new();
#pragma warning restore IDE0017 // Simplify object initialization

            rect.Y = ReadInt32();
            rect.X = ReadInt32();
            rect.Height = ReadInt32() - rect.Y;
            rect.Width = ReadInt32() - rect.X;

            return rect;
        }

        /// <summary>
        /// Reads a length-prefixed UTF-16 string.
        /// </summary>
        /// <returns>A string containing the characters of the UTF-16 string..</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public string ReadUnicodeString()
        {
            VerifyNotDisposed();

            int lengthInChars = ReadInt32();

            if (lengthInChars == 0)
            {
                return string.Empty;
            }

            int lengthInBytes = checked(lengthInChars * 2);

            Span<byte> stringData;
            byte[] arrayFromPool = null;

            if (lengthInBytes <= bufferSize)
            {
                EnsureBuffer(lengthInBytes);
                stringData = new Span<byte>(buffer, readOffset, lengthInBytes);

                readOffset += lengthInBytes;
            }
            else
            {
                arrayFromPool = ArrayPool<byte>.Shared.Rent(lengthInBytes);
                stringData = new Span<byte>(arrayFromPool, 0, lengthInBytes);
                ProperRead(stringData);
            }

            string result;

            try
            {
                int stringLengthInBytes = lengthInBytes;

                // Skip any UTF-16 NUL characters at the end of the string.
                while (stringLengthInBytes > 0
                       && stringData[stringLengthInBytes - 1] == 0
                       && stringData[stringLengthInBytes - 2] == 0)
                {
                    stringLengthInBytes -= 2;
                }

                stringData = stringData.Slice(0, stringLengthInBytes);

                if (stringData.Length == 0)
                {
                    result = string.Empty;
                }
                else
                {
                    result = Encoding.BigEndianUnicode.GetString(stringData);
                }
            }
            finally
            {
                if (arrayFromPool != null)
                {
                    ArrayPool<byte>.Shared.Return(arrayFromPool);
                }
            }

            return result;
        }

        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Ensures that the buffer contains at least the number of bytes requested.
        /// </summary>
        /// <param name="count">The minimum number of bytes the buffer should contain.</param>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        private void EnsureBuffer(int count)
        {
            if ((readOffset + count) > readLength)
            {
                FillBuffer(count);
            }
        }

        /// <summary>
        /// Fills the buffer with at least the number of bytes requested.
        /// </summary>
        /// <param name="minBytes">The minimum number of bytes to place in the buffer.</param>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        private void FillBuffer(int minBytes)
        {
            if (!TryFillBuffer(minBytes))
            {
                ThrowEndOfStreamException();
            }

            static void ThrowEndOfStreamException()
            {
                throw new EndOfStreamException();
            }
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream.
        /// </summary>
        /// <param name="destination">The span.</param>
        /// <returns>The number of bytes read from the stream.</returns>
        private int ReadInternal(Span<byte> destination)
        {
            int count = destination.Length;

            if (count == 0)
            {
                return 0;
            }

            if ((readOffset + count) <= readLength)
            {
                new ReadOnlySpan<byte>(buffer, readOffset, count).CopyTo(destination);
                readOffset += count;

                return count;
            }
            else
            {
                int totalBytesRead;

                if (count < bufferSize)
                {
                    // This is an optimization for sequentially reading small ranges of bytes
                    // from a file.
                    // For example, a file signature that will be followed by other header data.
                    //
                    // TryFillBuffer may return fewer bytes than were requested if the end of the
                    // stream has been reached.
                    totalBytesRead = TryFillBuffer(count) ? count : readLength;

                    if (totalBytesRead > 0)
                    {
                        new ReadOnlySpan<byte>(buffer, readOffset, totalBytesRead).CopyTo(destination.Slice(0, totalBytesRead));

                        readOffset += totalBytesRead;
                    }
                }
                else
                {
                    // Ensure that any bytes at the end of the current buffer are included.
                    int bytesUnread = readLength - readOffset;

                    if (bytesUnread > 0)
                    {
                        new ReadOnlySpan<byte>(buffer, readOffset, bytesUnread).CopyTo(destination);
                    }

                    totalBytesRead = bytesUnread;
                    int bytesRemaining = count - bytesUnread;

                    // Invalidate the existing buffer.
                    readOffset = 0;
                    readLength = 0;

                    totalBytesRead += stream.Read(destination.Slice(bytesUnread, bytesRemaining));
                }

                return totalBytesRead;
            }
        }

        /// <summary>
        /// Attempts to fill the buffer with at least the number of bytes requested.
        /// </summary>
        /// <param name="minBytes">The minimum number of bytes to place in the buffer.</param>
        /// <returns>
        /// <see langword="true"/> if the buffer contains at least <paramref name="minBytes"/>; otherwise, <see langword="false"/>.
        /// </returns>
        private bool TryFillBuffer(int minBytes)
        {
            int bytesUnread = readLength - readOffset;

            if (bytesUnread > 0)
            {
                Buffer.BlockCopy(buffer, readOffset, buffer, 0, bytesUnread);
            }

            readOffset = 0;
            readLength = bytesUnread;
            do
            {
                int bytesRead = stream.Read(buffer, readLength, bufferSize - readLength);

                if (bytesRead == 0)
                {
                    return false;
                }

                readLength += bytesRead;

            } while (readLength < minBytes);

            return true;
        }

        private void VerifyNotDisposed()
        {
            if (stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }
        }
    }
}
